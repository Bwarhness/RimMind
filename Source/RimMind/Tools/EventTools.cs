using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimMind.API;
using RimWorld;
using Verse;

namespace RimMind.Tools
{
    public static class EventTools
    {
        public static string GetRecentEvents(int count)
        {
            if (count <= 0) count = 5;
            if (count > 20) count = 20;

            var arr = new JSONArray();

            var archive = Find.Archive;
            if (archive != null)
            {
                var entries = archive.ArchivablesListForReading
                    .OrderByDescending(a => a.CreatedTicksGame)
                    .Take(count);

                foreach (var entry in entries)
                {
                    var obj = new JSONObject();

                    if (entry is Letter letter)
                    {
                        obj["type"] = letter.def?.LabelCap.ToString() ?? "Letter";
                        obj["title"] = letter.Label.ToString() ?? "Unknown";
                        obj["dayOccurred"] = (entry.CreatedTicksGame / 60000f).ToString("F1");
                    }
                    else if (entry is ArchivedDialog dialog)
                    {
                        obj["type"] = "Message";
                        obj["title"] = entry.GetType().GetProperty("text")?.GetValue(entry)?.ToString() ?? "Message";
                        obj["dayOccurred"] = (entry.CreatedTicksGame / 60000f).ToString("F1");
                    }
                    else
                    {
                        obj["type"] = entry.GetType().Name;
                        obj["dayOccurred"] = (entry.CreatedTicksGame / 60000f).ToString("F1");
                    }

                    arr.Add(obj);
                }
            }

            var result = new JSONObject();
            result["recentEvents"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }

        public static string GetActiveAlerts()
        {
            var arr = new JSONArray();

            try
            {
                var alertsReadout = ((UIRoot_Play)Find.UIRoot).alerts;
                if (alertsReadout != null)
                {
                    // AllAlerts may be private - try reflection
                    var allAlertsField = typeof(AlertsReadout).GetField("AllAlerts", BindingFlags.NonPublic | BindingFlags.Instance)
                                     ?? typeof(AlertsReadout).GetField("allAlerts", BindingFlags.NonPublic | BindingFlags.Instance);

                    System.Collections.Generic.List<Alert> allAlerts = null;
                    if (allAlertsField != null)
                        allAlerts = allAlertsField.GetValue(alertsReadout) as System.Collections.Generic.List<Alert>;

                    // Fallback: try public property
                    if (allAlerts == null)
                    {
                        var prop = typeof(AlertsReadout).GetProperty("AllAlerts", BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null)
                            allAlerts = prop.GetValue(alertsReadout) as System.Collections.Generic.List<Alert>;
                    }

                    if (allAlerts != null)
                    {
                        foreach (var alert in allAlerts)
                        {
                            if (alert.Active)
                            {
                                var obj = new JSONObject();
                                obj["label"] = alert.GetLabel().ToString();
                                obj["severity"] = alert.Priority.ToString();

                                TaggedString explanation = alert.GetExplanation();
                                string explanationStr = explanation.ToString();
                                if (!string.IsNullOrEmpty(explanationStr) && explanationStr.Length > 200)
                                    explanationStr = explanationStr.Substring(0, 200) + "...";
                                obj["explanation"] = explanationStr;

                                arr.Add(obj);
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                var errObj = new JSONObject();
                errObj["error"] = "Could not read alerts: " + ex.Message;
                arr.Add(errObj);
            }

            var result = new JSONObject();
            result["activeAlerts"] = arr;
            result["count"] = arr.Count;
            return result.ToString();
        }
    }
}
