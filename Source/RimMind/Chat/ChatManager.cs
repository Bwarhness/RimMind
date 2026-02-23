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
        private const int MAX_TOOL_LOOPS = 50;
        private const int MAX_HISTORY_MESSAGES = 500;

        private readonly List<ChatMessage> conversationHistory = new List<ChatMessage>();
        private bool isProcessing;

        public bool IsProcessing => isProcessing;
        public string StatusMessage { get; private set; } = "";

        // Token usage from last API response
        public int LastPromptTokens { get; private set; }
        public int LastCompletionTokens { get; private set; }
        public int LastTotalTokens { get; private set; }

        public event Action OnMessageUpdated;

        public IReadOnlyList<ChatMessage> History => conversationHistory;

        public void SendMessage(string userMessage)
        {
            if (isProcessing || string.IsNullOrWhiteSpace(userMessage)) return;

            conversationHistory.Add(ChatMessage.User(userMessage));
            DebugLogger.LogUserMessage(userMessage);
            isProcessing = true;
            StatusMessage = "Thinking...";
            OnMessageUpdated?.Invoke();

            SendToAPI(0);
        }

        private void SendToAPI(int toolLoopCount)
        {
            if (toolLoopCount >= MAX_TOOL_LOOPS)
            {
                conversationHistory.Add(ChatMessage.Assistant("[RimMind hit the maximum tool call limit (" + MAX_TOOL_LOOPS + " rounds). Try asking a more focused question, or break it into smaller parts.]"));
                isProcessing = false;
                StatusMessage = "";
                OnMessageUpdated?.Invoke();
                return;
            }

            DebugLogger.LogToolLoop(toolLoopCount, MAX_TOOL_LOOPS);

            ChatRequest request;
            try
            {
                request = BuildRequest();
            }
            catch (Exception ex)
            {
                Log.Error("[RimMind] BuildRequest error: " + ex);
                conversationHistory.Add(ChatMessage.Assistant("[Error building request: " + ex.Message + "]"));
                isProcessing = false;
                StatusMessage = "";
                OnMessageUpdated?.Invoke();
                return;
            }
            DebugLogger.LogAPIRequest(request.messages.Count, request.tools?.Count ?? 0, request.model);

            Action<ChatResponse> handleResponse = response =>
            {
                // Track token usage from every response
                if (response.promptTokens > 0)
                {
                    LastPromptTokens = response.promptTokens;
                    LastCompletionTokens = response.completionTokens;
                    LastTotalTokens = response.totalTokens;
                }

                if (!response.success)
                {
                    DebugLogger.LogAPIError(response.error ?? "Unknown error");
                    conversationHistory.Add(ChatMessage.Assistant("[Error: " + response.error + "]"));
                    isProcessing = false;
                    StatusMessage = "";
                    OnMessageUpdated?.Invoke();
                    return;
                }

                if (response.HasToolCalls)
                {
                    DebugLogger.LogAPIResponse(response.HasToolCalls, response.message.tool_calls?.Count ?? 0, response.message.content);

                    // Add assistant message with tool_calls to history
                    conversationHistory.Add(response.message);

                    // Execute each tool call and add results
                    for (int i = 0; i < response.message.tool_calls.Count; i++)
                    {
                        var toolCall = response.message.tool_calls[i];
                        StatusMessage = "Querying: " + FormatToolName(toolCall.function.name) + "...";
                        OnMessageUpdated?.Invoke();

                        DebugLogger.LogToolCall(toolLoopCount, i, toolCall.function.name, toolCall.function.arguments);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        string result = ToolExecutor.Execute(toolCall.function.name, toolCall.function.arguments);
                        sw.Stop();
                        DebugLogger.LogToolResult(toolCall.function.name, result, sw.ElapsedMilliseconds);
                        conversationHistory.Add(ChatMessage.ToolResult(toolCall.id, result));
                    }

                    // Re-send with tool results
                    StatusMessage = "Processing results...";
                    OnMessageUpdated?.Invoke();
                    SendToAPI(toolLoopCount + 1);
                }
                else
                {
                    DebugLogger.LogAPIResponse(false, 0, response.message?.content);

                    // Final text response
                    if (response.message != null && !string.IsNullOrEmpty(response.message.content))
                    {
                        DebugLogger.LogFinalResponse(response.message.content);
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
            };

            if (RimMindMod.Settings.IsClaudeCode)
                ClaudeCodeClient.SendAsync(request, handleResponse);
            else if (RimMindMod.Settings.IsAnthropic)
                AnthropicClient.SendAsync(request, handleResponse);
            else if (RimMindMod.Settings.IsCustom)
                CustomProviderClient.SendAsync(request, handleResponse);
            else
                OpenRouterClient.SendAsync(request, handleResponse);
        }

        private ChatRequest BuildRequest()
        {
            var messages = new List<ChatMessage>();

            // System prompt
            string context = ColonyContext.GetLightweightContext();
            string directives = Core.DirectivesTracker.Instance?.PlayerDirectives;
            messages.Add(ChatMessage.System(PromptBuilder.BuildChatSystemPrompt(context, directives)));

            // Conversation history (trimmed)
            int start = Math.Max(0, conversationHistory.Count - MAX_HISTORY_MESSAGES);
            for (int i = start; i < conversationHistory.Count; i++)
            {
                messages.Add(conversationHistory[i]);
            }

            // Conditionally include tools: always for non-custom providers,
            // only if customProviderSupportsTools is true for custom providers
            bool isCustom = RimMindMod.Settings.IsCustom;
            bool includeTools = !isCustom || RimMindMod.Settings.customProviderSupportsTools;

            return new ChatRequest
            {
                model = RimMindMod.Settings.ActiveModelId,
                messages = messages,
                temperature = RimMindMod.Settings.temperature,
                max_tokens = RimMindMod.Settings.maxTokens,
                tools = includeTools ? ToolDefinitions.GetAllTools().ConvertAll(t => (JSONNode)t) : null
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
