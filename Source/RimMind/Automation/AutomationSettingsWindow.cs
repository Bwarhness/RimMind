using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimMind.Languages;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Automation
{
    /// <summary>
    /// Settings window for configuring event automation rules.
    /// </summary>
    public class AutomationSettingsWindow : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private string selectedEventType = null;
        private string editingPrompt = "";
        private int editingCooldown = 60;

        private const float ROW_HEIGHT = 30f;
        private const float CATEGORY_HEIGHT = 35f;

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public AutomationSettingsWindow()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = false;

            // Ensure all known event types have entries
            EnsureDefaultRules();
        }

        private void EnsureDefaultRules()
        {
            foreach (var eventType in DefaultAutomationPrompts.GetKnownEventTypes())
            {
                if (!RimMindMod.Settings.automationRules.ContainsKey(eventType))
                {
                    RimMindMod.Settings.automationRules[eventType] = new AutomationRule
                    {
                        enabled = false,
                        customPrompt = DefaultAutomationPrompts.Get(eventType),
                        cooldownSeconds = 60
                    };
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            var titleRect = new Rect(0f, 0f, inRect.width, 40f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, RimMindTranslations.Get("RimMind_EventAutomation"));
            Text.Font = GameFont.Small;

            // Enable/disable master toggle
            var masterToggleRect = new Rect(0f, 45f, inRect.width, 30f);
            bool wasEnabled = RimMindMod.Settings.enableEventAutomation;
            Widgets.CheckboxLabeled(masterToggleRect, RimMindTranslations.Get("RimMind_EnableEventAutomation"), ref RimMindMod.Settings.enableEventAutomation);
            
            if (wasEnabled != RimMindMod.Settings.enableEventAutomation && RimMindMod.Settings.enableEventAutomation)
            {
                Messages.Message(RimMindTranslations.Get("RimMind_EventAutomationEnabled"), MessageTypeDefOf.NeutralEvent);
            }

            // Description
            var descRect = new Rect(0f, 80f, inRect.width, 60f);
            Widgets.Label(descRect, RimMindTranslations.Get("RimMind_EventAutomationDesc"));

            // Event list area
            var listRect = new Rect(0f, 145f, inRect.width - 320f, inRect.height - 150f);
            DrawEventList(listRect);

            // Editor panel (right side)
            var editorRect = new Rect(inRect.width - 310f, 145f, 310f, inRect.height - 150f);
            DrawEditor(editorRect);
        }

        private void DrawEventList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            
            var innerRect = rect.ContractedBy(5f);
            var viewRect = new Rect(0f, 0f, innerRect.width - 20f, CalculateContentHeight());

            Widgets.BeginScrollView(innerRect, ref scrollPosition, viewRect);

            float curY = 0f;

            // Group rules by category
            var rulesByCategory = RimMindMod.Settings.automationRules
                .GroupBy(kvp => DefaultAutomationPrompts.GetCategory(kvp.Key))
                .OrderBy(g => g.Key);

            foreach (var category in rulesByCategory)
            {
                // Category header
                var categoryRect = new Rect(0f, curY, viewRect.width, CATEGORY_HEIGHT);
                Widgets.DrawBoxSolid(categoryRect, new Color(0.2f, 0.3f, 0.4f, 0.8f));
                Text.Font = GameFont.Medium;
                Widgets.Label(categoryRect.ContractedBy(5f), category.Key);
                Text.Font = GameFont.Small;
                curY += CATEGORY_HEIGHT + 2f;

                // Rules in this category
                foreach (var kvp in category.OrderBy(x => x.Key))
                {
                    DrawRuleRow(new Rect(0f, curY, viewRect.width, ROW_HEIGHT), kvp.Key, kvp.Value);
                    curY += ROW_HEIGHT + 2f;
                }

                curY += 5f; // Extra spacing between categories
            }

            Widgets.EndScrollView();
        }

        private void DrawRuleRow(Rect rect, string eventType, AutomationRule rule)
        {
            bool isSelected = selectedEventType == eventType;

            if (isSelected)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.5f, 0.7f, 0.5f));
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            }

            // Click to select
            if (Widgets.ButtonInvisible(rect))
            {
                selectedEventType = eventType;
                editingPrompt = rule.customPrompt;
                editingCooldown = rule.cooldownSeconds;
            }

            // Enabled checkbox
            var checkboxRect = new Rect(rect.x + 5f, rect.y + 5f, 24f, 24f);
            bool wasEnabled = rule.enabled;
            Widgets.Checkbox(checkboxRect.position, ref rule.enabled);
            
            if (wasEnabled != rule.enabled)
            {
                RimMindMod.Settings.Write();
            }

            // Event type label
            var labelRect = new Rect(rect.x + 35f, rect.y, rect.width - 100f, rect.height);
            string displayName = DefaultAutomationPrompts.GetDisplayName(eventType);
            if (rule.enabled)
            {
                GUI.color = Color.green;
            }
            Widgets.Label(labelRect, displayName);
            GUI.color = Color.white;

            // Status indicator
            var statusRect = new Rect(rect.x + rect.width - 60f, rect.y + 5f, 55f, rect.height - 10f);
            if (rule.enabled && !string.IsNullOrEmpty(rule.customPrompt))
            {
                Widgets.Label(statusRect, RimMindTranslations.Get("RimMind_EventActive"));
            }
            else if (rule.enabled)
            {
                GUI.color = Color.yellow;
                Widgets.Label(statusRect, RimMindTranslations.Get("RimMind_EventNoPrompt"));
                GUI.color = Color.white;
            }
        }

        private void DrawEditor(Rect rect)
        {
            if (string.IsNullOrEmpty(selectedEventType))
            {
                Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
                var hintRect = rect.ContractedBy(10f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(hintRect, RimMindTranslations.Get("RimMind_SelectEventToConfigure"));
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            var innerRect = rect.ContractedBy(10f);

            GUI.BeginGroup(innerRect);
            float w = innerRect.width;
            float h = innerRect.height;
            float curY = 0f;

            // Event type title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, w, 30f), DefaultAutomationPrompts.GetDisplayName(selectedEventType));
            Text.Font = GameFont.Small;
            curY += 35f;

            // Enabled toggle
            var rule = RimMindMod.Settings.automationRules[selectedEventType];
            Widgets.CheckboxLabeled(new Rect(0f, curY, w, 30f), RimMindTranslations.Get("RimMind_Enabled"), ref rule.enabled);
            curY += 35f;

            // Cooldown setting
            Widgets.Label(new Rect(0f, curY, w, 25f), RimMindTranslations.Get("RimMind_Cooldown", editingCooldown.ToString()));
            curY += 25f;

            editingCooldown = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(0f, curY, w, 25f), editingCooldown, 10f, 300f, true, null, "10s", "300s"));
            curY += 30f;

            // Custom prompt label
            Widgets.Label(new Rect(0f, curY, w, 25f), RimMindTranslations.Get("RimMind_CustomPrompt"));
            curY += 25f;

            // Prompt text area
            float promptHeight = h - curY - 80f;
            if (promptHeight > 30f)
            {
                editingPrompt = Widgets.TextArea(new Rect(0f, curY, w, promptHeight), editingPrompt);
            }
            curY = h - 75f;

            // Use default button
            if (Widgets.ButtonText(new Rect(0f, curY, w / 2f - 5f, 30f), RimMindTranslations.Get("RimMind_UseDefault")))
            {
                editingPrompt = DefaultAutomationPrompts.Get(selectedEventType);
            }

            // Clear button
            if (Widgets.ButtonText(new Rect(w / 2f + 5f, curY, w / 2f - 5f, 30f), RimMindTranslations.Get("RimMind_Clear")))
            {
                editingPrompt = "";
            }
            curY += 35f;

            // Save button
            if (Widgets.ButtonText(new Rect(0f, curY, w, 30f), RimMindTranslations.Get("RimMind_SaveChanges")))
            {
                rule.customPrompt = editingPrompt;
                rule.cooldownSeconds = editingCooldown;
                RimMindMod.Settings.Write();
                Messages.Message(RimMindTranslations.Get("RimMind_SavedAutomationRule", selectedEventType), MessageTypeDefOf.TaskCompletion);
            }

            GUI.EndGroup();
        }

        private float CalculateContentHeight()
        {
            float height = 0f;
            var categories = RimMindMod.Settings.automationRules
                .GroupBy(kvp => DefaultAutomationPrompts.GetCategory(kvp.Key));

            foreach (var category in categories)
            {
                height += CATEGORY_HEIGHT + 2f; // Category header
                height += category.Count() * (ROW_HEIGHT + 2f); // Rules
                height += 5f; // Extra spacing
            }

            return height + 50f; // Extra padding
        }
    }
}
