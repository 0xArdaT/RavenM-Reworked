using HarmonyLib;

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
    /// Hook VictoryUi.EndGame — the single official entry point for the victory/defeat banner,
    /// called only by game modes (BattleMode.Win, DominationMode.Win, PointMatch, etc.) when the
    /// match has officially concluded. This fires even when GameManager.gameOver is never set
    /// (e.g. neverending battles or spectating), which otherwise leaves everyone stuck in the map.
    /// </summary>
    [HarmonyPatch(typeof(VictoryUi), "EndGame")]
    public class VictoryUiEndGamePatch
    {
        static void Postfix(int __0)
        {
            if (!IngameNetManager.instance.IsClient || !IngameNetManager.instance.IsHost)
                return;

            if (GameManager.instance == null || !GameManager.instance.ingame)
                return;

            Plugin.logger.LogInfo($"VictoryUi.EndGame fired (winner: {__0}); scheduling match end redirect.");
            IngameNetManager.instance.ScheduleMatchEndRedirect(true, __0);
        }
    }

    /// <summary>
    /// Hook GameManager.OnWin as a fallback match-end trigger for end paths that bypass VictoryUi.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "OnWin")]
    public class GameManagerOnWinPatch
    {
        static void Postfix(int winner)
        {
            if (!IngameNetManager.instance.IsClient || !IngameNetManager.instance.IsHost)
                return;

            if (GameManager.instance == null || !GameManager.instance.ingame)
                return;

            IngameNetManager.instance.ScheduleMatchEndRedirect(true, winner);
        }
    }
}
