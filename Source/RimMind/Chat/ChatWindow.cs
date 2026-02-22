using System;
using RimMind.Languages;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Chat
{
    public class ChatWindow : Window
    {
        private static ChatManager chatManager;
        private static ChatWindow instance;
        private string inputText = "";
        private Vector2 scrollPosition;
        private bool scrollToBottom;
        private bool showPrompts;
        private Vector2 promptScrollPos;

        public static ChatWindow Instance => instance;
        public ChatManager Manager => chatManager;
        public ChatManager ChatManager => chatManager;

        /// <summary>
        /// Get the shared ChatManager, creating it if needed.
        /// Works even when the chat window is not open.
        /// </summary>
        public static ChatManager SharedManager
        {
            get
            {
                if (chatManager == null)
                    chatManager = new ChatManager();
                return chatManager;
            }
        }

        // [0] = translation key for button label, [1] = prompt text (always English for AI)
        private static readonly string[][] quickPrompts = new string[][]
        {
            new[] { "RimMind_Prompt_Bedroom", "Build me a standard wooden bedroom near the center of the map with a bed, end table, and dresser" },
            new[] { "RimMind_Prompt_DiningKitchen", "Build a stone dining room and kitchen sharing a wall. Dining room should have a table and chairs, kitchen should have a stove" },
            new[] { "RimMind_Prompt_Barracks", "Build a 5-bed granite barracks with end tables for each bed" },
            new[] { "RimMind_Prompt_PowerSetup", "Set up a power grid: 2 solar generators, a wind turbine, and 4 batteries connected with conduits" },
            new[] { "RimMind_Prompt_Workshop", "Build a workshop room with an electric smithy, a hand tailoring bench, and a research bench" },
            new[] { "RimMind_Prompt_Hospital", "Build a hospital room with 3 medical beds, a lamp, and good flooring" },
            new[] { "RimMind_Prompt_Killbox", "Design a killbox entrance with sandbags and turrets for raid defense" },
            new[] { "RimMind_Prompt_BaseLayout", "Plan a compact base layout with bedroom, dining room, kitchen, workshop, and hospital - all sharing walls" },
            new[] { "RimMind_Prompt_ColonyStatus", "Give me a full colony status report - colonists, resources, threats, and priorities" },
            new[] { "RimMind_Prompt_MapScout", "Scout the map around our base and tell me what you see - terrain, resources, threats" },
        };

        public override Vector2 InitialSize => new Vector2(500f, 600f);

        public ChatWindow()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;

            if (chatManager == null)
                chatManager = new ChatManager();

            chatManager.OnMessageUpdated += () => scrollToBottom = true;
            instance = this;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            scrollToBottom = true;
            instance = this;
        }

        public override void PostClose()
        {
            base.PostClose();
            if (instance == this)
                instance = null;
        }

        public override void OnAcceptKeyPressed()
        {
            // Override base behavior (which closes the window) to send message instead
            if (!chatManager.IsProcessing && !string.IsNullOrWhiteSpace(inputText))
            {
                SendCurrentMessage();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title bar
            var titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, RimMindTranslations.Get("RimMind_ChatTitle"));
            Text.Font = GameFont.Small;

            // Header buttons — right-aligned
            float btnW = 62f;
            float btnH = 24f;
            float btnY = 2f;
            float btnX = inRect.width - 30f; // start from right, leave room for close X

            // Clear button
            btnX -= btnW;
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), RimMindTranslations.Get("RimMind_ChatClear")))
            {
                chatManager.ClearHistory();
            }

            // Prompts toggle button
            btnX -= btnW + 4f;
            GUI.color = showPrompts ? new Color(0.9f, 0.8f, 0.5f) : Color.white;
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), RimMindTranslations.Get("RimMind_ChatPrompts")))
            {
                showPrompts = !showPrompts;
            }
            GUI.color = Color.white;

            // Auto button
            btnX -= btnW + 4f;
            bool autoEnabled = Core.RimMindMod.Settings.enableEventAutomation;
            GUI.color = autoEnabled ? new Color(0.9f, 0.7f, 0.4f) : Color.white;
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), RimMindTranslations.Get("RimMind_ChatAuto")))
            {
                var existing = Find.WindowStack.WindowOfType<Automation.AutomationSettingsWindow>();
                if (existing != null)
                    Find.WindowStack.TryRemove(existing);
                else
                    Find.WindowStack.Add(new Automation.AutomationSettingsWindow());
            }
            GUI.color = Color.white;

            // Directives button
            btnX -= 72f + 4f;
            bool hasDirectives = Core.DirectivesTracker.Instance != null && !string.IsNullOrEmpty(Core.DirectivesTracker.Instance.PlayerDirectives);
            GUI.color = hasDirectives ? new Color(0.6f, 0.9f, 0.7f) : Color.white;
            if (Widgets.ButtonText(new Rect(btnX, btnY, 72f, btnH), RimMindTranslations.Get("RimMind_ChatDirectives")))
            {
                var existing = Find.WindowStack.WindowOfType<DirectivesWindow>();
                if (existing != null)
                    Find.WindowStack.TryRemove(existing);
                else
                    Find.WindowStack.Add(new DirectivesWindow());
            }
            GUI.color = Color.white;

            // Context button
            btnX -= btnW + 4f;
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), RimMindTranslations.Get("RimMind_ChatContext")))
            {
                var existing = Find.WindowStack.WindowOfType<ContextViewWindow>();
                if (existing != null)
                    Find.WindowStack.TryRemove(existing);
                else
                    Find.WindowStack.Add(new ContextViewWindow(chatManager));
            }

            // Vision button (Spatial Vision debug window)
            btnX -= btnW + 4f;
            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, btnH), "Vision"))
            {
                var existing = Find.WindowStack.WindowOfType<SpatialVisionWindow>();
                if (existing != null)
                    Find.WindowStack.TryRemove(existing);
                else
                    Find.WindowStack.Add(new SpatialVisionWindow());
            }

            // Token usage display
            float topOffset = 34f;
            if (chatManager.LastTotalTokens > 0)
            {
                var tokenRect = new Rect(0f, topOffset, inRect.width, 16f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.55f, 0.55f, 0.55f);
                string tokenText = "  " + RimMindTranslations.Get("RimMind_ChatTokensIn", 
                    FormatTokenCount(chatManager.LastPromptTokens), 
                    FormatTokenCount(chatManager.LastCompletionTokens),
                    FormatTokenCount(chatManager.LastTotalTokens));
                Widgets.Label(tokenRect, tokenText);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                topOffset += 16f;
            }
            float inputAreaHeight = 35f;
            float statusHeight = chatManager.IsProcessing ? 22f : 0f;
            float promptPanelHeight = showPrompts ? 90f : 0f;
            float chatHeight = inRect.height - topOffset - inputAreaHeight - statusHeight - promptPanelHeight - 8f;

            // Chat messages area
            var chatOuterRect = new Rect(0f, topOffset, inRect.width, chatHeight);
            DrawChatMessages(chatOuterRect);

            // Status indicator
            if (chatManager.IsProcessing)
            {
                var statusRect = new Rect(0f, topOffset + chatHeight + 2f, inRect.width, 20f);
                GUI.color = new Color(0.7f, 0.85f, 1f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(statusRect, "  " + chatManager.StatusMessage);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }

            // Quick prompt panel
            if (showPrompts)
            {
                float promptY = topOffset + chatHeight + statusHeight + (statusHeight > 0 ? 2f : 0f);
                DrawQuickPrompts(new Rect(0f, promptY, inRect.width, promptPanelHeight));
            }

            // Input area
            float inputY = inRect.height - inputAreaHeight;
            var inputRect = new Rect(0f, inputY, inRect.width - 70f, inputAreaHeight);
            var sendRect = new Rect(inRect.width - 65f, inputY, 65f, inputAreaHeight);

            // Text input
            GUI.SetNextControlName("RimMindInput");
            inputText = Widgets.TextField(inputRect, inputText);

            // Send button
            bool canSend = !chatManager.IsProcessing && !string.IsNullOrWhiteSpace(inputText);
            if (Widgets.ButtonText(sendRect, RimMindTranslations.Get("RimMind_ChatSend"), active: canSend) && canSend)
            {
                SendCurrentMessage();
            }

            // Focus input only when user clicks inside our window (not every frame)
            if (Event.current.type == EventType.MouseDown && Mouse.IsOver(inRect))
            {
                GUI.FocusControl("RimMindInput");
            }
        }

        private void SendCurrentMessage()
        {
            string msg = inputText.Trim();
            inputText = "";
            chatManager.SendMessage(msg);
        }

        private void DrawChatMessages(Rect outerRect)
        {
            Widgets.DrawBoxSolid(outerRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

            float contentHeight = CalculateContentHeight(outerRect.width - 30f);
            var viewRect = new Rect(0f, 0f, outerRect.width - 16f, Math.Max(contentHeight, outerRect.height));

            if (scrollToBottom)
            {
                scrollPosition.y = Math.Max(0, contentHeight - outerRect.height);
                scrollToBottom = false;
            }

            Widgets.BeginScrollView(outerRect, ref scrollPosition, viewRect);

            float y = 4f;
            float maxWidth = viewRect.width - 16f;

            foreach (var msg in chatManager.History)
            {
                // Skip tool messages in display (they're internal)
                if (msg.role == "tool" || msg.role == "system") continue;

                // Skip assistant messages that are just tool_calls with no content
                if (msg.role == "assistant" && string.IsNullOrEmpty(msg.content) && msg.tool_calls != null)
                    continue;

                bool isUser = msg.role == "user";
                string displayText = isUser 
                    ? RimMindTranslations.Get("RimMind_ChatYou", msg.content ?? "") 
                    : RimMindTranslations.Get("RimMind_ChatRimMind", msg.content ?? "");

                // Calculate height
                float textHeight = Text.CalcHeight(displayText, maxWidth - 12f);
                var bubbleRect = new Rect(4f, y, maxWidth, textHeight + 8f);

                // Background
                Color bgColor = isUser ? new Color(0.2f, 0.25f, 0.35f, 0.9f) : new Color(0.15f, 0.3f, 0.2f, 0.9f);

                // Check for error messages
                if (!isUser && msg.content != null && msg.content.StartsWith("[Error]"))
                    bgColor = new Color(0.35f, 0.15f, 0.15f, 0.9f);

                Widgets.DrawBoxSolid(bubbleRect, bgColor);

                var textRect = new Rect(bubbleRect.x + 6f, bubbleRect.y + 4f, bubbleRect.width - 12f, bubbleRect.height - 8f);

                GUI.color = isUser ? new Color(0.85f, 0.9f, 1f) : new Color(0.85f, 1f, 0.9f);
                Widgets.Label(textRect, displayText);
                GUI.color = Color.white;

                y += textHeight + 12f;
            }

            Widgets.EndScrollView();
        }

        private void DrawQuickPrompts(Rect outerRect)
        {
            Widgets.DrawBoxSolid(outerRect, new Color(0.15f, 0.15f, 0.18f, 0.9f));

            // Label
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(outerRect.x + 6f, outerRect.y + 2f, outerRect.width, 16f), RimMindTranslations.Get("RimMind_ChatQuickPrompts"));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Scrollable button area
            float btnY = outerRect.y + 18f;
            float btnAreaHeight = outerRect.height - 20f;
            var scrollOuterRect = new Rect(outerRect.x, btnY, outerRect.width, btnAreaHeight);

            // Calculate content width for horizontal scroll
            float btnPadding = 4f;
            float btnHeight = 28f;
            float x = 4f;
            float rowY = 0f;
            float maxRowWidth = outerRect.width - 16f;

            // Lay out as wrapping flow
            float contentHeight = btnHeight;
            foreach (var prompt in quickPrompts)
            {
                string label = RimMindTranslations.Get(prompt[0]);
                float btnWidth = Text.CalcSize(label).x + 16f;
                if (x + btnWidth > maxRowWidth && x > 4f)
                {
                    x = 4f;
                    contentHeight += btnHeight + btnPadding;
                }
                x += btnWidth + btnPadding;
            }
            contentHeight += 4f;

            var viewRect = new Rect(0f, 0f, maxRowWidth, Math.Max(contentHeight, btnAreaHeight));
            Widgets.BeginScrollView(scrollOuterRect, ref promptScrollPos, viewRect);

            x = 4f;
            rowY = 0f;
            foreach (var prompt in quickPrompts)
            {
                string label = RimMindTranslations.Get(prompt[0]);
                float btnWidth = Text.CalcSize(label).x + 16f;
                if (x + btnWidth > maxRowWidth && x > 4f)
                {
                    x = 4f;
                    rowY += btnHeight + btnPadding;
                }

                var btnRect = new Rect(x, rowY, btnWidth, btnHeight);

                // Draw button background
                Widgets.DrawBoxSolid(btnRect, new Color(0.25f, 0.28f, 0.35f, 0.9f));
                if (Mouse.IsOver(btnRect))
                {
                    Widgets.DrawHighlight(btnRect);
                    TooltipHandler.TipRegion(btnRect, prompt[1]);
                }

                // Button text
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.85f, 0.9f, 1f);
                Widgets.Label(btnRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(btnRect))
                {
                    inputText = prompt[1];
                }

                x += btnWidth + btnPadding;
            }

            Widgets.EndScrollView();
        }

        private static string FormatTokenCount(int tokens)
        {
            if (tokens >= 1000000)
                return (tokens / 1000000f).ToString("0.#") + "M";
            if (tokens >= 1000)
                return (tokens / 1000f).ToString("0.#") + "k";
            return tokens.ToString();
        }

        private float CalculateContentHeight(float width)
        {
            float y = 4f;
            foreach (var msg in chatManager.History)
            {
                if (msg.role == "tool" || msg.role == "system") continue;
                if (msg.role == "assistant" && string.IsNullOrEmpty(msg.content) && msg.tool_calls != null)
                    continue;

                string displayText = msg.role == "user" 
                    ? RimMindTranslations.Get("RimMind_ChatYou", msg.content ?? "") 
                    : RimMindTranslations.Get("RimMind_ChatRimMind", msg.content ?? "");
                float textHeight = Text.CalcHeight(displayText, width - 12f);
                y += textHeight + 12f;
            }
            return y;
        }
    }

    // MainButtonWorker to open the chat window
    public class MainButtonWorker_RimMind : MainButtonWorker
    {
        public override void Activate()
        {
            // Check if window is already open
            var existing = Find.WindowStack.WindowOfType<ChatWindow>();
            if (existing != null)
            {
                Find.WindowStack.TryRemove(existing);
            }
            else
            {
                Find.WindowStack.Add(new ChatWindow());
            }
        }
    }
}
