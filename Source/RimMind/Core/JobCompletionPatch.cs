using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace RimMind.Core
{
    /// <summary>
    /// Harmony patch on JobDriver.EndJobWith.
    /// When a player-forced job completes successfully, fires the next pending callback for that pawn.
    /// This allows instant state changes (draft, undraft) to be deferred until after jobs complete.
    /// </summary>
    public static class JobCompletionPatch
    {
        public static void Apply(Harmony harmony)
        {
            var target = typeof(JobDriver).GetMethod(
                "EndJobWith",
                BindingFlags.Public | BindingFlags.Instance);

            if (target == null)
            {
                Log.Error("[RimMind] Could not find JobDriver.EndJobWith to patch!");
                return;
            }

            var postfix = typeof(JobCompletionPatch).GetMethod(
                "Postfix",
                BindingFlags.Static | BindingFlags.Public);

            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            Log.Message("[RimMind] Patched JobDriver.EndJobWith for callback queue");
        }

        public static void Postfix(JobDriver __instance, JobCondition condition)
        {
            try
            {
                // Only fire on successful completion of player-issued jobs
                if (condition != JobCondition.Succeeded) return;
                if (__instance?.job == null || !__instance.job.playerForced) return;

                var pawn = __instance.pawn;
                if (pawn == null || pawn.Destroyed) return;

                if (PendingCallbackQueue.HasPending(pawn))
                {
                    // Check if there are more queued jobs before firing — if so, wait for all to finish
                    if (pawn.jobs.jobQueue.Count > 0) return;
                    PendingCallbackQueue.FireNext(pawn);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[RimMind] JobCompletionPatch failed: " + ex);
            }
        }
    }
}
