using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RimMind.API;
using RimMind.Languages;

namespace RimMind.Chat
{
    /// <summary>
    /// Static helper class for exporting chat conversation history to HTML or TXT files.
    /// </summary>
    public static class ChatExporter
    {
        /// <summary>
        /// Export conversation history to a styled, self-contained HTML file.
        /// User messages are rendered with a blue tint, AI messages with a green tint.
        /// </summary>
        public static void ExportToHtml(IReadOnlyList<ChatMessage> history, string filePath)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>RimMind Conversation Export</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body {");
            sb.AppendLine("      background-color: #1a1a1a;");
            sb.AppendLine("      color: #e0e0e0;");
            sb.AppendLine("      font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;");
            sb.AppendLine("      font-size: 14px;");
            sb.AppendLine("      margin: 0;");
            sb.AppendLine("      padding: 20px;");
            sb.AppendLine("    }");
            sb.AppendLine("    .chat-container {");
            sb.AppendLine("      max-width: 800px;");
            sb.AppendLine("      margin: 0 auto;");
            sb.AppendLine("    }");
            sb.AppendLine("    .chat-header {");
            sb.AppendLine("      text-align: center;");
            sb.AppendLine("      margin-bottom: 20px;");
            sb.AppendLine("      padding-bottom: 10px;");
            sb.AppendLine("      border-bottom: 1px solid #444;");
            sb.AppendLine("    }");
            sb.AppendLine("    .chat-header h1 {");
            sb.AppendLine("      color: #8ab4f8;");
            sb.AppendLine("      font-size: 22px;");
            sb.AppendLine("      margin: 0 0 4px 0;");
            sb.AppendLine("    }");
            sb.AppendLine("    .chat-header .export-date {");
            sb.AppendLine("      color: #888;");
            sb.AppendLine("      font-size: 12px;");
            sb.AppendLine("    }");
            sb.AppendLine("    .message-bubble {");
            sb.AppendLine("      border-radius: 6px;");
            sb.AppendLine("      padding: 10px 14px;");
            sb.AppendLine("      margin-bottom: 10px;");
            sb.AppendLine("      line-height: 1.5;");
            sb.AppendLine("      white-space: pre-wrap;");
            sb.AppendLine("      word-break: break-word;");
            sb.AppendLine("    }");
            sb.AppendLine("    .message-label {");
            sb.AppendLine("      font-weight: bold;");
            sb.AppendLine("      font-size: 12px;");
            sb.AppendLine("      margin-bottom: 4px;");
            sb.AppendLine("      text-transform: uppercase;");
            sb.AppendLine("      letter-spacing: 0.05em;");
            sb.AppendLine("    }");
            sb.AppendLine("    .user-bubble {");
            sb.AppendLine("      background-color: #1e3050;");
            sb.AppendLine("      border-left: 3px solid #4a78c4;");
            sb.AppendLine("    }");
            sb.AppendLine("    .user-bubble .message-label {");
            sb.AppendLine("      color: #8ab4f8;");
            sb.AppendLine("    }");
            sb.AppendLine("    .user-bubble .message-text {");
            sb.AppendLine("      color: #d8e4ff;");
            sb.AppendLine("    }");
            sb.AppendLine("    .ai-bubble {");
            sb.AppendLine("      background-color: #1a3020;");
            sb.AppendLine("      border-left: 3px solid #4a9c6a;");
            sb.AppendLine("    }");
            sb.AppendLine("    .ai-bubble .message-label {");
            sb.AppendLine("      color: #72c99a;");
            sb.AppendLine("    }");
            sb.AppendLine("    .ai-bubble .message-text {");
            sb.AppendLine("      color: #d8fde5;");
            sb.AppendLine("    }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class=\"chat-container\">");
            sb.AppendLine("    <div class=\"chat-header\">");
            sb.AppendLine("      <h1>RimMind AI — Conversation Export</h1>");
            sb.AppendFormat("      <div class=\"export-date\">Exported on {0}</div>\n", HtmlEncode(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.AppendLine("    </div>");

            foreach (var msg in history)
            {
                if (msg.role == "tool" || msg.role == "system") continue;
                if (msg.role == "assistant" && string.IsNullOrEmpty(msg.content) && msg.tool_calls != null)
                    continue;

                string content = msg.content ?? "";
                bool isUser = msg.role == "user";

                if (isUser)
                {
                    sb.AppendLine("    <div class=\"message-bubble user-bubble\">");
                    sb.AppendLine("      <div class=\"message-label\">You</div>");
                    sb.AppendFormat("      <div class=\"message-text\">{0}</div>\n", HtmlEncode(content));
                    sb.AppendLine("    </div>");
                }
                else
                {
                    sb.AppendLine("    <div class=\"message-bubble ai-bubble\">");
                    sb.AppendLine("      <div class=\"message-label\">RimMind</div>");
                    sb.AppendFormat("      <div class=\"message-text\">{0}</div>\n", HtmlEncode(content));
                    sb.AppendLine("    </div>");
                }
            }

            sb.AppendLine("  </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Export conversation history to a plain text file.
        /// Format: [You] message or [RimMind] message, one per line.
        /// </summary>
        public static void ExportToTxt(IReadOnlyList<ChatMessage> history, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RimMind AI — Conversation Export");
            sb.AppendLine("Exported: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine(new string('-', 40));
            sb.AppendLine();

            foreach (var msg in history)
            {
                if (msg.role == "tool" || msg.role == "system") continue;
                if (msg.role == "assistant" && string.IsNullOrEmpty(msg.content) && msg.tool_calls != null)
                    continue;

                string content = msg.content ?? "";
                bool isUser = msg.role == "user";
                string prefix = isUser ? "[You]" : "[RimMind]";
                sb.AppendLine(prefix + " " + content);
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Build the export directory path and filename with timestamp.
        /// Creates the directory if it does not exist.
        /// </summary>
        public static string BuildExportPath(string basePath, string extension)
        {
            if (!Directory.Exists(basePath))
                Directory.CreateDirectory(basePath);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = "RimMind_Export_" + timestamp + extension;
            return Path.Combine(basePath, filename);
        }

        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}
