using System.Diagnostics;
using System.IO;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace RavenM
{
    /// <summary>
    /// Sent by the host when a match ends so all clients return to the lobby/map selection screen.
    /// </summary>
    public class MatchEndPacket
    {
        /// <summary>
        /// The team that won the match, or -1 if no winner data is available.
        /// </summary>
        public int WinningTeam = -1;
    }

    /// <summary>
    /// Hook GameManager.OnGameEnded to broadcast a match-end RPC and redirect everyone back to the lobby.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "OnGameEnded")]
    public class GameManagerOnGameEndedPatch
    {
        static void Postfix()
        {
            if (!IngameNetManager.instance.IsClient || !IngameNetManager.instance.IsHost)
                return;

            // If OnWin triggered first, let it handle the redirect.
            if (IsInStack("OnWin"))
                return;

            IngameNetManager.instance.PerformMatchEndRedirect(true);
        }

        static bool IsInStack(string name)
        {
            var trace = new StackTrace();
            foreach (var frame in trace.GetFrames())
            {
                if (frame.GetMethod().Name == name)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Hook GameManager.OnWin as a fallback match-end trigger.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "OnWin")]
    public class GameManagerOnWinPatch
    {
        static void Postfix(int winner, bool continueNeverendingBattle)
        {
            if (!IngameNetManager.instance.IsClient || !IngameNetManager.instance.IsHost)
                return;

            // If this OnWin was called from OnGameEnded, OnGameEnded will handle the redirect.
            if (IsInStack("OnGameEnded"))
                return;

            IngameNetManager.instance.PerformMatchEndRedirect(true, winner);
        }

        static bool IsInStack(string name)
        {
            var trace = new StackTrace();
            foreach (var frame in trace.GetFrames())
            {
                if (frame.GetMethod().Name == name)
                    return true;
            }
            return false;
        }
    }
}
