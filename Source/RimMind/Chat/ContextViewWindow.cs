using System;
using System.Collections.Generic;
using System.Text;
using RimMind.API;
using RimMind.Core;
using RimMind.Tools;
using UnityEngine;
using Verse;

namespace RimMind.Chat
{
    public class ContextViewWindow : Window
    {
        private const int CHUNK_SIZE = 2000;

        private readonly ChatManager chatManager;
        private int activeTab;
        private Vector2[] tabScrollPositions = new Vector2[3];

        private List<string>[] tabChunks = new List<string>[3];
        private string[] tabHeaders = new string[3];
        private int cachedSystemChars;
        private int cachedToolsChars;
        private int cachedHistoryChars;
        private int cachedMessageCount;
        private int cachedToolCount;

        // Monospace font style
        private static GUIStyle monoStyle;
        private static Font monoFont;

        public override Vector2 InitialSize => new Vector2(700f, 650f);

        public ContextViewWindow(ChatManager manager)
        {
            chatManager = manager;
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            forcePause = false;

            EnsureMonoStyle();
            RebuildAll();
        }

        private static void EnsureMonoStyle()
        {
            if (monoStyle != null) return;

            // Try common monospace fonts available on Windows
            string[] monoFonts = { "Consolas", "Courier New", "Lucida Console" };
            foreach (var fontName in monoFonts)
            {
                monoFont = Font.CreateDynamicFontFromOSFont(fontName, 13);
                if (monoFont != null) break;
            }

            monoStyle = new GUIStyle(GUI.skin.label)
            {
                font = monoFont,
                fontSize = 13,
                wordWrap = true,
                richText = false,
                normal = { textColor = new Color(0.82f, 0.82f, 0.82f) }
            };
        }

        private float CalcMonoHeight(string text, float width)
        {
            EnsureMonoStyle();
            return monoStyle.CalcHeight(new GUIContent(text), width);
        }

        private void RebuildAll()
        {
            BuildSystemTab();
            BuildToolsTab();
            BuildChatTab();
        }

        private void BuildSystemTab()
        {
            string colonyContext = ColonyContext.GetLightweightContext();
            string directives = DirectivesTracker.Instance?.PlayerDirectives;
            string systemPrompt = PromptBuilder.BuildChatSystemPrompt(colonyContext, directives);
            cachedSystemChars = systemPrompt.Length;

            tabChunks[0] = SplitIntoChunks(systemPrompt);
            tabHeaders[0] = "System (" + FormatSize(cachedSystemChars) + ")";
        }

        private void BuildToolsTab()
        {
            var tools = ToolDefinitions.GetAllTools();
            cachedToolCount = tools?.Count ?? 0;

            var sb = new StringBuilder();
            if (tools != null && tools.Count > 0)
            {
                for (int i = 0; i < tools.Count; i++)
                {
                    var fn = tools[i]["function"];
                    string name = fn?["name"]?.Value ?? "?";
                    string desc = fn?["description"]?.Value ?? "";
                    var parameters = fn?["parameters"];

                    sb.AppendLine("── " + (i + 1) + ". " + name + " ──");
                    if (!string.IsNullOrEmpty(desc))
                        sb.AppendLine(desc);
                    if (parameters != null && !parameters.IsNull)
                        sb.AppendLine("Parameters: " + parameters.ToString(true));
                    sb.AppendLine();
                }
            }

            string toolsText = sb.ToString();
            cachedToolsChars = toolsText.Length;

            tabChunks[1] = SplitIntoChunks(toolsText);
            tabHeaders[1] = "Tools (" + cachedToolCount + ", " + FormatSize(cachedToolsChars) + ")";
        }

        private void BuildChatTab()
        {
            var sb = new StringBuilder();
            int histChars = 0;
            int msgCount = 0;
            var history = chatManager.History;
            int start = Math.Max(0, history.Count - 500);

            for (int i = start; i < history.Count; i++)
            {
                var msg = history[i];
                msgCount++;

                string roleLabel = msg.role.ToUpper();
                int msgChars = (msg.content ?? "").Length;

                sb.AppendLine("── #" + msgCount + " " + roleLabel + " (" + FormatSize(msgChars) + ") ──");

                if (msg.role == "tool")
                    sb.AppendLine("[tool_call_id: " + (msg.tool_call_id ?? "?") + "]");

                if (msg.tool_calls != null && msg.tool_calls.Count > 0)
                {
                    sb.AppendLine("[tool_calls: " + msg.tool_calls.Count + "]");
                    foreach (var tc in msg.tool_calls)
                    {
                        string args = tc.function.arguments ?? "{}";
                        sb.AppendLine("  -> " + tc.function.name + "(" + args + ")");
                        msgChars += tc.function.name.Length + args.Length;
                    }
                }

                if (!string.IsNullOrEmpty(msg.content))
                {
                    if (msg.content.Length > 3000)
                    {
                        sb.AppendLine(msg.content.Substring(0, 3000));
                        sb.AppendLine("... [truncated, " + msg.content.Length + " chars total]");
                    }
                    else
                    {
                        sb.AppendLine(msg.content);
                    }
                }

                sb.AppendLine();
                histChars += msgChars;
            }

            cachedHistoryChars = histChars;
            cachedMessageCount = msgCount;

            string chatText = sb.ToString();
            tabChunks[2] = SplitIntoChunks(chatText);
            tabHeaders[2] = "Chat (" + cachedMessageCount + " msgs, " + FormatSize(cachedHistoryChars) + ")";
        }

        private static List<string> SplitIntoChunks(string text)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                chunks.Add("(empty)");
                return chunks;
            }

            var current = new StringBuilder();
            int pos = 0;
            while (pos < text.Length)
            {
                int newline = text.IndexOf('\n', pos);
                string line;
                if (newline >= 0)
                {
                    line = text.Substring(pos, newline - pos + 1);
                    pos = newline + 1;
                }
                else
                {
                    line = text.Substring(pos);
                    pos = text.Length;
                }

                if (current.Length > 0 && current.Length + line.Length > CHUNK_SIZE)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }
                current.Append(line);
            }

            if (current.Length > 0)
                chunks.Add(current.ToString());

            if (chunks.Count == 0)
                chunks.Add("(empty)");

            return chunks;
        }

        public override void DoWindowContents(Rect inRect)
        {
            EnsureMonoStyle();

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, 180f, 30f), "Context Inspector");
            Text.Font = GameFont.Small;

            // Refresh button
            if (Widgets.ButtonText(new Rect(inRect.width - 80f, 2f, 70f, 24f), "Refresh"))
            {
                RebuildAll();
            }

            // Summary bar
            float y = 30f;
            Text.Font = GameFont.Tiny;

            int totalChars = cachedSystemChars + cachedToolsChars + cachedHistoryChars;
            int estTokens = totalChars / 4;

            GUI.color = new Color(0.9f, 0.85f, 0.6f);
            string summary = "Total: " + FormatSize(totalChars) + " (~" + FormatTokenCount(estTokens) + " tokens est.)";
            if (chatManager.LastTotalTokens > 0)
            {
                summary += "  |  Last API: " + FormatTokenCount(chatManager.LastPromptTokens) + " in + "
                    + FormatTokenCount(chatManager.LastCompletionTokens) + " out = "
                    + FormatTokenCount(chatManager.LastTotalTokens) + " actual";
            }
            Widgets.Label(new Rect(4f, y, inRect.width - 8f, 16f), summary);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 18f;

            // Tab buttons
            float tabWidth = (inRect.width - 8f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var tabRect = new Rect(2f + i * tabWidth, y, tabWidth - 2f, 26f);
                bool isActive = activeTab == i;

                if (isActive)
                    Widgets.DrawBoxSolid(tabRect, new Color(0.25f, 0.3f, 0.4f));
                else
                    Widgets.DrawBoxSolid(tabRect, new Color(0.14f, 0.14f, 0.16f));

                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = isActive ? Color.white : new Color(0.65f, 0.65f, 0.65f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(tabRect, tabHeaders[i]);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(tabRect))
                    activeTab = i;
            }
            y += 28f;

            // Content area
            var contentRect = new Rect(0f, y, inRect.width, inRect.height - y);
            DrawTabContent(contentRect, activeTab);
        }

        private void DrawTabContent(Rect outerRect, int tab)
        {
            var chunks = tabChunks[tab];
            if (chunks == null || chunks.Count == 0) return;

            float textWidth = outerRect.width - 28f;

            // Calculate total height using the monospace style
            float totalHeight = 4f;
            for (int i = 0; i < chunks.Count; i++)
            {
                totalHeight += CalcMonoHeight(chunks[i], textWidth) + 2f;
            }
            totalHeight += 10f;

            var viewRect = new Rect(0f, 0f, outerRect.width - 16f, Math.Max(totalHeight, outerRect.height));

            Widgets.DrawBoxSolid(outerRect, new Color(0.08f, 0.08f, 0.08f, 0.9f));
            Widgets.BeginScrollView(outerRect, ref tabScrollPositions[tab], viewRect);

            float cy = 4f;
            for (int i = 0; i < chunks.Count; i++)
            {
                float h = CalcMonoHeight(chunks[i], textWidth);
                GUI.Label(new Rect(6f, cy, textWidth, h), chunks[i], monoStyle);
                cy += h + 2f;
            }

            Widgets.EndScrollView();
        }

        private static string FormatSize(int chars)
        {
            if (chars >= 1000000)
                return (chars / 1000000f).ToString("0.#") + "M chars";
            if (chars >= 1000)
                return (chars / 1000f).ToString("0.#") + "k chars";
            return chars + " chars";
        }

        private static string FormatTokenCount(int tokens)
        {
            if (tokens >= 1000000)
                return (tokens / 1000000f).ToString("0.#") + "M";
            if (tokens >= 1000)
                return (tokens / 1000f).ToString("0.#") + "k";
            return tokens.ToString();
        }
    }
}
