using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
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
            absorbInputAroundWindow = true;

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
            Widgets.Label(titleRect, "RimMind Event Automation");
            Text.Font = GameFont.Small;

            // Enable/disable master toggle
            var masterToggleRect = new Rect(0f, 45f, inRect.width, 30f);
            bool wasEnabled = RimMindMod.Settings.enableEventAutomation;
            Widgets.CheckboxLabeled(masterToggleRect, "Enable Event Automation (Master Switch)", ref RimMindMod.Settings.enableEventAutomation);
            
            if (wasEnabled != RimMindMod.Settings.enableEventAutomation && RimMindMod.Settings.enableEventAutomation)
            {
                Messages.Message("Event automation enabled. Configure rules below.", MessageTypeDefOf.NeutralEvent);
            }

            // Description
            var descRect = new Rect(0f, 80f, inRect.width, 60f);
            Widgets.Label(descRect, "When enabled, RimMind will automatically send configured prompts to the AI when specific game events occur.\nEach event can have a custom prompt and cooldown period to prevent spam.");

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
            string displayName = eventType;
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
                Widgets.Label(statusRect, "Active");
            }
            else if (rule.enabled)
            {
                GUI.color = Color.yellow;
                Widgets.Label(statusRect, "No prompt");
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
                Widgets.Label(hintRect, "← Select an event type\nto configure automation");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            var innerRect = rect.ContractedBy(10f);

            float curY = 0f;

            // Event type title
            Text.Font = GameFont.Medium;
            var titleRect = new Rect(0f, curY, innerRect.width, 30f);
            Widgets.Label(titleRect, selectedEventType);
            Text.Font = GameFont.Small;
            curY += 35f;

            // Enabled toggle
            var rule = RimMindMod.Settings.automationRules[selectedEventType];
            var enabledRect = new Rect(0f, curY, innerRect.width, 30f);
            Widgets.CheckboxLabeled(enabledRect, "Enabled", ref rule.enabled);
            curY += 35f;

            // Cooldown setting
            var cooldownLabelRect = new Rect(0f, curY, innerRect.width, 25f);
            Widgets.Label(cooldownLabelRect, $"Cooldown: {editingCooldown} seconds");
            curY += 25f;

            var cooldownSliderRect = new Rect(0f, curY, innerRect.width, 25f);
            editingCooldown = Mathf.RoundToInt(Widgets.HorizontalSlider(cooldownSliderRect, editingCooldown, 10f, 300f, true, null, "10s", "300s"));
            curY += 30f;

            // Custom prompt label
            var promptLabelRect = new Rect(0f, curY, innerRect.width, 25f);
            Widgets.Label(promptLabelRect, "Custom Prompt:");
            curY += 25f;

            // Prompt text area
            var promptRect = new Rect(0f, curY, innerRect.width, innerRect.height - curY - 80f);
            editingPrompt = Widgets.TextArea(promptRect, editingPrompt);
            curY = innerRect.height - 75f;

            // Use default button
            var defaultButtonRect = new Rect(0f, curY, innerRect.width / 2f - 5f, 30f);
            if (Widgets.ButtonText(defaultButtonRect, "Use Default"))
            {
                editingPrompt = DefaultAutomationPrompts.Get(selectedEventType);
            }

            // Clear button
            var clearButtonRect = new Rect(innerRect.width / 2f + 5f, curY, innerRect.width / 2f - 5f, 30f);
            if (Widgets.ButtonText(clearButtonRect, "Clear"))
            {
                editingPrompt = "";
            }
            curY += 35f;

            // Save button
            var saveButtonRect = new Rect(0f, curY, innerRect.width, 30f);
            if (Widgets.ButtonText(saveButtonRect, "Save Changes"))
            {
                rule.customPrompt = editingPrompt;
                rule.cooldownSeconds = editingCooldown;
                RimMindMod.Settings.Write();
                Messages.Message($"Saved automation rule for {selectedEventType}", MessageTypeDefOf.TaskCompletion);
            }
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
