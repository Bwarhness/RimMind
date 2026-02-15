using RimMind.API;
using RimMind.Core;

namespace RimMind.Tools
{
    public static class DirectiveTools
    {
        public static string GetDirectives()
        {
            var tracker = DirectivesTracker.Instance;
            if (tracker == null)
                return ToolExecutor.JsonError("No active game.");

            var result = new JSONObject();
            string directives = tracker.PlayerDirectives;
            result["directives"] = directives;
            result["length"] = directives.Length;
            result["empty"] = string.IsNullOrWhiteSpace(directives);
            return result.ToString();
        }

        public static string AddDirective(string text)
        {
            var tracker = DirectivesTracker.Instance;
            if (tracker == null)
                return ToolExecutor.JsonError("No active game.");

            if (string.IsNullOrWhiteSpace(text))
                return ToolExecutor.JsonError("Directive text cannot be empty.");

            tracker.AddDirective(text);

            var result = new JSONObject();
            result["success"] = true;
            result["added"] = text.Trim();
            result["total_length"] = tracker.PlayerDirectives.Length;
            return result.ToString();
        }

        public static string RemoveDirective(string search)
        {
            var tracker = DirectivesTracker.Instance;
            if (tracker == null)
                return ToolExecutor.JsonError("No active game.");

            if (string.IsNullOrWhiteSpace(search))
                return ToolExecutor.JsonError("Search text cannot be empty.");

            bool removed = tracker.RemoveDirective(search);

            var result = new JSONObject();
            result["success"] = removed;
            if (removed)
                result["message"] = "Removed directive matching '" + search + "'.";
            else
                result["message"] = "No directive found matching '" + search + "'.";
            result["remaining_directives"] = tracker.PlayerDirectives;
            return result.ToString();
        }
    }
}
