using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
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
        private const float COLUMN_GAP = 16f;
        private const float SECTION_MARGIN = 10f;
        private const float LINE_HEIGHT = 14f;
        private const float MARGIN_PORTRAIT = 50f;

        // Parchment colors
        private static readonly Color PARCHMENT_COLOR = new Color(0.96f, 0.90f, 0.78f, PARCHMENT_ALPHA);
        private static readonly Color INK_COLOR = new Color(0.15f, 0.10f, 0.05f, 1f);
        private static readonly Color HEADLINE_COLOR = new Color(0.10f, 0.05f, 0.02f, 1f);
        private static readonly Color COLUMN_LINE_COLOR = new Color(0.6f, 0.5f, 0.3f, 0.5f);
        private static readonly Color SEPARATOR_COLOR = new Color(0.4f, 0.3f, 0.2f, 0.4f);
        private static readonly Color EDITORIAL_BG_COLOR = new Color(0.92f, 0.88f, 0.76f, 0.8f);
        private static readonly Color PREDICTION_BAR_EMPTY = new Color(0.5f, 0.45f, 0.35f, 0.8f);
        private static readonly Color PREDICTION_BAR_FILLED = new Color(0.2f, 0.5f, 0.3f, 1f);

        private Vector2 scrollPosition;
        private float totalHeight;
        private WeeklyChronicle chronicle;
        private bool isLoading;
        private string loadingMessage = "Loading Chronicle...";
        private int chronicleVolume = 1; // Increments each year

        public override Vector2 InitialSize => new Vector2(750f, 800f);

        public ChronicleTab()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            forcePause = false;

            // Calculate volume number (years since start would be ideal, but we start at 1)
            chronicleVolume = DateTime.Now.Year - 2024 + 1;

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

            // Outer border - darker ink effect
            float borderWidth = 3f;
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
            float contentX = inRect.x + 16f;
            float contentY = inRect.y + 16f;
            float contentWidth = inRect.width - 32f;
            float contentHeight = inRect.height - 32f;

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

            // Draw "ON THIS DAY" if available
            if (!string.IsNullOrEmpty(chronicle.oneYearAgoSummary))
            {
                y = DrawOneYearAgo(chronicle.oneYearAgoSummary, contentWidth, y);
            }

            // Draw headline
            y = DrawHeadline(chronicle, contentWidth, y);

            // Draw lead paragraph
            y = DrawLeadParagraph(chronicle, contentWidth, y);

            // Draw "FROM THE EDITOR'S DESK" early if we have it
            if (!string.IsNullOrEmpty(chronicle.editorial))
            {
                y = DrawEditorial(chronicle.editorial, contentWidth, y);
            }

            // Draw running joke if we have it
            if (!string.IsNullOrEmpty(chronicle.runningJokeCurrent))
            {
                y = DrawRunningJoke(chronicle.runningJokeCurrent, contentWidth, y);
            }

            // Draw sections in two-column layout for main content
            y = DrawSectionsTwoColumn(chronicle.sections, contentWidth, y);

            // Draw interviews if any
            if (chronicle.interviews != null && chronicle.interviews.Count > 0)
            {
                y = DrawInterviews(chronicle.interviews, contentWidth, y);
            }

            // Draw predictions with confidence bars
            if (chronicle.predictions != null && chronicle.predictions.Count > 0)
            {
                y = DrawPredictions(chronicle.predictions, contentWidth, y);
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
            y += 50f;

            // Date line
            y += 18f;

            // Separator
            y += 12f;

            // One year ago
            if (!string.IsNullOrEmpty(chronicle.oneYearAgoSummary))
                y += 40f;

            // Headline
            if (!string.IsNullOrEmpty(chronicle.topHeadline))
                y += Text.CalcHeight(chronicle.topHeadline, width) + 10f;

            // Lead paragraph
            if (!string.IsNullOrEmpty(chronicle.leadParagraph))
                y += Text.CalcHeight(chronicle.leadParagraph, width) + 18f;

            // Editorial
            if (!string.IsNullOrEmpty(chronicle.editorial))
                y += Text.CalcHeight(chronicle.editorial, width) + 20f;

            // Running joke
            if (!string.IsNullOrEmpty(chronicle.runningJokeCurrent))
                y += Text.CalcHeight(chronicle.runningJokeCurrent, width) + 16f;

            // Sections (rough estimate for two columns)
            float colWidth = (width - COLUMN_GAP) / 2f;
            foreach (var section in chronicle.sections)
            {
                y += SECTION_MARGIN + 22f;
                if (!string.IsNullOrEmpty(section.content))
                    y += Text.CalcHeight(section.content, colWidth) + 6f;
            }

            // Interviews
            if (chronicle.interviews != null)
            {
                foreach (var interview in chronicle.interviews)
                {
                    y += SECTION_MARGIN + 80f;
                }
            }

            // Predictions
            if (chronicle.predictions != null && chronicle.predictions.Count > 0)
            {
                y += SECTION_MARGIN + 25f;
                foreach (var pred in chronicle.predictions)
                {
                    y += Text.CalcHeight(pred.eventDescription + " " + pred.GetConfidenceBar(), width) + 6f;
                }
            }

            // Quotes
            if (chronicle.quotes != null && chronicle.quotes.Count > 0)
            {
                y += SECTION_MARGIN + 25f;
                foreach (var quote in chronicle.quotes)
                {
                    y += Text.CalcHeight($"\"{quote.quote}\" — {quote.colonistName}", width) + 6f;
                }
            }

            // Footer
            y += SECTION_MARGIN + 40f;

            return y;
        }

        private float DrawMasthead(WeeklyChronicle chronicle, float width, float y)
        {
            // Get colony name from game
            string colonyName = "THE COLONY CHRONICLE";
            if (Find.CurrentMap?.Parent?.LabelCap != null)
                colonyName = $"THE {Find.CurrentMap.Parent.LabelCap.ToString().ToUpper()} CHRONICLE";

            Text.Font = GameFont.Medium;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperCenter;

            string masthead = $"📰 {colonyName} 📰";
            Widgets.Label(new Rect(0f, y, width, 28f), masthead);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.3f, 0.25f, 0.15f, 0.8f);

            string tagline = $"\"Tell the truth. It's the most devastating weapon in journalism.\"\n— Vol. {chronicleVolume}, No. {chronicle.weekNumber} —";
            float taglineHeight = Text.CalcHeight(tagline, width);
            Widgets.Label(new Rect(0f, y + 25f, width, taglineHeight), tagline);

            y += 25f + taglineHeight + 4f;

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

        private float DrawOneYearAgo(string summary, float width, float y)
        {
            // Special styling for "On This Day" section
            Rect bgRect = new Rect(0f, y, width, 36f);
            GUI.color = new Color(0.85f, 0.8f, 0.7f, 0.6f);
            Widgets.DrawBoxSolid(bgRect, EDITORIAL_BG_COLOR);
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawBox(bgRect, 1);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            Text.Anchor = TextAnchor.UpperLeft;

            string label = "📅 ON THIS DAY, ONE YEAR AGO:";
            Widgets.Label(new Rect(8f, y + 4f, width - 16f, 14f), label);

            Text.Font = GameFont.Small;
            GUI.color = new Color(0.2f, 0.15f, 0.1f, 1f);
            float summaryHeight = Text.CalcHeight(summary, width - 16f);
            Widgets.Label(new Rect(8f, y + 18f, width - 16f, summaryHeight), summary);

            y += 22f + summaryHeight + 6f;

            Text.Anchor = TextAnchor.UpperLeft;
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

            y += height + 10f;

            // Decorative line under headline
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal(width * 0.2f, y - 4f, width * 0.6f);

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

            y += height + 18f;

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }

        private float DrawEditorial(string editorial, float width, float y)
        {
            // Calculate height
            float textHeight = Text.CalcHeight(editorial, width - 16f);

            // Draw background
            Rect bgRect = new Rect(0f, y, width, textHeight + 30f);
            GUI.color = EDITORIAL_BG_COLOR;
            Widgets.DrawBoxSolid(bgRect.ExpandedBy(4f), EDITORIAL_BG_COLOR);

            // Draw decorative border
            GUI.color = new Color(0.5f, 0.4f, 0.3f, 0.5f);
            Widgets.DrawLineHorizontal(0f, y + 2f, width);

            Text.Font = GameFont.Small;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperCenter;

            Widgets.Label(new Rect(0f, y + 6f, width, 18f), "📝 FROM THE EDITOR'S DESK");

            // Draw line under header
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal(20f, y + 22f, width - 40f);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = INK_COLOR;

            Widgets.Label(new Rect(8f, y + 26f, width - 16f, textHeight), editorial);

            y += textHeight + 32f;

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }

        private float DrawRunningJoke(string joke, float width, float y)
        {
            float textHeight = Text.CalcHeight(joke, width);

            // Draw italicized, slightly indented running joke
            Rect bgRect = new Rect(10f, y, width - 10f, textHeight + 16f);
            GUI.color = new Color(0.9f, 0.85f, 0.75f, 0.5f);
            Widgets.DrawBoxSolid(bgRect, new Color(0.9f, 0.85f, 0.75f, 0.3f));

            Text.Font = GameFont.Small;
            GUI.color = new Color(0.35f, 0.25f, 0.15f, 0.9f);
            Text.Anchor = TextAnchor.UpperLeft;

            // Italic style by using italics markers
            Widgets.Label(new Rect(16f, y + 8f, width - 26f, textHeight), $"🔥 {joke}");

            y += textHeight + 20f;

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }

        private float DrawSectionsTwoColumn(List<ChronicleSection> sections, float width, float y)
        {
            if (sections == null || sections.Count == 0)
                return y;

            float colWidth = (width - COLUMN_GAP) / 2f;
            float col1Y = y;
            float col2Y = y;

            bool leftColumn = true;

            for (int i = 0; i < sections.Count; i++)
            {
                var section = sections[i];

                // Skip predictions and running joke sections (they're handled elsewhere)
                if (section.title == "PREDICTIONS" || section.title == "RUNNING JOKE" || section.title == "EDITORIAL")
                    continue;

                if (leftColumn)
                {
                    col1Y = DrawSectionSingleColumn(section, colWidth, col1Y, left: true);
                    leftColumn = false;
                }
                else
                {
                    col2Y = DrawSectionSingleColumn(section, colWidth, col2Y, left: false);
                    leftColumn = true;
                }
            }

            // Return the max height
            return Math.Max(col1Y, col2Y);
        }

        private float DrawSectionSingleColumn(ChronicleSection section, float width, float y, bool left)
        {
            float xOffset = left ? 0f : width + COLUMN_GAP;

            // Section header with emoji
            Text.Font = GameFont.Small;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperLeft;

            string header = $"{section.emoji} {section.title} {section.emoji}";
            Widgets.Label(new Rect(xOffset, y, width, 18f), header);

            // Underline for section header
            float headerWidth = Text.CalcSize(header).x;
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal(xOffset, y + 16f, Math.Min(headerWidth, width));

            y += 22f;

            // Section content
            if (!string.IsNullOrEmpty(section.content))
            {
                Text.Font = GameFont.Small;
                GUI.color = INK_COLOR;

                float contentHeight = Text.CalcHeight(section.content, width);
                Widgets.Label(new Rect(xOffset, y, width, contentHeight), section.content);

                y += contentHeight + 6f;
            }

            GUI.color = Color.white;

            return y;
        }

        private float DrawInterviews(List<ColonistInterview> interviews, float width, float y)
        {
            y += SECTION_MARGIN;

            // Section header
            Text.Font = GameFont.Small;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperCenter;

            string header = "  📋 COLONIST INTERVIEW  ";
            Widgets.Label(new Rect(0f, y, width, 20f), header);

            float headerWidth = Text.CalcSize(header).x;
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal((width - headerWidth) / 2f, y + 18f, headerWidth);

            y += 26f;

            foreach (var interview in interviews)
            {
                // Draw interview box
                Rect interviewRect = new Rect(10f, y, width - 20f, 70f);
                GUI.color = new Color(0.92f, 0.88f, 0.80f, 0.6f);
                Widgets.DrawBoxSolid(interviewRect, new Color(0.92f, 0.88f, 0.80f, 0.4f));

                // Header
                Text.Font = GameFont.Small;
                GUI.color = HEADLINE_COLOR;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(new Rect(interviewRect.x + 10f, y + 6f, interviewRect.width - 20f, 18f),
                    $"INTERVIEW WITH: {interview.colonistName}, {interview.age}, {interview.currentJob}");

                // Question
                Text.Font = GameFont.Small;
                GUI.color = INK_COLOR;
                string questionText = $"\"{interview.question}\"";
                Widgets.Label(new Rect(interviewRect.x + 10f, y + 24f, interviewRect.width - 20f, 16f), questionText);

                // Answer
                GUI.color = new Color(0.25f, 0.2f, 0.15f, 1f);
                string answerText = $"\"{interview.answer}\"";
                Widgets.Label(new Rect(interviewRect.x + 10f, y + 40f, interviewRect.width - 20f, 24f), answerText);

                y += 80f;
            }

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }

        private float DrawPredictions(List<Prediction> predictions, float width, float y)
        {
            y += SECTION_MARGIN;

            // Section header
            Text.Font = GameFont.Small;
            GUI.color = HEADLINE_COLOR;
            Text.Anchor = TextAnchor.UpperCenter;

            string header = "  🔮 WEEK AHEAD: PREDICTIONS  ";
            Widgets.Label(new Rect(0f, y, width, 20f), header);

            float headerWidth = Text.CalcSize(header).x;
            GUI.color = SEPARATOR_COLOR;
            Widgets.DrawLineHorizontal((width - headerWidth) / 2f, y + 18f, headerWidth);

            y += 26f;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            foreach (var pred in predictions)
            {
                // Prediction text
                GUI.color = INK_COLOR;
                string predText = $"• {pred.eventDescription}";
                float textWidth = Text.CalcSize(predText).x;
                Widgets.Label(new Rect(0f, y, textWidth + 10f, 18f), predText);

                // Confidence bar
                float barX = textWidth + 14f;
                float barWidth = 100f;
                float barHeight = 14f;

                // Draw bar background
                GUI.color = PREDICTION_BAR_EMPTY;
                Widgets.DrawBoxSolid(new Rect(barX, y + 2f, barWidth, barHeight), PREDICTION_BAR_EMPTY);

                // Draw filled portion
                float filledWidth = (pred.confidencePct / 100f) * barWidth;
                GUI.color = GetConfidenceColor(pred.confidencePct);
                Widgets.DrawBoxSolid(new Rect(barX, y + 2f, filledWidth, barHeight), GetConfidenceColor(pred.confidencePct));

                // Draw border
                GUI.color = new Color(0.3f, 0.25f, 0.2f, 0.8f);
                Widgets.DrawBox(new Rect(barX, y + 2f, barWidth, barHeight), 1);

                // Confidence percentage
                GUI.color = pred.confidencePct > 50 ? Color.white : INK_COLOR;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(barX + 2f, y + 3f, barWidth - 4f, barHeight), $"{pred.confidencePct}%");

                // Basis line
                y += 18f;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.4f, 0.35f, 0.3f, 0.9f);
                string basisText = $"  (based on: {pred.basis})";
                Widgets.Label(new Rect(0f, y, width, 14f), basisText);

                y += 18f;

                Text.Font = GameFont.Small;
            }

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }

        private Color GetConfidenceColor(int confidencePct)
        {
            if (confidencePct >= 75)
                return new Color(0.2f, 0.6f, 0.3f, 1f);  // Green - high confidence
            if (confidencePct >= 50)
                return new Color(0.7f, 0.6f, 0.2f, 1f);  // Yellow - medium confidence
            return new Color(0.7f, 0.3f, 0.2f, 1f);        // Red - low confidence
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
            Widgets.DrawLineHorizontal((width - headerWidth) / 2f, y + 18f, headerWidth);

            y += 26f;

            // Individual quotes
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.2f, 0.15f, 0.1f, 1f);
            Text.Anchor = TextAnchor.UpperLeft;

            foreach (var quote in quotes)
            {
                string quoteText = $"\"{quote.quote}\"";
                float quoteHeight = Text.CalcHeight(quoteText, width - 20f);

                Widgets.Label(new Rect(10f, y, width - 20f, quoteHeight), quoteText);
                y += quoteHeight;

                // Attribution
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.35f, 0.3f, 0.25f, 1f);
                Widgets.Label(new Rect(width - 100f, y, 90f, 14f), $"— {quote.colonistName}");
                GUI.color = new Color(0.2f, 0.15f, 0.1f, 1f);
                Text.Font = GameFont.Small;

                y += 18f;
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

            string footer = "— End of Chronicle —\nBrought to you by RimMind AI, Colony Chronicle Staff Editor\n\"We have survived 47 colonies. This one might make it.\"";
            float footerHeight = Text.CalcHeight(footer, width);
            Widgets.Label(new Rect(0f, y, width, footerHeight), footer);

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            return y;
        }
    }
}
