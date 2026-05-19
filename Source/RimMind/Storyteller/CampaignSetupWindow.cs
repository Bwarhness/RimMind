using System;
using RimMind.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller
{
    /// <summary>
    /// Pre-game UI for hybrid campaign creation.
    /// User gives initial input; AI generates draft campaign frame.
    /// User edits draft or regenerates, then locks before first pawn lands.
    /// </summary>
    public class CampaignSetupWindow : Window
    {
        private string userPrompt = "";
        private bool isGenerating = false;
        private CampaignFrame draftFrame;
        private string statusMessage = "";
        private Vector2 scrollPosition;

        private const float INPUT_HEIGHT = 120f;
        private const float BUTTON_HEIGHT = 40f;
        private const float PADDING = 16f;

        public override Vector2 InitialSize => new Vector2(640f, 720f);

        public CampaignSetupWindow()
        {
            closeOnClickedOutside = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("<b>" + "RimMind AI Storyteller — Campaign Setup".Colorize(Color.cyan) + "</b>");
            listing.Gap(8f);

            if (draftFrame == null)
            {
                // Input stage
                listing.Label("Describe the story you want to experience:");
                listing.Gap(4f);

                var inputRect = listing.GetRect(INPUT_HEIGHT);
                GUI.SetNextControlName("CampaignPrompt");
                userPrompt = GUI.TextArea(inputRect, userPrompt);

                listing.Gap(8f);
                listing.Label("Examples: \"A gritty political intrigue on a desert planet\" or \"A horror story where the colony slowly goes mad\"");
                listing.Gap(16f);

                if (isGenerating)
                {
                    listing.Label("Generating campaign frame...".Colorize(Color.yellow));
                }
                else
                {
                    bool canGenerate = !string.IsNullOrWhiteSpace(userPrompt) && userPrompt.Length >= 10;
                    if (canGenerate && listing.ButtonText("Generate Campaign Frame"))
                    {
                        GenerateFrame();
                    }
                }

                if (!string.IsNullOrEmpty(statusMessage))
                {
                    listing.Gap(8f);
                    listing.Label(statusMessage.Colorize(Color.red));
                }

                listing.Gap(16f);
                if (listing.ButtonText("Skip AI Storyteller (Classic Mode)"))
                {
                    // Disable storyteller and close
                    RimMindMod.Settings.storytellerEnabled = false;
                    Close();
                }
            }
            else
            {
                // Review/edit stage
                listing.Label("<b>" + "Campaign Frame Draft".Colorize(Color.cyan) + "</b>");
                listing.Gap(8f);

                var scrollRect = listing.GetRect(inRect.height - BUTTON_HEIGHT * 4 - PADDING * 4 - listing.CurHeight);
                var viewRect = new Rect(0f, 0f, scrollRect.width - 20f, 500f);

                scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);
                var innerListing = new Listing_Standard();
                innerListing.Begin(viewRect);

                EditField(innerListing, "Setting", ref draftFrame.Setting);
                EditField(innerListing, "Inciting Incident", ref draftFrame.IncitingIncident);
                EditField(innerListing, "Current Act", ref draftFrame.CurrentAct);
                EditField(innerListing, "Pending Threat", ref draftFrame.PendingThreat);
                EditField(innerListing, "Opportunity", ref draftFrame.Opportunity);

                innerListing.Label("Active Forces:");
                for (int i = 0; i < draftFrame.ActiveForces.Count; i++)
                {
                    var force = draftFrame.ActiveForces[i];
                    var rowRect = innerListing.GetRect(24f);
                    force = GUI.TextField(rowRect, force);
                    draftFrame.ActiveForces[i] = force;
                }
                if (innerListing.ButtonText("Add Force"))
                {
                    draftFrame.ActiveForces.Add("New force");
                }

                innerListing.Gap(12f);
                innerListing.Label("Planted Seeds (narrative hooks):");
                for (int i = 0; i < draftFrame.PlantedSeeds.Count; i++)
                {
                    var seed = draftFrame.PlantedSeeds[i];
                    var rowRect = innerListing.GetRect(48f);
                    var leftRect = new Rect(rowRect.x, rowRect.y, rowRect.width * 0.7f, rowRect.height);
                    var rightRect = new Rect(rowRect.x + rowRect.width * 0.72f, rowRect.y, rowRect.width * 0.28f, rowRect.height);

                    seed.Description = GUI.TextArea(leftRect, seed.Description);
                    if (GUI.Button(rightRect, "Remove"))
                    {
                        draftFrame.PlantedSeeds.RemoveAt(i);
                        i--;
                    }
                }
                if (innerListing.ButtonText("Add Seed"))
                {
                    draftFrame.PlantedSeeds.Add(new NarrativeSeed(
                        $"seed_{draftFrame.PlantedSeeds.Count}",
                        "A mystery yet to unfold",
                        "",
                        0
                    ));
                }

                innerListing.End();
                GUI.EndScrollView();

                listing.Gap(12f);

                if (isGenerating)
                {
                    listing.Label("Regenerating...".Colorize(Color.yellow));
                }
                else
                {
                    if (listing.ButtonText("Regenerate Frame"))
                    {
                        GenerateFrame();
                    }
                }

                if (listing.ButtonText("Lock Frame & Begin Story"))
                {
                    LockAndBegin();
                }
            }

            listing.End();
        }

        private void EditField(Listing_Standard listing, string label, ref string value)
        {
            listing.Label($"<b>{label}:</b>");
            value = listing.TextEntry(value);
            listing.Gap(4f);
        }

        private void GenerateFrame()
        {
            if (string.IsNullOrWhiteSpace(userPrompt)) return;

            isGenerating = true;
            statusMessage = "";

            var engine = NarrativeEngine.Instance;
            if (engine == null)
            {
                // Create a temporary engine or fallback
                statusMessage = "Narrative engine not available. Is the storyteller enabled in mod settings?";
                isGenerating = false;
                return;
            }

            engine.GenerateCampaignFrame(userPrompt, frame =>
            {
                isGenerating = false;
                if (frame == null)
                {
                    statusMessage = "Failed to generate campaign frame. Check your API settings and try again.";
                    return;
                }

                draftFrame = frame;
                statusMessage = "";
            });
        }

        private void LockAndBegin()
        {
            if (draftFrame == null) return;

            var engine = NarrativeEngine.Instance;
            if (engine != null)
            {
                engine.SetCampaignFrame(draftFrame);
                var map = Find.CurrentMap ?? Find.Maps.FirstOrDefault(m => m.IsPlayerHome);
                int day = map != null ? GenLocalDate.DayOfYear(map) : 0;
                engine.LockCampaignFrame(day);
            }

            Close();
        }
    }
}
