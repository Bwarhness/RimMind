using System;
using System.Collections.Generic;
using RimMind.API;
using RimMind.Core;
using RimMind.Tools;
using Verse;

namespace RimMind.Chat
{
    public class ChatManager
    {
        private const int MAX_TOOL_LOOPS = 5;
        private const int MAX_HISTORY_MESSAGES = 40;

        private readonly List<ChatMessage> conversationHistory = new List<ChatMessage>();
        private bool isProcessing;

        public bool IsProcessing => isProcessing;
        public string StatusMessage { get; private set; } = "";

        public event Action OnMessageUpdated;

        public IReadOnlyList<ChatMessage> History => conversationHistory;

        public void SendMessage(string userMessage)
        {
            if (isProcessing || string.IsNullOrWhiteSpace(userMessage)) return;

            conversationHistory.Add(ChatMessage.User(userMessage));
            isProcessing = true;
            StatusMessage = "Thinking...";
            OnMessageUpdated?.Invoke();

            SendToAPI(0);
        }

        private void SendToAPI(int toolLoopCount)
        {
            if (toolLoopCount >= MAX_TOOL_LOOPS)
            {
                conversationHistory.Add(ChatMessage.Assistant("[RimMind hit the maximum tool call limit. Please try a simpler question.]"));
                isProcessing = false;
                StatusMessage = "";
                OnMessageUpdated?.Invoke();
                return;
            }

            var request = BuildRequest();

            OpenRouterClient.SendAsync(request, response =>
            {
                if (!response.success)
                {
                    conversationHistory.Add(ChatMessage.Assistant("[Error: " + response.error + "]"));
                    isProcessing = false;
                    StatusMessage = "";
                    OnMessageUpdated?.Invoke();
                    return;
                }

                if (response.HasToolCalls)
                {
                    // Add assistant message with tool_calls to history
                    conversationHistory.Add(response.message);

                    // Execute each tool call and add results
                    foreach (var toolCall in response.message.tool_calls)
                    {
                        StatusMessage = "Querying: " + FormatToolName(toolCall.function.name) + "...";
                        OnMessageUpdated?.Invoke();

                        string result = ToolExecutor.Execute(toolCall.function.name, toolCall.function.arguments);
                        conversationHistory.Add(ChatMessage.ToolResult(toolCall.id, result));
                    }

                    // Re-send with tool results
                    StatusMessage = "Processing results...";
                    OnMessageUpdated?.Invoke();
                    SendToAPI(toolLoopCount + 1);
                }
                else
                {
                    // Final text response
                    if (response.message != null && !string.IsNullOrEmpty(response.message.content))
                    {
                        conversationHistory.Add(ChatMessage.Assistant(response.message.content));
                    }
                    else
                    {
                        conversationHistory.Add(ChatMessage.Assistant("[Received empty response from AI.]"));
                    }

                    isProcessing = false;
                    StatusMessage = "";
                    OnMessageUpdated?.Invoke();
                }
            });
        }

        private ChatRequest BuildRequest()
        {
            var messages = new List<ChatMessage>();

            // System prompt
            string context = ColonyContext.GetLightweightContext();
            messages.Add(ChatMessage.System(PromptBuilder.BuildChatSystemPrompt(context)));

            // Conversation history (trimmed)
            int start = Math.Max(0, conversationHistory.Count - MAX_HISTORY_MESSAGES);
            for (int i = start; i < conversationHistory.Count; i++)
            {
                messages.Add(conversationHistory[i]);
            }

            return new ChatRequest
            {
                model = RimMindMod.Settings.modelId,
                messages = messages,
                temperature = RimMindMod.Settings.temperature,
                max_tokens = RimMindMod.Settings.maxTokens,
                tools = ToolDefinitions.GetAllTools().ConvertAll(t => (JSONNode)t)
            };
        }

        public void ClearHistory()
        {
            conversationHistory.Clear();
            OnMessageUpdated?.Invoke();
        }

        private static string FormatToolName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "data";
            return name.Replace("_", " ");
        }
    }
}
