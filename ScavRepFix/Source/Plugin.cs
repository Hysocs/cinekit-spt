using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ScavRepFix
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.SPT.singleplayer", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.hysocs.scavrepfix";
        public const string PluginName = "Hysocs-ScavRepFix";
        public const string PluginVersion = "1.0.0";

        private const string StatisticsManagerType =
            "EFT.BaseStatisticsManager";
        private const string FaultingPatchType =
            "SPT.SinglePlayer.Patches.ScavMode.ScavRepAdjustmentPatch";
        private const string FaultingPatchMethod = "PatchPrefix";

        private static ManualLogSource _log;
        private Harmony _harmony;
        private static bool _reportedSuppression;

        private void Awake()
        {
            _log = Logger;

            Type statisticsManager =
                AccessTools.TypeByName(StatisticsManagerType);
            MethodInfo onEnemyKill = statisticsManager == null
                ? null
                : AccessTools.Method(statisticsManager, "OnEnemyKill");
            Type sptPatch = AccessTools.TypeByName(FaultingPatchType);
            MethodInfo patchPrefix = sptPatch == null
                ? null
                : AccessTools.Method(sptPatch, FaultingPatchMethod);

            if (onEnemyKill == null || patchPrefix == null)
            {
                Logger.LogError(
                    "Compatible SPT scav-reputation methods were not found. " +
                    "The guard was not installed.");
                return;
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.Patch(
                onEnemyKill,
                finalizer: new HarmonyMethod(
                    AccessTools.Method(
                        typeof(Plugin), nameof(GuardSptScavRepError))));

            Logger.LogInfo(
                "Scav reputation null-reference guard installed.");
        }

        private static Exception GuardSptScavRepError(Exception __exception)
        {
            if (__exception == null)
                return null;

            if (!IsKnownSptScavRepNullReference(__exception))
                return __exception;

            if (!_reportedSuppression)
            {
                _reportedSuppression = true;
                _log?.LogWarning(
                    "Suppressed the known SPT ScavRepAdjustmentPatch " +
                    "NullReferenceException. Further occurrences in this " +
                    "session will be suppressed silently.");
            }

            return null;
        }

        private static bool IsKnownSptScavRepNullReference(
            Exception exception)
        {
            if (!(exception is NullReferenceException))
                return false;

            string trace = exception.StackTrace;
            return !string.IsNullOrEmpty(trace) &&
                   trace.IndexOf(
                       FaultingPatchType + "." + FaultingPatchMethod,
                       StringComparison.Ordinal) >= 0;
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
