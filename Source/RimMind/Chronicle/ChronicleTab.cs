using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimMind.Core;

namespace RimMind.Chronicle
{
    /// <summary>
    /// UI window for displaying the Colony Chronicle in a newspaper-style format.
    /// This is opened as a tab within the RimMind window system.
    /// </summary>
    public class ChronicleTab : Window
    {
        private const float PARCHMENT_ALPHA = 0.95f;
        private const float COLUMN_GAP = 12f;
        private const float SECTION_MARGIN = 8f;
        private const float LINE_HEIGHT = 14f;

        // Parchment colors
        private static readonly Color PARCHMENT_COLOR = new Color(0.96f, 0.90f, 0.78f, PARCHMENT_ALPHA);
        private static readonly Color INK_COLOR = new Color(0.15f, 0.10f, 0.05f, 1f);
        private static readonly Color HEADLINE_COLOR = new Color(0.10f, 0.05f, 0.02f, 1f);
        private static readonly Color COLUMN_LINE_COLOR = new Color(0.6f, 0.5f, 0.3f, 0.5f);
        private static readonly Color SEPARATOR_COLOR = new Color(0.4f, 0.3f, 0.2f, 0.4f);

        private Vector2 scrollPosition;
        private float totalHeight;
        private WeeklyChronicle chronicle;
        private bool isLoading;
        private string loadingMessage = "Loading Chronicle...";

        public override Vector2 InitialSize => new Vector2(650f, 700f);

        public ChronicleTab()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            forcePause = false;

            RefreshChronicle();
        }

        /// <summary>
        /// Refresh the chronicle from the tracker.
        /// </summary>
        public void RefreshChronicle()
        {
            chronicle = null;
            isLoading = true;

            if (ChronicleTracker.Instance != null)
            {
                chronicle = ChronicleTracker.Instance.GetCurrentChronicle();

                if (ChronicleTracker.Instance.IsGeneratingChronicle)
                {
                    isLoading = true;
                    loadingMessage = "Generating Chronicle...";
                }
                else if (chronicle == null)
                {
                    loadingMessage = "No Chronicle available yet. Check back at the end of the week!";
                }
            }
            else
            {
                loadingMessage = "Chronicle system not initialized.";
            }

            if (chronicle != null)
                isLoading = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Draw parchment background
            DrawParchmentBackground(inRect);

            if (isLoading)
            {
                DrawLoadingState(inRect);
                return;
            }

            if (chronicle == null)
            {
                DrawNoChronicleState(inRect);
                return;
            }

            // Draw the chronicle content
            DrawChronicleContent(inRect);
        }

        private void DrawParchmentBackground(Rect inRect)
        {
            // Main parchment background
            Widgets.DrawBoxSolid(inRect, PARCHMENT_COLOR);

            // Add subtle aged paper texture effect (simple noise pattern via alternating pixels)
            // This is stylized - just draw a border to look like worn paper
            float borderWidth = 3f;

            // Outer border - darker ink
            GUI.color = new Color(0.5f, 0.4f, 0.3f, 0.6f);
            Widgets.DrawBox(new Rect(inRect.x, inRect.y, inRect.width, borderWidth));
            Widgets.DrawBox(new Rect(inRect.x, inRect.yMax - borderWidth, inRect.width, borderWidth));
            Widgets.DrawBox(new Rect(inRect.x, inRect.y, borderWidth, inRect.height));
            Widgets.DrawBox(new Rect(inRect.xMax - borderWidth, inRect.y, borderWidth, inRect.height));

            // Inner border - lighter
            GUI.color = new Color(0.6f, 0.5f, 0.35f, 0.3f);
            Widgets.DrawBox(new Rect(inRect.x + 6f, inRect.y + 6f, inRect.width - 12f, 1f));
            Widgets.DrawBox(new Rect(inRect.x + 6f, inRect.yMax - 7f, inRect.width - 12f, 1f));
            Widgets.DrawBox(new Rect(inRect.x + 6f, inRect.y + 6f, 1f, inRect.height - 12f));
            Widgets.DrawBox(new Rect(inRect.xMax - 7f, inRect.y + 6f, 1f, inRect.height - 12f));

            GUI.color = Color.white;
        }

        private void DrawLoadingState(Rect inRect)
        {
            float centerX = inRect.width / 2f;
            float centerY = inRect.height / 2f;

            Text.Font = GameFont.Medium;
            GUI.color = INK_COLOR;
            Text.Anchor = TextAnchor.MiddleCenter;

            Widgets.Label(new Rect(0f, centerY - 20f, inRect.width, 40f), loadingMessage);

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private void DrawNoChronicleState(Rect inRect)
        {
            float centerX = inRect.width / 2f;
            float centerY = inRect.height / 2f;

            Text.Font = GameFont.Medium;
            GUI.color = INK_COLOR;
            Text.Anchor = TextAnchor.MiddleCenter;

            Widgets.Label(new Rect(0f, centerY - 40f, inRect.width, 30f), "📰 Colony Chronicle");

            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(20f, centerY, inRect.width - 40f, 60f),
                "The Chronicle is published at the end of each week.\nCheck back when a new week begins!");

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void DrawChronicleContent(Rect inRect)
        {
            // Content area with padding
            float contentX = inRect.x + 20f;
            float contentY = inRect.y + 20f;
            float contentWidth = inRect.width - 40f;
            float contentHeight = inRect.height - 40f;

            // Calculate total height needed
            totalHeight = CalculateChronicleHeight(chronicle, contentWidth);

            // Begin scroll view
            var viewRect = new Rect(contentX, contentY, contentWidth - 16f, Math.Max(totalHeight, contentHeight));

            Widgets.BeginScrollView(new Rect(contentX, contentY, contentWidth, contentHeight),
                ref scrollPosition, viewRect);

            float y = 0f;

            // Draw masthead (newspaper title)
            y = DrawMasthead(chronicle, contentWidth, y);

            // Draw date line
            y = DrawDateLine(chronicle, contentWidth, y);

            // Draw separator
            y = DrawSeparator(contentWidth, y);

            // Draw headline
            y = DrawHeadline(chronicle, contentWidth, y);

            // Draw lead paragraph
            y = DrawLeadParagraph(chronicle, contentWidth, y);

            // Draw sections
            foreach (var section in chronicle.sections)
            {
                y = DrawSection(section, contentWidth, y);
            }

            // Draw quotes if any
            if (chronicle.quotes != null && chronicle.quotes.Count > 0)
            {
                y = DrawQuotes(chronicle.quotes, contentWidth, y);
            }

            // Draw footer
            y = DrawFooter(contentWidth, y);

            Widgets.EndScrollView();
        }

        private float CalculateChronicleHeight(WeeklyChronicle chronicle, float width)
        {
            float y = 0f;

            // Masthead
            y += 45f;

            // Date line
            y += 18f;

            // Separator
            y += 12f;

            // Headline
            if (!string.IsNullOrEmpty(chronicle.topHeadline))
                y += Text.CalcHeight(chronicle.topHeadline, width) + 8f;

            // Lead paragraph
            if (!string.IsNullOrEmpty(chronicle.leadParagraph))
                y += Text.CalcHeight(chronicle.leadParagraph, width) + 16f;

            // Sections
            foreach (var section in chronicle.sections)
            {
                y += SECTION_MARGIN;
                y += 20f; // Section title
                if (!string.IsNullOrEmpty(section.content))
                    y += Text.CalcHeight(section.content, width) + 4f;
            }

            // Quotes
            if (chronicle.quotes != null && chronicle.quotes.Count > 0)
            {
                y += SECTION_MARGIN;
                y += 20f;
                foreach (var quote in chronicle.quotes)
                {
                    y += Text.CalcHeight($"\"{quote.quote}\" — {quote.colonistName}", width) + 4f;
                }
            }

            // Footer
            y += SECTION_MARGIN + 20f;

            return y;
        }

        private float DrawMasthead(WeeklyChronicle chronicle, float width, float y)
        {
            Text.Font = GameFont.Medium;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperCenter;

            string masthead = "📰 THE COLONY CHRONICLE 📰";
            Widgets.Label(new Rect(0f, y, width, 30f), masthead);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.3f, 0.25f, 0.15f, 0.8f);

            string tagline = "Your Trusted Source for Colony News Since Year One";
            Widgets.Label(new Rect(0f, y + 22f, width, 16f), tagline);

            y += 45f;

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }

        private float DrawDateLine(WeeklyChronicle chronicle, float width, float y)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.35f, 0.3f, 0.2f, 0.9f);
            Text.Anchor = TextAnchor.UpperCenter;

            string dateLine = $"Week {chronicle.weekNumber} • {chronicle.season}, Day {chronicle.gameDay} • Year {chronicle.year}";
            Widgets.Label(new Rect(0f, y, width, 16f), dateLine);

            y += 18f;

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }

        private float DrawSeparator(float width, float y)
        {
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal(0f, y + 3f, width);
            GUI.color = new Color(0.5f, 0.4f, 0.3f, 0.4f);
            Widgets.DrawLineHorizontal(0f, y + 5f, width);

            y += 12f;
            GUI.color = Color.white;

            return y;
        }

        private float DrawHeadline(WeeklyChronicle chronicle, float width, float y)
        {
            if (string.IsNullOrEmpty(chronicle.topHeadline))
                return y;

            Text.Font = GameFont.Medium;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperCenter;

            float height = Text.CalcHeight(chronicle.topHeadline, width);
            Widgets.Label(new Rect(0f, y, width, height), chronicle.topHeadline);

            y += height + 8f;

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            return y;
        }

        private float DrawLeadParagraph(WeeklyChronicle chronicle, float width, float y)
        {
            if (string.IsNullOrEmpty(chronicle.leadParagraph))
                return y;

            Text.Font = GameFont.Small;
            GUI.color = INK_COLOR;
            Text.Anchor = TextAnchor.UpperLeft;

            float height = Text.CalcHeight(chronicle.leadParagraph, width);
            Widgets.Label(new Rect(0f, y, width, height), chronicle.leadParagraph);

            y += height + 16f;

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }

        private float DrawSection(ChronicleSection section, float width, float y)
        {
            y += SECTION_MARGIN;

            // Section header with emoji
            Text.Font = GameFont.Small;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperLeft;

            string header = $"  {section.emoji} {section.title} {section.emoji}  ";
            Widgets.Label(new Rect(0f, y, width, 20f), header);

            // Underline for section header
            float headerWidth = Text.CalcSize(header).x;
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal(0f, y + 18f, Math.Min(headerWidth, width));

            y += 22f;

            // Section content
            if (!string.IsNullOrEmpty(section.content))
            {
                Text.Font = GameFont.Small;
                GUI.color = INK_COLOR;

                float contentHeight = Text.CalcHeight(section.content, width);
                Widgets.Label(new Rect(0f, y, width, contentHeight), section.content);

                y += contentHeight + 4f;
            }

            GUI.color = Color.white;

            return y;
        }

        private float DrawQuotes(List<ColonistQuote> quotes, float width, float y)
        {
            y += SECTION_MARGIN;

            // Section header
            Text.Font = GameFont.Small;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperCenter;

            string header = "  📜 NOTABLE QUOTES  ";
            Widgets.Label(new Rect(0f, y, width, 20f), header);

            float headerWidth = Text.CalcSize(header).x;
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal(0f, y + 18f, Math.Min(headerWidth, width));

            y += 24f;

            // Individual quotes
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.2f, 0.15f, 0.1f, 1f);
            Text.Anchor = TextAnchor.UpperLeft;

            foreach (var quote in quotes)
            {
                string quoteText = $"\"{quote.quote}\" — {quote.colonistName}";
                float quoteHeight = Text.CalcHeight(quoteText, width);

                Widgets.Label(new Rect(10f, y, width - 20f, quoteHeight), quoteText);
                y += quoteHeight + 4f;
            }

            GUI.color = Color.white;

            return y;
        }

        private float DrawFooter(float width, float y)
        {
            y += SECTION_MARGIN;

            // Double line separator
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal(0f, y, width);
            GUI.color = new Color(0.5f, 0.4f, 0.3f, 0.4f);
            Widgets.DrawLineHorizontal(0f, y + 2f, width);

            y += 8f;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.4f, 0.35f, 0.25f, 0.8f);
            Text.Anchor = TextAnchor.UpperCenter;

            string footer = "— End of Chronicle —\nBrought to you by RimMind AI • The Colony's Trusted Advisor";
            float footerHeight = Text.CalcHeight(footer, width);
            Widgets.Label(new Rect(0f, y, width, footerHeight), footer);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }
    }
}
