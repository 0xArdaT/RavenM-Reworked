using System;
using System.Collections;
using System.IO;
using System.Reflection;
using RavenM.DiscordGameSDK;
using Steamworks;
using UnityEngine;
using System.Runtime.InteropServices;

namespace RavenM
{
    public class DiscordIntegration : MonoBehaviour
    {
        public static DiscordIntegration instance;

        public Discord Discord;

        public long discordClientID = 1007054793220571247;

        public long startSessionTime;

        private ActivityManager _activityManager;

        private enum DiscordState
        {
            Disconnected,
            Connected,
        }

        private DiscordState _state = DiscordState.Disconnected;

        private TimedAction _reconnectTimer = new TimedAction(5f);
        private TimedAction _presenceTimer = new TimedAction(5f);
        private bool _needsPresenceUpdate = true;
        private string _pendingDisconnectReason;
        private string _lastDiscordStatus;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpPathName);

        private void Awake()
        {
            instance = this;
        }

        private void EnsureDiscordLibrary()
        {
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dllPath = Path.Combine(pluginDir, "discord_game_sdk.dll");

            if (!File.Exists(dllPath))
            {
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = null;

                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("discord_game_sdk.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(resourceName))
                {
                    Plugin.logger.LogError("Embedded discord_game_sdk.dll resource not found. Discord RPC will not work without the native library.");
                    return;
                }

                try
                {
                    using (Stream s = assembly.GetManifestResourceStream(resourceName))
                    using (FileStream fs = new FileStream(dllPath, FileMode.Create, FileAccess.Write))
                    {
                        s.CopyTo(fs);
                    }

                    Plugin.logger.LogInfo("Extracted discord_game_sdk.dll alongside RavenM.dll.");
                }
                catch (Exception e)
                {
                    Plugin.logger.LogWarning($"Failed to extract discord_game_sdk.dll: {e.Message}");
                    return;
                }
            }

            IntPtr hMod = LoadLibrary(dllPath);
            if (hMod == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Plugin.logger.LogWarning($"LoadLibrary discord_game_sdk.dll failed (error {err}). Discord may not function.");
            }
        }

        private void Start()
        {
            _reconnectTimer.Start();
            _presenceTimer.Start();
            TryInitialize();
        }

        private void TryInitialize()
        {
            if (Discord != null)
                return;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.Is64BitProcess)
            {
                EnsureDiscordLibrary();
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                if (Directory.Exists("ravenfield_Data")) // Assume its the linux installation
                {
                    if (!File.Exists("ravenfield_Data/Plugins/discord_game_sdk.so"))
                    {
                        Plugin.logger.LogWarning("Linux Discord Library Not Found, Attempting to Copy it from lib folder");

                        File.Copy("BepInEx/plugins/lib/discord_game_sdk.so", "ravenfield_Data/Plugins/discord_game_sdk.so");
                    }
                }
                else if (Directory.Exists("ravenfield.app")) // Assume its the MacOS installation
                {
                    if (!File.Exists("ravenfield.app/Contents/Plugins/discord_game_sdk.dylib"))
                    {
                        Plugin.logger.LogWarning("MacOS Discord Library Not Found, Attempting to Copy it from lib folder");

                        File.Copy("BepInEx/plugins/lib/discord_game_sdk.dylib", "ravenfield.app/Contents/Plugins/discord_game_sdk.dylib");
                    }
                    if (!File.Exists("ravenfield.app/Contents/Plugins/discord_game_sdk.bundle"))
                    {
                        Plugin.logger.LogWarning("MacOS Discord Library Not Found, Attempting to Copy it from lib folder");

                        File.Copy("BepInEx/plugins/lib/discord_game_sdk.bundle", "ravenfield.app/Contents/Plugins/discord_game_sdk.bundle");
                    }
                }
            }

            try
            {
                Discord = new Discord(discordClientID, (UInt64)CreateFlags.NoRequireDiscord);
                _activityManager = Discord.GetActivityManager();

                _activityManager.OnActivityJoin += OnDiscordActivityJoin;
                _activityManager.OnActivityJoinRequest += OnDiscordActivityJoinRequest;
                _activityManager.OnActivitySpectate += secret =>
                {
                    Plugin.logger.LogInfo($"OnActivitySpectate {secret}");
                };

                startSessionTime = ((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
                _state = DiscordState.Connected;
                _needsPresenceUpdate = true;
                _pendingDisconnectReason = null;

                LogDiscordStatus("Discord RPC connected.");
                UpdatePresence();
            }
            catch (Exception e)
            {
                string resultMsg = "";
                if (e is ResultException re)
                    resultMsg = $" ({re.Result})";

                LogDiscordStatus($"Discord RPC not available{resultMsg}: {e.Message}");
                DisposeDiscord();
            }
            finally
            {
                _reconnectTimer.Start();
            }
        }

        private void Disconnect(string reason)
        {
            if (_state == DiscordState.Connected)
                LogDiscordStatus($"Discord RPC disconnected: {reason}");

            _state = DiscordState.Disconnected;
            _pendingDisconnectReason = null;
            DisposeDiscord();
            _reconnectTimer.Start();
        }

        private void DisposeDiscord()
        {
            if (_activityManager != null)
            {
                try
                {
                    _activityManager.ClearActivity(result => { });
                }
                catch
                {
                    // ignored
                }
                _activityManager = null;
            }

            if (Discord != null)
            {
                try
                {
                    Discord.Dispose();
                }
                catch
                {
                    // ignored
                }
                Discord = null;
            }
        }

        private void LogDiscordStatus(string message)
        {
            if (message == _lastDiscordStatus)
                return;

            _lastDiscordStatus = message;

            if (message.StartsWith("Discord RPC connected"))
                Plugin.logger.LogInfo(message);
            else
                Plugin.logger.LogWarning(message);
        }

        private void OnApplicationQuit()
        {
            DisposeDiscord();
        }

        private void FixedUpdate()
        {
            if (Discord == null)
            {
                if (_reconnectTimer.TrueDone())
                {
                    TryInitialize();
                }
                return;
            }

            try
            {
                Discord.RunCallbacks();
            }
            catch (Exception e)
            {
                string resultMsg = "";
                if (e is ResultException re)
                    resultMsg = $" ({re.Result})";

                Disconnect($"RunCallbacks error{resultMsg}: {e.Message}");
                return;
            }

            if (!string.IsNullOrEmpty(_pendingDisconnectReason))
            {
                var reason = _pendingDisconnectReason;
                _pendingDisconnectReason = null;
                Disconnect(reason);
                return;
            }

            if (_presenceTimer.TrueDone() || _needsPresenceUpdate)
            {
                _needsPresenceUpdate = false;
                UpdatePresence();
                _presenceTimer.Start();
            }
        }

        private void UpdatePresence()
        {
            if (Discord == null || GameManager.instance == null)
                return;

            ChangeActivityDynamically();
        }

        private bool _isInGame;
        private bool _isInLobby;

        void ChangeActivityDynamically()
        {
            if (Discord == null || GameManager.instance == null)
                return;

            _isInGame = GameManager.instance.ingame;
            _isInLobby = LobbySystem.instance != null && LobbySystem.instance.InLobby;

            if (_isInGame)
            {
                _gameMode = GetGameModeName();
                var map = GetMapName();
                var score = GetScoreString();
                var (players, max) = GetPlayerCounts();
                var state = _isInLobby ? "Playing Multiplayer" : "Playing Singleplayer";
                var details = $"{map} - {_gameMode}{score} ({players}/{max} Players)";
                UpdateActivity(Discord, Activities.InMatch, state, details, players, max);
            }
            else if (_isInLobby)
            {
                _gameMode = GetGameModeName();
                var map = GetMapName();
                var (players, max) = GetPlayerCounts();
                var state = "Waiting In Lobby";
                var details = $"{map} - {_gameMode} ({players}/{max} Players)";
                UpdateActivity(Discord, Activities.InLobby, state, details, players, max, LobbySystem.instance.ActualLobbyID.ToString());
            }
            else
            {
                UpdateActivity(Discord, Activities.InMenu);
            }
        }

        private string _gameMode = "Insert Game Mode";

        private string GetMapName()
        {
            if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.mapDisplayName))
                return GameManager.instance.mapDisplayName;

            if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.sceneName))
                return GameManager.instance.sceneName;

            if (InstantActionConfigMenu.instance != null)
            {
                try
                {
                    var selectedMapField = typeof(InstantActionConfigMenu).GetField("selectedMap", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var selectedMap = selectedMapField?.GetValue(InstantActionConfigMenu.instance);
                    var sceneNameField = selectedMap?.GetType().GetField("sceneName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var sceneName = sceneNameField?.GetValue(selectedMap) as string;
                    if (!string.IsNullOrEmpty(sceneName))
                        return sceneName;
                }
                catch (Exception e)
                {
                    Plugin.logger.LogWarning($"Failed to read selected map for Discord presence: {e.Message}");
                }
            }

            return "Unknown Map";
        }

        private string GetGameModeName()
        {
            var modeInfo = GameManager.instance?.gameModeParameters?.gameMode;
            if (modeInfo == null)
                modeInfo = InstantActionConfigMenu.instance?.selectedGameMode;

            return !string.IsNullOrEmpty(modeInfo?.name) ? modeInfo.name : "Unknown Mode";
        }

        private (int current, int max) GetPlayerCounts()
        {
            if (LobbySystem.instance != null && LobbySystem.instance.InLobby && LobbySystem.instance.ActualLobbyID.IsValid())
            {
                int current = SteamMatchmaking.GetNumLobbyMembers(LobbySystem.instance.ActualLobbyID);
                int max = SteamMatchmaking.GetLobbyMemberLimit(LobbySystem.instance.ActualLobbyID);
                return (current, max);
            }

            return (1, 2);
        }

        private string GetScoreString()
        {
            var mode = GameModeBase.activeGameMode;
            if (mode == null)
                return string.Empty;

            try
            {
                var type = mode.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                var ticketsField = type.GetField("tickets", flags);
                if (ticketsField != null && ticketsField.FieldType == typeof(int[]))
                {
                    var tickets = (int[])ticketsField.GetValue(mode);
                    if (tickets != null && tickets.Length >= 2)
                        return $" | {tickets[0]}-{tickets[1]}";
                }

                var blueScoreField = type.GetField("blueScore", flags);
                var redScoreField = type.GetField("redScore", flags);
                if (blueScoreField != null && redScoreField != null)
                {
                    int blue = (int)blueScoreField.GetValue(mode);
                    int red = (int)redScoreField.GetValue(mode);
                    return $" | {blue}-{red}";
                }

                var battalionsField = type.GetField("remainingBattalions", flags);
                if (battalionsField != null && battalionsField.FieldType == typeof(int[]))
                {
                    var battalions = (int[])battalionsField.GetValue(mode);
                    if (battalions != null && battalions.Length >= 2)
                        return $" | {battalions[0]}-{battalions[1]}";
                }
            }
            catch (Exception e)
            {
                Plugin.logger.LogWarning($"Failed to read team scores for Discord presence: {e.Message}");
            }

            return string.Empty;
        }

        private void OnDiscordActivityJoin(string secret)
        {
            if (string.IsNullOrEmpty(secret))
                return;

            secret = secret.Replace("_join", "");
            Plugin.logger.LogInfo($"OnJoin {secret}");

            if (!ulong.TryParse(secret, out ulong lobbyIdUlong))
            {
                Plugin.logger.LogWarning("Discord join secret was not a valid lobby ID.");
                return;
            }

            var LobbyID = new CSteamID(lobbyIdUlong);

            if (_isInGame)
            {
                GameManager.ReturnToMenu();
            }

            if (LobbySystem.instance != null)
            {
                SteamMatchmaking.JoinLobby(LobbyID);
                LobbySystem.instance.InLobby = true;
                LobbySystem.instance.IsLobbyOwner = false;
                LobbySystem.instance.LobbyDataReady = false;
            }
        }

        private void OnDiscordActivityJoinRequest(ref User user)
        {
            Plugin.logger.LogInfo($"OnJoinRequest {user.Username} {user.Id}");

            if (_activityManager != null)
            {
                _activityManager.SendRequestReply(user.Id, ActivityJoinRequestReply.Yes, result =>
                {
                    if (result != Result.Ok)
                        Plugin.logger.LogWarning($"Discord join request reply failed: {result}");
                });
            }
        }

        public void UpdateActivity(Discord discord, Activities activity, string state = "", string details = "", int currentPlayers = 1, int maxPlayers = 2, string lobbyID = "None")
        {
            var activityManager = discord.GetActivityManager();
            var activityPresence = new Activity();

            switch (activity)
            {
                case Activities.InMenu:
                    activityPresence = new Activity()
                    {
                        State = "In Main Menu",
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InLobby:
                    activityPresence = new Activity()
                    {
                        State = string.IsNullOrEmpty(state) ? "Waiting In Lobby" : state,
                        Details = details,
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Party =
                        {
                            Id = lobbyID,
                            Size =
                            {
                                CurrentSize = currentPlayers,
                                MaxSize = maxPlayers,
                            },
                        },
                        Secrets =
                        {
                            Join = lobbyID + "_join",
                        },
                        Instance = true,
                    };
                    break;
                case Activities.InMatch:
                    activityPresence = new Activity()
                    {
                        State = string.IsNullOrEmpty(state) ? "Playing" : state,
                        Details = details,
                        Timestamps =
                        {
                            Start = startSessionTime,
                        },
                        Assets =
                        {
                            LargeImage = "rfimg_1_",
                            LargeText = "RavenM",
                        },
                        Party =
                        {
                            Id = lobbyID,
                            Size =
                            {
                                CurrentSize = currentPlayers,
                                MaxSize = maxPlayers,
                            },
                        },
                        Secrets =
                        {
                            Join = lobbyID == "None" ? string.Empty : lobbyID + "_join",
                        },
                        Instance = true,
                    };
                    break;
            }

            activityManager.UpdateActivity(activityPresence, result =>
            {
                if (result == Result.NotRunning)
                {
                    _pendingDisconnectReason = $"UpdateActivity failed: {result}";
                    return;
                }

                if (result != Result.Ok)
                    LogDiscordStatus($"Update Discord Activity failed: {result}");
            });
        }

        public enum Activities
        {
            InMenu,
            InLobby,
            InMatch,
        }
    }
}
