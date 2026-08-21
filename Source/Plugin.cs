using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace RavenM
{
    /// <summary>
    /// Disable mods that are NOT workshop mods.
    /// </summary>
    [HarmonyPatch(typeof(ModManager), nameof(ModManager.OnGameManagerStart))]
    public class NoCustommodsPatch
    {
        static bool Prefix(ModManager __instance)
        {
            string path = "NOT_REAL";
            if (Plugin.addToBuiltInMutators)
            {
                path = Plugin.customBuildInMutators;
                __instance.noContentMods = false;
                __instance.noWorkshopMods = true;
            }
            __instance.modStagingPathOverride = path;
            typeof(MapEditor.MapDescriptor).GetField("DATA_PATH", BindingFlags.Static | BindingFlags.Public).SetValue(null, path);
            return true;
        }
    }

    public class GuidComponent : MonoBehaviour
    {
        public int guid; //TODO: Replace with System.GUID?
    }

    public sealed class RavenLogger
    {
        readonly BepInEx.Logging.ManualLogSource _inner;

        public RavenLogger(BepInEx.Logging.ManualLogSource inner)
        {
            _inner = inner;
        }

        public void LogInfo(object data)
        {
            if (Plugin.VerboseLogging)
                _inner.LogInfo(data);
        }

        public void LogDebug(object data)
        {
            if (Plugin.VerboseLogging)
                _inner.LogDebug(data);
        }

        public void LogMessage(object data)
        {
            if (Plugin.VerboseLogging)
                _inner.LogMessage(data);
        }

        public void LogWarning(object data) => _inner.LogWarning(data);

        public void LogError(object data) => _inner.LogError(data);
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("RavenM.Updater", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        public bool FirstSteamworksInit = false;

        public static Plugin instance = null;

        public static RavenLogger logger = null;

        public static bool VerboseLogging = false;

        public static bool changeGUID = false;

        public static bool addToBuiltInMutators = false;
        public static string customBuildInMutators;
        public static List<string> customMutatorsDirectories = new List<string>();

        public static bool JoinedLobbyFromArgument = false;
        public static Dictionary<string, string> Arguments = new Dictionary<string, string>();

        public static string BuildGUID
        {
            get
            {
                if (!changeGUID)
                {
                    return "RavenM-Reworked-v1.2-EA38";
                }
                else
                {
                    return "WARNING-TESTING-MODE-89a27d9e2fcb";
                }
            }
        }

        public static readonly int EXPECTED_BUILD_NUMBER = 38;

        private ConfigEntry<bool> configRavenMDevMod;
        private ConfigEntry<bool> configRavenMAddToBuiltInMutators;
        private ConfigEntry<string> configRavenMBuiltInMutatorsDirectory;
        private ConfigEntry<bool> configVerboseLogging;
        private void Awake()
        {
            instance = this;

            string[] commandLineArgs = Environment.GetCommandLineArgs();

            for (int i = 0; i < commandLineArgs.Length; i++)
            {
                if (commandLineArgs[i] == "-noravenm") 
                { 
                    Logger.LogWarning($"Plugin {PluginInfo.PLUGIN_GUID} is canceled to load!");
                    throw new Exception("Cancel load");
                }
            }

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            configRavenMDevMod = Config.Bind("General.Toggles",
                                                "Enable Dev Mode",
                                                false,
                                                "Change GUID to WARNING-TESTING-MODE-89a27d9e2fcb");
            configRavenMAddToBuiltInMutators = Config.Bind("General.Toggles",
                "Enable Custom Build In Mutators",
                false,
                "Add Directory in General.BuildInMutators");
            configVerboseLogging = Config.Bind("General.Toggles",
                "Enable Verbose Logging",
                false,
                "Log packet, prefab and gameplay debug details. Keep this off to avoid large log files and extra overhead.");

            configRavenMBuiltInMutatorsDirectory = Config.Bind("General.BuildInMutators",
                                                                "Directory",
                                                                "",
                                                                "The mutators in the folder will be added automatically as Build In Mutators, this is for testing mutators without having to start the game with mods.");

            VerboseLogging = configVerboseLogging.Value;
            logger = new RavenLogger(Logger);

            changeGUID = configRavenMDevMod.Value;
            addToBuiltInMutators = configRavenMAddToBuiltInMutators.Value;
            customBuildInMutators = configRavenMBuiltInMutatorsDirectory.Value;
            if (addToBuiltInMutators && System.IO.Directory.Exists(customBuildInMutators))
            {
                Logger.LogInfo("Added Custom Build In Mutator Directory " + customBuildInMutators);
            }
            else if (addToBuiltInMutators)
            {
                Logger.LogError($"Directory {customBuildInMutators} could not be found.");
                customBuildInMutators = "NOT_REAL";
            }
            else
            {
                customBuildInMutators = "NOT_REAL";
            }
            var harmony = new Harmony("patch.ravenm");
            try {
                harmony.PatchAll( Assembly.GetAssembly( typeof(LobbySystem) ) );
            } catch (Exception e) {
                Logger.LogError($"Failed to patch: {e}");
            }
            
            string[] args = Environment.GetCommandLineArgs();
            foreach (var argument in args)
            {
                if (argument.Contains("="))
                {
                    string[] argumentVals = argument.Split('=');
                    string argumentName = argumentVals[0];
                    string argumentValue = argumentVals[1];
                    Arguments.Add(argumentName, argumentValue);
                }
                else
                {
                    Arguments.Add(argument, "");
                }
            }
        }
        private void OnGUI()
        {
            GUI.Label(new Rect(10, Screen.height - 20, 420, 40), $"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} (EA38 Compatible)");

            // Game build check is disabled so the mod can run on EA38+ without the compatibility banner.
        }
        public void printConsole(string message)
        {
            Lua.ScriptConsole.instance.LogInfo(message);
        }
        void Update()
        {
            if (!SteamManager.Initialized)
                return;

            SteamAPI.RunCallbacks();
            if (!FirstSteamworksInit)
            {
                FirstSteamworksInit = true;

                var lobbyObject = new GameObject();
                lobbyObject.AddComponent<LobbySystem>();
                DontDestroyOnLoad(lobbyObject);

                var chatObject = new GameObject();
                chatObject.AddComponent<ChatManager>();
                DontDestroyOnLoad(chatObject);

                var netObject = new GameObject();
                netObject.AddComponent<IngameNetManager>();
                DontDestroyOnLoad(netObject);

                var discordObject = new GameObject();
                discordObject.AddComponent<DiscordIntegration>();
                DontDestroyOnLoad(discordObject);
            }
            else if (!JoinedLobbyFromArgument && Arguments.ContainsKey("-ravenm-lobby"))
            {
                JoinLobbyFromArgument();
            }
        }

        void JoinLobbyFromArgument()
        {
            JoinedLobbyFromArgument = true;
            CSteamID lobbyId = new CSteamID(ulong.Parse(Arguments["-ravenm-lobby"]));
            SteamMatchmaking.JoinLobby(lobbyId);
            LobbySystem.instance.InLobby = true;
            LobbySystem.instance.IsLobbyOwner = false;
            LobbySystem.instance.LobbyDataReady = false;
        }
    }
}
