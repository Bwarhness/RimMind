using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Chat
{
    public class ChatWindow : Window
    {
        private static ChatManager chatManager;
        private string inputText = "";
        private Vector2 scrollPosition;
        private bool scrollToBottom;

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
        }

        public override void PostOpen()
        {
            base.PostOpen();
            scrollToBottom = true;
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
            Widgets.Label(titleRect, "RimMind AI");
            Text.Font = GameFont.Small;

            // Directives button
            var directivesRect = new Rect(inRect.width - 170f, 2f, 80f, 24f);
            bool hasDirectives = Core.DirectivesTracker.Instance != null && !string.IsNullOrEmpty(Core.DirectivesTracker.Instance.PlayerDirectives);
            GUI.color = hasDirectives ? new Color(0.6f, 0.9f, 0.7f) : Color.white;
            if (Widgets.ButtonText(directivesRect, "Directives"))
            {
                var existing = Find.WindowStack.WindowOfType<DirectivesWindow>();
                if (existing != null)
                    Find.WindowStack.TryRemove(existing);
                else
                    Find.WindowStack.Add(new DirectivesWindow());
            }
            GUI.color = Color.white;

            // Clear button
            var clearRect = new Rect(inRect.width - 80f, 2f, 70f, 24f);
            if (Widgets.ButtonText(clearRect, "Clear"))
            {
                chatManager.ClearHistory();
            }

            float topOffset = 34f;
            float inputAreaHeight = 35f;
            float statusHeight = chatManager.IsProcessing ? 22f : 0f;
            float chatHeight = inRect.height - topOffset - inputAreaHeight - statusHeight - 8f;

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

            // Input area
            float inputY = inRect.height - inputAreaHeight;
            var inputRect = new Rect(0f, inputY, inRect.width - 70f, inputAreaHeight);
            var sendRect = new Rect(inRect.width - 65f, inputY, 65f, inputAreaHeight);

            // Text input
            GUI.SetNextControlName("RimMindInput");
            inputText = Widgets.TextField(inputRect, inputText);

            // Send button
            bool canSend = !chatManager.IsProcessing && !string.IsNullOrWhiteSpace(inputText);
            if (Widgets.ButtonText(sendRect, "Send", active: canSend) && canSend)
            {
                SendCurrentMessage();
            }

            // Auto-focus input (only when DirectivesWindow is not open)
            if (Event.current.type == EventType.Repaint && Find.WindowStack.WindowOfType<DirectivesWindow>() == null)
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
                string prefix = isUser ? "You: " : "RimMind: ";
                string displayText = prefix + (msg.content ?? "");

                // Calculate height
                float textHeight = Text.CalcHeight(displayText, maxWidth - 12f);
                var bubbleRect = new Rect(4f, y, maxWidth, textHeight + 8f);

                // Background
                Color bgColor = isUser ? new Color(0.2f, 0.25f, 0.35f, 0.9f) : new Color(0.15f, 0.3f, 0.2f, 0.9f);

                // Check for error messages
                if (!isUser && msg.content != null && msg.content.StartsWith("[Error"))
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

        private float CalculateContentHeight(float width)
        {
            float y = 4f;
            foreach (var msg in chatManager.History)
            {
                if (msg.role == "tool" || msg.role == "system") continue;
                if (msg.role == "assistant" && string.IsNullOrEmpty(msg.content) && msg.tool_calls != null)
                    continue;

                string prefix = msg.role == "user" ? "You: " : "RimMind: ";
                string displayText = prefix + (msg.content ?? "");
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
