using RimMind.Core;
using RimMind.Languages;
using UnityEngine;
using Verse;

namespace RimMind.Chat
{
    public class DirectivesWindow : Window
    {
        private string editBuffer = "";
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(500f, 500f);

        public DirectivesWindow()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            forcePause = false;

            var tracker = DirectivesTracker.Instance;
            if (tracker != null)
                editBuffer = tracker.PlayerDirectives;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, RimMindTranslations.Get("RimMind_DirectivesTitle"));
            Text.Font = GameFont.Small;

            // Description
            float y = 34f;
            string desc = RimMindTranslations.Get("RimMind_DirectivesDesc");
            float descHeight = Text.CalcHeight(desc, inRect.width);
            var descRect = new Rect(0f, y, inRect.width, descHeight);
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(descRect, desc);
            GUI.color = Color.white;
            y += descHeight + 8f;

            // Buttons area at bottom
            float buttonHeight = 35f;
            float counterHeight = 20f;
            float bottomArea = buttonHeight + counterHeight + 12f;

            // Text area with manual scroll view
            float textAreaHeight = inRect.height - y - bottomArea;
            var textAreaRect = new Rect(0f, y, inRect.width, textAreaHeight);

            // Calculate content height based on text
            float contentHeight = Text.CalcHeight(editBuffer ?? "", textAreaRect.width - 20f);
            if (contentHeight < textAreaHeight)
                contentHeight = textAreaHeight;

            var viewRect = new Rect(0f, 0f, textAreaRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(textAreaRect, ref scrollPosition, viewRect);
            editBuffer = Widgets.TextArea(viewRect, editBuffer ?? "", false);
            Widgets.EndScrollView();

            y += textAreaHeight + 4f;

            // Character counter
            int charCount = (editBuffer ?? "").Length;
            var counterRect = new Rect(0f, y, inRect.width, counterHeight);
            if (charCount > 10000)
            {
                GUI.color = new Color(1f, 0.7f, 0.3f);
                Widgets.Label(counterRect, RimMindTranslations.Get("RimMind_DirectivesCharsLarge", charCount.ToString()));
            }
            else
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(counterRect, RimMindTranslations.Get("RimMind_DirectivesChars", charCount.ToString()));
            }
            GUI.color = Color.white;
            y += counterHeight + 4f;

            // Buttons
            float buttonWidth = 100f;
            float gap = 10f;
            float totalButtonWidth = buttonWidth * 2 + gap;
            float buttonX = (inRect.width - totalButtonWidth) / 2f;

            var saveRect = new Rect(buttonX, y, buttonWidth, buttonHeight);
            var cancelRect = new Rect(buttonX + buttonWidth + gap, y, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(saveRect, RimMindTranslations.Get("RimMind_DirectivesSave")))
            {
                var tracker = DirectivesTracker.Instance;
                if (tracker != null)
                {
                    tracker.PlayerDirectives = editBuffer ?? "";
                    DebugLogger.Log("Directives", "Player directives saved (" + (editBuffer ?? "").Length + " chars)");
                }
                Close();
            }

            if (Widgets.ButtonText(cancelRect, RimMindTranslations.Get("RimMind_DirectivesCancel")))
            {
                Close();
            }
        }
    }
}
