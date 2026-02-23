using System;
using RimMind.API;
using RimMind.Core;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    /// <summary>
    /// Tool for AI to ping/highlight locations on the map.
    /// Uses RimWorld's native letter system for persistence and familiarity.
    /// Camera jumps immediately, and a letter is posted for reference.
    /// </summary>
    public static class PingTools
    {
        public static string PingLocation(JSONNode args)
        {
            if (args == null)
                return ToolExecutor.JsonError("Arguments required: x, z");

            // Required parameters
            if (args["x"] == null || args["z"] == null)
                return ToolExecutor.JsonError("x and z coordinates are required");

            int x = args["x"].AsInt;
            int z = args["z"].AsInt;

            // Optional parameters
            string label = args["label"]?.Value ?? "";
            string colorParam = args["color"]?.Value ?? "yellow";
            int duration = args["duration"]?.AsInt ?? 5;

            // Validate coordinates
            Map map = Find.CurrentMap;
            if (map == null)
                return ToolExecutor.JsonError("No active map");

            if (x < 0 || x >= map.Size.x || z < 0 || z >= map.Size.z)
                return ToolExecutor.JsonError($"Coordinates ({x}, {z}) out of map bounds (0-{map.Size.x - 1}, 0-{map.Size.z - 1})");

            IntVec3 cell = new IntVec3(x, 0, z);

            // Determine letter def based on color
            LetterDef letterDef = GetLetterDef(colorParam);

            // Build letter text
            string letterTitle = string.IsNullOrEmpty(label) 
                ? "RimMind_PingTitle".Translate().ToString()
                : label;
            
            string letterText = string.IsNullOrEmpty(label)
                ? "RimMind_PingTextDefault".Translate(x, z).ToString()
                : "RimMind_PingTextWithLabel".Translate(label, x, z).ToString();

            // Execute on main thread
            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    // Jump camera immediately using IntVec3 overload
                    CameraJumper.TryJump(cell, map);

                    // Create LookTargets using TargetInfo (local map target)
                    var targetInfo = new TargetInfo(cell, map);
                    var lookTargets = new LookTargets(targetInfo);

                    // Create and post the letter
                    ChoiceLetter letter = LetterMaker.MakeLetter(
                        letterTitle,
                        letterText,
                        letterDef,
                        lookTargets
                    );
                    Find.LetterStack.ReceiveLetter(letter);
                }
                catch (Exception ex)
                {
                    Log.Warning("[RimMind] PingLocation error: " + ex.Message);
                }
            });

            // Return success response
            var result = new JSONObject();
            result["pinged"] = true;
            result["x"] = x;
            result["z"] = z;
            if (!string.IsNullOrEmpty(label))
                result["label"] = label;
            result["color"] = colorParam;
            result["letter_posted"] = true;
            return result.ToString();
        }

        // RimWorld letter colors (from StandardLetters.xml):
        //   ThreatBig:     (204,115,115) = red,    flashing, bounce, urgent
        //   ThreatSmall:   (204,155,125) = orange,  flashing, bounce
        //   NegativeEvent: (204,196,135) = yellow
        //   NeutralEvent:  (175,176,185) = grey/silver
        //   PositiveEvent: (120,176,216) = blue
        private static LetterDef GetLetterDef(string color)
        {
            if (string.IsNullOrEmpty(color))
                return LetterDefOf.NeutralEvent;

            switch (color.ToLowerInvariant())
            {
                case "blue":
                case "positive":
                case "info":
                    return LetterDefOf.PositiveEvent;

                case "red":
                case "danger":
                case "threat":
                    return LetterDefOf.ThreatBig;

                case "orange":
                case "warning":
                case "negative":
                    return LetterDefOf.ThreatSmall;

                case "yellow":
                    return LetterDefOf.NegativeEvent;

                case "grey":
                case "gray":
                case "neutral":
                default:
                    return LetterDefOf.NeutralEvent;
            }
        }
    }
}
