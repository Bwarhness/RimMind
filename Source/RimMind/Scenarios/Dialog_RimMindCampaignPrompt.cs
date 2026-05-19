using System;
using System.Linq;
using RimMind.Core;
using UnityEngine;
using Verse;

namespace RimMind.Scenarios
{
    /// <summary>
    /// Modal window that prompts the user for their story pitch and shows the
    /// AI-generated plan. Opened automatically when the user picks the RimMind
    /// scenario on Page_SelectScenario; the user can also re-open it from the
    /// ScenPart edit page.
    /// </summary>
    public class Dialog_RimMindCampaignPrompt : Window
    {
        private readonly ScenPart_RimMindCampaign scenpart;
        private readonly Action onCommitted;

        private string editingPrompt = "";
        private bool isGenerating;
        private string statusMessage = "";
        private Vector2 reviewScroll;

        private readonly object resultLock = new object();
        private RimMindScenarioPlan pendingPlan;
        private bool resultReady;
        private bool resultFailed;

        public override Vector2 InitialSize => new Vector2(760f, 720f);

        public Dialog_RimMindCampaignPrompt(ScenPart_RimMindCampaign part, Action onCommitted = null)
        {
            this.scenpart = part;
            this.onCommitted = onCommitted;
            this.editingPrompt = part?.plan?.UserPrompt ?? "";

            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            preventCameraMotion = false;
            draggable = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            // Drain MainThreadDispatcher here — pre-game (on the scenario page)
            // Current.Game is null and GameComponentUpdate never fires, so HTTP
            // callbacks marshalled through the dispatcher would otherwise sit
            // forever in the queue.
            MainThreadDispatcher.Drain();

            DrainPendingResult();

            float y = 0f;
            var titleRect = new Rect(0, y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "RimMind_Scenario_DialogTitle".Translate());
            Text.Font = GameFont.Small;
            y += 36f;

            var subRect = new Rect(0, y, rect.width, 22f);
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(subRect, "RimMind_Scenario_DialogSubtitle".Translate());
            GUI.color = Color.white;
            y += 26f;

            bool hasPlan = scenpart?.plan != null && scenpart.plan.HasContent;

            if (!hasPlan)
                DrawPromptStage(new Rect(0, y, rect.width, rect.height - y - 50f));
            else
                DrawReviewStage(new Rect(0, y, rect.width, rect.height - y - 50f));

            // Bottom buttons
            var bottomY = rect.height - 38f;
            var cancelRect = new Rect(0, bottomY, 160f, 32f);
            if (Widgets.ButtonText(cancelRect, "Cancel".Translate()))
            {
                Close();
            }

            if (hasPlan && !isGenerating)
            {
                var commitRect = new Rect(rect.width - 240f, bottomY, 240f, 32f);
                GUI.color = new Color(0.55f, 0.85f, 0.55f);
                if (Widgets.ButtonText(commitRect, "RimMind_Scenario_DialogCommit".Translate()))
                {
                    GUI.color = Color.white;
                    Close();
                    onCommitted?.Invoke();
                    return;
                }
                GUI.color = Color.white;
            }
        }

        private void DrawPromptStage(Rect body)
        {
            float y = body.y;
            Widgets.Label(new Rect(body.x, y, body.width, 22f), "RimMind_Scenario_PromptLabel".Translate());
            y += 24f;

            var inputRect = new Rect(body.x, y, body.width, 180f);
            editingPrompt = Widgets.TextArea(inputRect, editingPrompt ?? "");
            y += 188f;

            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(body.x, y, body.width, 40f), "RimMind_Scenario_PromptHint".Translate());
            GUI.color = Color.white;
            y += 48f;

            var btnRect = new Rect(body.x, y, 280f, 36f);
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
                y += 42f;
                GUI.color = new Color(1f, 0.45f, 0.45f);
                Widgets.Label(new Rect(body.x, y, body.width, 40f), statusMessage);
                GUI.color = Color.white;
            }
        }

        private void DrawReviewStage(Rect body)
        {
            var topRect = new Rect(body.x, body.y, body.width, 32f);
            var regenRect = new Rect(topRect.x, topRect.y, 200f, 32f);
            var clearRect = new Rect(topRect.x + 210f, topRect.y, 200f, 32f);
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
                if (Widgets.ButtonText(clearRect, "RimMind_Scenario_ClearPlan".Translate()))
                {
                    scenpart.plan = null;
                    statusMessage = "";
                    return;
                }
            }

            var scroll = new Rect(body.x, body.y + 38f, body.width, body.height - 38f);
            var plan = scenpart.plan;
            float lineH = 22f;
            float viewH = 100f
                + plan.Pawns.Count * 60f
                + plan.Items.Count * lineH
                + 80f;
            if (plan.Campaign != null) viewH += 80f;
            var view = new Rect(0f, 0f, scroll.width - 20f, viewH);
            Widgets.BeginScrollView(scroll, ref reviewScroll, view);

            float y = 0f;
            Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_PitchHeading".Translate() + "</b>");
            y += lineH;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            Widgets.Label(new Rect(0, y, view.width, lineH * 2f), Trunc(plan.UserPrompt, 240));
            GUI.color = Color.white;
            y += lineH * 2f + 8f;

            if (plan.Campaign != null)
            {
                Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_FrameHeading".Translate() + "</b>");
                y += lineH;
                if (!string.IsNullOrEmpty(plan.Campaign.Setting))
                {
                    Widgets.Label(new Rect(0, y, view.width, lineH * 2f), "  " + plan.Campaign.Setting);
                    y += lineH * 2f;
                }
                y += 8f;
            }

            if (plan.Pawns.Count > 0)
            {
                Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_PawnsHeading".Translate(plan.Pawns.Count) + "</b>");
                y += lineH;
                foreach (var p in plan.Pawns)
                {
                    string name = string.IsNullOrWhiteSpace(p.NickName)
                        ? $"{p.FirstName} {p.LastName}".Trim()
                        : $"{p.FirstName} \"{p.NickName}\" {p.LastName}".Trim();
                    string traits = (p.Traits != null && p.Traits.Count > 0) ? string.Join(", ", p.Traits) : "(rolled)";
                    Widgets.Label(new Rect(0, y, view.width, lineH), $"  {name} — {traits}");
                    y += lineH;
                    if (!string.IsNullOrEmpty(p.Narrative))
                    {
                        GUI.color = new Color(0.75f, 0.75f, 0.75f);
                        Widgets.Label(new Rect(16, y, view.width - 16, lineH * 2f), Trunc(p.Narrative, 200));
                        GUI.color = Color.white;
                        y += lineH * 2f;
                    }
                }
                y += 8f;
            }

            if (plan.Items.Count > 0)
            {
                Widgets.Label(new Rect(0, y, view.width, lineH), "<b>" + "RimMind_Scenario_ItemsHeading".Translate(plan.Items.Count) + "</b>");
                y += lineH;
                foreach (var it in plan.Items)
                {
                    Widgets.Label(new Rect(0, y, view.width, lineH), $"  {it.Count}× {it.DefName}");
                    y += lineH;
                }
                y += 8f;
            }

            if (!string.IsNullOrEmpty(plan.BiomeHint) || !string.IsNullOrEmpty(plan.SeasonHint))
            {
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
            scenpart.plan = p;
            statusMessage = "";
        }

        private static string Trunc(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "...";
        }
    }
}
