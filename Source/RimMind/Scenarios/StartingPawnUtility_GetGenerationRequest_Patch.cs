using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimMind.Scenarios
{
    /// <summary>
    /// Forces the AI's xenotype onto each player-starter pawn at generation time.
    ///
    /// We patch PawnGenerator.GeneratePawn(PawnGenerationRequest) as a Prefix because:
    ///   - It's the choke point that EVERY pawn generation flows through, regardless of
    ///     which scenario / page / scen-part triggered it. Earlier we tried
    ///     StartingPawnUtility.GetGenerationRequest but in RimWorld 1.6 the configure
    ///     pawns page uses ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes which
    ///     does its own request building — so GetGenerationRequest wasn't on the path.
    ///   - PawnGenerator.GeneratePawn IS on every path.
    ///
    /// Slot index is inferred from Find.GameInitData.startingAndOptionalPawns.Count:
    /// during initial generation it grows from 0 upward, so the next pawn's index
    /// matches the current count. For per-pawn re-randomize the list count stays
    /// the same so the index is ambiguous; we fall back to a wrap-around cursor.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class PawnGenerator_GeneratePawn_Patch
    {
        private static int wrapCursor;

        public static void Prefix(ref PawnGenerationRequest request)
        {
            try
            {
                if (request.Context != PawnGenerationContext.PlayerStarter) return;

                var scen = Find.Scenario;
                if (scen == null) return;
                var part = scen.AllParts.OfType<ScenPart_RimMindCampaign>().FirstOrDefault();
                if (part?.plan?.Pawns == null || part.plan.Pawns.Count == 0) return;

                int slot;
                var initData = Find.GameInitData;
                if (initData?.startingAndOptionalPawns != null && initData.startingAndOptionalPawns.Count < part.plan.Pawns.Count)
                {
                    slot = initData.startingAndOptionalPawns.Count;
                }
                else
                {
                    slot = wrapCursor++ % part.plan.Pawns.Count;
                }

                if (slot < 0 || slot >= part.plan.Pawns.Count) return;
                var spec = part.plan.Pawns[slot];
                if (spec == null || string.IsNullOrWhiteSpace(spec.Xenotype)) return;

                bool hasCustomGenes = spec.XenotypeGenes != null && spec.XenotypeGenes.Count > 0;
                if (hasCustomGenes)
                {
                    var custom = part.GetSavedCustomXenotype(spec.Xenotype);
                    if (custom != null)
                    {
                        request.ForcedCustomXenotype = custom;
                        request.AllowedXenotypes = null;
                        request.ForcedXenotype = null;
                    }
                    return;
                }

                var def = DefDatabase<XenotypeDef>.GetNamedSilentFail(spec.Xenotype)
                       ?? DefDatabase<XenotypeDef>.AllDefsListForReading
                            .FirstOrDefault(d => string.Equals(d.defName, spec.Xenotype, StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(d.label, spec.Xenotype, StringComparison.OrdinalIgnoreCase));
                if (def == null) return;

                request.ForcedXenotype = def;
                request.ForcedCustomXenotype = null;
                request.AllowedXenotypes = null;
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] GeneratePawn prefix failed: " + ex.Message);
            }
        }
    }
}
