using System;
using System.IO;
using System.Text;

namespace RimMind.Core
{
    public static class DebugLogger
    {
        private static string LogDir;
        private static string LogPath;
        private static readonly object lockObj = new object();
        private static bool initialized = false;

        public static void Init(string modRootDir = null)
        {
            lock (lockObj)
            {
                try
                {
                    if (string.IsNullOrEmpty(modRootDir))
                    {
                        Verse.Log.Warning("[RimMind] No mod root dir provided to DebugLogger.Init");
                        return;
                    }
                    LogDir = Path.Combine(modRootDir, "Logs");
                    LogPath = Path.Combine(LogDir, "debug.log");

                    if (!Directory.Exists(LogDir))
                        Directory.CreateDirectory(LogDir);

                    // Clear log on each startup
                    File.WriteAllText(LogPath, "=== RimMind Debug Log ===\n"
                        + "Started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n"
                        + new string('=', 60) + "\n\n");

                    initialized = true;
                    Verse.Log.Message("[RimMind] Debug logging to: " + LogPath);
                }
                catch (Exception ex)
                {
                    Verse.Log.Warning("[RimMind] Failed to init debug logger: " + ex.Message);
                }
            }
        }

        public static void Log(string category, string message)
        {
            if (!initialized) return;
            lock (lockObj)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.Append('[');
                    sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
                    sb.Append("] [");
                    sb.Append(category);
                    sb.Append("] ");
                    sb.AppendLine(message);
                    File.AppendAllText(LogPath, sb.ToString());
                }
                catch { }
            }
        }

        public static void LogSeparator(string title = null)
        {
            if (!initialized) return;
            lock (lockObj)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine();
                    sb.Append("--- ");
                    sb.Append(title ?? "");
                    sb.Append(' ');
                    sb.AppendLine(new string('-', Math.Max(0, 55 - (title?.Length ?? 0))));
                    File.AppendAllText(LogPath, sb.ToString());
                }
                catch { }
            }
        }

        public static void LogAPIRequest(int messageCount, int toolCount, string model)
        {
            LogSeparator("API REQUEST");
            Log("API", "Model: " + model + " | Messages: " + messageCount + " | Tools: " + toolCount);
        }

        public static void LogAPIResponse(bool hasToolCalls, int toolCallCount, string textPreview)
        {
            if (hasToolCalls)
                Log("API", "Response: " + toolCallCount + " tool call(s)");
            else
                Log("API", "Response: text (" + (textPreview?.Length ?? 0) + " chars) — " + Truncate(textPreview, 200));
        }

        public static void LogAPIError(string error)
        {
            Log("API", "ERROR: " + error);
        }

        public static void LogToolCall(int loopIndex, int callIndex, string toolName, string arguments)
        {
            Log("TOOL", "Loop " + loopIndex + " | Call " + callIndex + " | " + toolName);
            Log("TOOL", "  Args: " + Truncate(arguments, 2000));
        }

        public static void LogToolResult(string toolName, string result, long elapsedMs)
        {
            Log("TOOL", "  Result (" + elapsedMs + "ms, " + (result?.Length ?? 0) + " chars): " + Truncate(result, 5000));
        }

        public static void LogToolLoop(int loopCount, int maxLoops)
        {
            LogSeparator("TOOL LOOP " + loopCount + "/" + maxLoops);
        }

        public static void LogUserMessage(string message)
        {
            LogSeparator("USER MESSAGE");
            Log("CHAT", Truncate(message, 500));
        }

        public static void LogFinalResponse(string message)
        {
            LogSeparator("FINAL RESPONSE");
            Log("CHAT", Truncate(message, 1000));
        }

        public static void LogRawRequest(string json)
        {
            LogSeparator("RAW REQUEST");
            LogRaw(json);
        }

        public static void LogRawResponse(string json)
        {
            LogSeparator("RAW RESPONSE");
            LogRaw(json);
        }

        private static void LogRaw(string json)
        {
            if (!initialized) return;
            lock (lockObj)
            {
                try
                {
                    File.AppendAllText(LogPath, json + "\n\n");
                }
                catch { }
            }
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            if (s.Length <= maxLen) return s.Replace("\n", "\\n");
            return s.Substring(0, maxLen).Replace("\n", "\\n") + "... [truncated, " + s.Length + " total]";
        }
    }
}
