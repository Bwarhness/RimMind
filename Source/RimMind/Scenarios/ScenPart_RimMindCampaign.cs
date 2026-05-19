using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Scenarios
{
    /// <summary>
    /// The custom ScenPart that hosts the AI-authored scenario.
    /// Hosts the prompt UI on the scenario customization page, calls the LLM,
    /// and applies the resulting plan to starting pawns and starting items.
    /// </summary>
    public class ScenPart_RimMindCampaign : ScenPart
    {
        // Persisted: the AI's response (user prompt + campaign + pawn/item specs).
        public RimMindScenarioPlan plan;

        // Transient UI state (not saved).
        private string editingPrompt = "";
        private bool isGenerating;
        private Vector2 reviewScroll;
        private string statusMessage = "";

        // Cross-thread handoff for the AI callback.
        private readonly object resultLock = new object();
        private RimMindScenarioPlan pendingPlan;
        private bool resultReady;
        private bool resultFailed;

        // Wrapping cursor for applying pawn specs to player starters.
        // Reset by SyncCursorToSlots() when GameInitData is available.
        private int pawnSpecCursor;

        public override string Summary(Scenario scen)
        {
            if (plan == null || string.IsNullOrWhiteSpace(plan.UserPrompt))
                return "RimMind_Scenario_SummaryEmpty".Translate();
            string pitch = plan.UserPrompt.Trim();
            if (pitch.Length > 140) pitch = pitch.Substring(0, 137) + "...";
            return "RimMind_Scenario_Summary".Translate(pitch);
        }

        public override void DoEditInterface(Listing_ScenEdit listing)
        {
            DrainPendingResult();
            bool hasPlan = plan != null && plan.HasContent;
            float height = hasPlan ? 380f : 220f;
            Rect rect = listing.GetScenPartRect(this, height);

            var titleRect = new Rect(rect.x, rect.y, rect.width, 24f);
            Widgets.Label(titleRect, "<b>" + "RimMind_Scenario_PartTitle".Translate() + "</b>");

            var body = new Rect(rect.x, rect.y + 26f, rect.width, rect.height - 26f);
            if (!hasPlan)
                DrawPromptStage(body);
            else
                DrawReviewStage(body);
        }

        private void DrawPromptStage(Rect body)
        {
            var lbl = new Rect(body.x, body.y, body.width, 20f);
            Widgets.Label(lbl, "RimMind_Scenario_PromptLabel".Translate());

            var inputRect = new Rect(body.x, body.y + 22f, body.width, 110f);
            editingPrompt = Widgets.TextArea(inputRect, editingPrompt ?? "");

            var hintRect = new Rect(body.x, body.y + 136f, body.width, 18f);
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(hintRect, "RimMind_Scenario_PromptHint".Translate());
            GUI.color = Color.white;

            var btnRect = new Rect(body.x, body.y + 158f, 240f, 28f);
            if (isGenerating)
            {
                GUI.color = Color.yellow;
                Widgets.Label(btnRect, "RimMind_Scenario_Generating".Translate());
                GUI.color = Color.white;
            }
            else
            {
                bool canGen = !string.IsNullOrWhiteSpace(editingPrompt) && editingPrompt.Trim().Length >= 10;
                if (!canGen) GUI.color = new Color(0.6f, 0.6f, 0.6f);
                if (Widgets.ButtonText(btnRect, "RimMind_Scenario_Generate".Translate()) && canGen)
                    StartGenerate();
                GUI.color = Color.white;
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                var statusRect = new Rect(body.x + 250f, body.y + 158f, body.width - 250f, 28f);
                GUI.color = new Color(1f, 0.45f, 0.45f);
                Widgets.Label(statusRect, statusMessage);
                GUI.color = Color.white;
            }
        }

        private void DrawReviewStage(Rect body)
        {
            float topBar = 28f;
            var topRect = new Rect(body.x, body.y, body.width, topBar);
            var regenRect = new Rect(topRect.x, topRect.y, 200f, topBar);
            var resetRect = new Rect(topRect.x + 210f, topRect.y, 200f, topBar);
            if (isGenerating)
            {
                GUI.color = Color.yellow;
                Widgets.Label(regenRect, "RimMind_Scenario_Generating".Translate());
                GUI.color = Color.white;
            }
            else
            {
                if (Widgets.ButtonText(regenRect, "RimMind_Scenario_Regenerate".Translate()))
                    StartGenerate();
                if (Widgets.ButtonText(resetRect, "RimMind_Scenario_ClearPlan".Translate()))
                {
                    plan = null;
                    statusMessage = "";
                    return;
                }
            }

            var scroll = new Rect(body.x, body.y + topBar + 6f, body.width, body.height - topBar - 6f);
            float lineH = 20f;
            float viewH = 80f
                + plan.Pawns.Count * lineH * 3f
                + plan.Items.Count * lineH
                + 60f;
            var view = new Rect(0f, 0f, scroll.width - 20f, viewH);
            Widgets.BeginScrollView(scroll, ref reviewScroll, view);

            float y = 0f;
            Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_PitchHeading".Translate() + "</b>");
            y += lineH;
            Widgets.Label(new Rect(0, y, view.width, lineH * 2f), TruncSafe(plan.UserPrompt, 200));
            y += lineH * 2f + 4f;

            if (plan.Campaign != null)
            {
                Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_FrameHeading".Translate() + "</b>");
                y += lineH;
                if (!string.IsNullOrEmpty(plan.Campaign.Setting))
                {
                    Widgets.Label(new Rect(0, y, view.width, lineH), "  " + plan.Campaign.Setting);
                    y += lineH;
                }
                if (!string.IsNullOrEmpty(plan.Campaign.CurrentAct))
                {
                    Widgets.Label(new Rect(0, y, view.width, lineH), "  " + plan.Campaign.CurrentAct);
                    y += lineH;
                }
            }

            if (plan.Pawns.Count > 0)
            {
                y += 4f;
                Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_PawnsHeading".Translate(plan.Pawns.Count) + "</b>");
                y += lineH;
                foreach (var p in plan.Pawns)
                {
                    var name = $"{p.FirstName} \"{p.NickName}\" {p.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(p.NickName)) name = $"{p.FirstName} {p.LastName}".Trim();
                    string traits = p.Traits != null && p.Traits.Count > 0 ? string.Join(", ", p.Traits) : "(rolled)";
                    Widgets.Label(new Rect(0, y, view.width, lineH), $"  {name} — {traits}");
                    y += lineH;
                    if (!string.IsNullOrEmpty(p.Narrative))
                    {
                        GUI.color = new Color(0.75f, 0.75f, 0.75f);
                        Widgets.Label(new Rect(12, y, view.width - 12, lineH * 2f), TruncSafe(p.Narrative, 160));
                        GUI.color = Color.white;
                        y += lineH * 2f;
                    }
                }
            }

            if (plan.Items.Count > 0)
            {
                y += 4f;
                Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_ItemsHeading".Translate(plan.Items.Count) + "</b>");
                y += lineH;
                foreach (var it in plan.Items)
                {
                    Widgets.Label(new Rect(0, y, view.width, lineH), $"  {it.Count}× {it.DefName}");
                    y += lineH;
                }
            }

            if (!string.IsNullOrEmpty(plan.BiomeHint) || !string.IsNullOrEmpty(plan.SeasonHint))
            {
                y += 4f;
                Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_HintsHeading".Translate() + "</b>");
                y += lineH;
                if (!string.IsNullOrEmpty(plan.BiomeHint))
                {
                    Widgets.Label(new Rect(0, y, view.width, lineH), "  " + "RimMind_Scenario_BiomeHint".Translate(plan.BiomeHint));
                    y += lineH;
                }
                if (!string.IsNullOrEmpty(plan.SeasonHint))
                {
                    Widgets.Label(new Rect(0, y, view.width, lineH), "  " + "RimMind_Scenario_SeasonHint".Translate(plan.SeasonHint));
                    y += lineH;
                }
            }

            Widgets.EndScrollView();
        }

        private void StartGenerate()
        {
            isGenerating = true;
            statusMessage = "";
            pawnSpecCursor = 0;
            lock (resultLock)
            {
                pendingPlan = null;
                resultReady = false;
                resultFailed = false;
            }
            string themeId = RimMindMod.Settings?.selectedTheme ?? "chronicle";
            ScenarioPlanGenerator.Generate(editingPrompt, themeId, p =>
            {
                lock (resultLock)
                {
                    pendingPlan = p;
                    resultFailed = (p == null || !p.HasContent);
                    resultReady = true;
                }
            });
        }

        private void DrainPendingResult()
        {
            RimMindScenarioPlan p = null;
            bool failed = false;
            bool ready = false;
            lock (resultLock)
            {
                if (resultReady)
                {
                    p = pendingPlan;
                    failed = resultFailed;
                    ready = true;
                    pendingPlan = null;
                    resultReady = false;
                    resultFailed = false;
                }
            }
            if (!ready) return;
            isGenerating = false;
            if (failed || p == null)
            {
                statusMessage = "RimMind_Scenario_GenerateFailed".Translate();
                return;
            }
            plan = p;
            statusMessage = "";
        }

        public override void Notify_NewPawnGenerating(Pawn pawn, PawnGenerationContext context)
        {
            base.Notify_NewPawnGenerating(pawn, context);
            if (context != PawnGenerationContext.PlayerStarter) return;
            if (plan?.Pawns == null || plan.Pawns.Count == 0) return;
            if (pawn == null) return;

            int idx = -1;
            var slots = Find.GameInitData?.startingAndOptionalPawns;
            if (slots != null)
                idx = slots.IndexOf(pawn);
            if (idx < 0)
                idx = pawnSpecCursor++ % plan.Pawns.Count;
            if (idx >= plan.Pawns.Count) return;

            try { ApplySpecToPawn(pawn, plan.Pawns[idx]); }
            catch (Exception ex)
            {
                Log.Warning($"[RimMind] ApplySpecToPawn failed for slot {idx}: {ex.Message}");
            }
        }

        private void ApplySpecToPawn(Pawn pawn, PawnSpec spec)
        {
            if (pawn == null || spec == null) return;

            // Name (only if any name fields were provided)
            if (!string.IsNullOrWhiteSpace(spec.FirstName) || !string.IsNullOrWhiteSpace(spec.LastName))
            {
                string first = spec.FirstName ?? pawn.Name?.ToStringShort ?? "Colonist";
                string last = spec.LastName ?? "";
                string nick = string.IsNullOrWhiteSpace(spec.NickName) ? first : spec.NickName;
                pawn.Name = new NameTriple(first, nick, last);
            }

            // Traits
            if (spec.Traits != null && spec.Traits.Count > 0 && pawn.story?.traits != null)
            {
                pawn.story.traits.allTraits.Clear();
                foreach (var traitName in spec.Traits)
                {
                    if (string.IsNullOrWhiteSpace(traitName)) continue;
                    var def = DefDatabase<TraitDef>.GetNamedSilentFail(traitName);
                    if (def == null)
                    {
                        // Fuzzy: try matching by label case-insensitive
                        def = DefDatabase<TraitDef>.AllDefsListForReading
                            .FirstOrDefault(d => string.Equals(d.defName, traitName, StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(d.label, traitName, StringComparison.OrdinalIgnoreCase));
                    }
                    if (def == null) continue;
                    if (pawn.story.traits.HasTrait(def)) continue;
                    pawn.story.traits.GainTrait(new Trait(def, 0, false));
                }
            }

            // Skills + passions
            if (spec.Skills != null && spec.Skills.Count > 0 && pawn.skills != null)
            {
                foreach (var kv in spec.Skills)
                {
                    var sdef = ResolveSkill(kv.Key);
                    if (sdef == null) continue;
                    var rec = pawn.skills.GetSkill(sdef);
                    if (rec == null) continue;
                    rec.Level = Mathf.Clamp(kv.Value, 0, 20);
                }
            }
            if (spec.Passions != null && spec.Passions.Count > 0 && pawn.skills != null)
            {
                foreach (var kv in spec.Passions)
                {
                    var sdef = ResolveSkill(kv.Key);
                    if (sdef == null) continue;
                    var rec = pawn.skills.GetSkill(sdef);
                    if (rec == null) continue;
                    rec.passion = ParsePassion(kv.Value);
                }
            }
        }

        private static SkillDef ResolveSkill(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var def = DefDatabase<SkillDef>.GetNamedSilentFail(name);
            if (def != null) return def;
            return DefDatabase<SkillDef>.AllDefsListForReading
                .FirstOrDefault(d => string.Equals(d.defName, name, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(d.label, name, StringComparison.OrdinalIgnoreCase));
        }

        private static Passion ParsePassion(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return Passion.None;
            if (string.Equals(s, "Major", StringComparison.OrdinalIgnoreCase)) return Passion.Major;
            if (string.Equals(s, "Minor", StringComparison.OrdinalIgnoreCase)) return Passion.Minor;
            return Passion.None;
        }

        public override IEnumerable<Thing> PlayerStartingThings()
        {
            if (plan?.Items == null) yield break;
            foreach (var spec in plan.Items)
            {
                if (string.IsNullOrWhiteSpace(spec.DefName)) continue;
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(spec.DefName);
                if (def == null) continue;

                Thing thing;
                ThingDef stuff = null;
                if (def.MadeFromStuff)
                {
                    if (!string.IsNullOrEmpty(spec.Stuff))
                        stuff = DefDatabase<ThingDef>.GetNamedSilentFail(spec.Stuff);
                    if (stuff == null || !stuff.IsStuff || !stuff.stuffProps.CanMake(def))
                        stuff = GenStuff.DefaultStuffFor(def);
                }
                thing = stuff != null ? ThingMaker.MakeThing(def, stuff) : ThingMaker.MakeThing(def);

                if (thing != null && thing.def.stackLimit > 1)
                    thing.stackCount = Mathf.Clamp(spec.Count, 1, thing.def.stackLimit);
                if (thing != null)
                    yield return thing;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref plan, "plan");
        }

        private static string TruncSafe(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
        }
    }
}
