using System;
using System.Linq;
using System.Text;
using RimMind.Chat;
using RimMind.Languages;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.UI
{
    /// <summary>
    /// Dialog that opens when clicking a location with the RimMind Query designator.
    /// Shows cell info and allows sending a location-aware message to the AI.
    /// </summary>
    public class Dialog_RimMindLocationQuery : Window
    {
        private readonly IntVec3 location;
        private readonly Map map;
        private string inputText = "";
        private string cellInfoText;

        public override Vector2 InitialSize => new Vector2(450f, 280f);

        public Dialog_RimMindLocationQuery(IntVec3 loc, Map map)
        {
            this.location = loc;
            this.map = map;

            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
            absorbInputAroundWindow = false;

            // Pre-compute cell info
            cellInfoText = BuildCellInfoDisplay();
        }

        public override void OnAcceptKeyPressed()
        {
            if (!string.IsNullOrWhiteSpace(inputText))
            {
                SendQuery();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Text.Font = GameFont.Medium;
            string title = RimMindTranslations.Get("RimMind_LocationQueryTitle", location.x, location.z);
            Widgets.Label(new Rect(0f, 0f, inRect.width - 30f, 30f), title);
            Text.Font = GameFont.Small;

            // Cell info display
            float y = 36f;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Text.Font = GameFont.Tiny;
            float infoHeight = Text.CalcHeight(cellInfoText, inRect.width - 10f);
            Widgets.Label(new Rect(5f, y, inRect.width - 10f, infoHeight), cellInfoText);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            y += infoHeight + 10f;

            // Hint text
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            string hint = RimMindTranslations.Get("RimMind_LocationQueryHint");
            Widgets.Label(new Rect(5f, y, inRect.width - 10f, 20f), hint);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            y += 22f;

            // Input field (multi-line text area)
            float inputHeight = 80f;
            var inputRect = new Rect(0f, y, inRect.width, inputHeight);
            inputText = Widgets.TextArea(inputRect, inputText);

            y += inputHeight + 10f;

            // Send button
            float btnWidth = 100f;
            float btnHeight = 32f;
            var sendBtnRect = new Rect(inRect.width - btnWidth, y, btnWidth, btnHeight);
            bool canSend = !string.IsNullOrWhiteSpace(inputText);

            if (Widgets.ButtonText(sendBtnRect, RimMindTranslations.Get("RimMind_LocationQuerySend"), active: canSend) && canSend)
            {
                SendQuery();
            }

            // Focus input field
            GUI.SetNextControlName("RimMindLocationInput");
            if (Event.current.type == EventType.Layout)
            {
                GUI.FocusControl("RimMindLocationInput");
            }
        }

        private void SendQuery()
        {
            string contextBlock = BuildContextBlock();
            string fullMessage = contextBlock + "\n" + inputText.Trim();

            // Use ChatManager to send the message
            var chatManager = ChatWindow.SharedManager;
            if (chatManager != null)
            {
                chatManager.SendMessage(fullMessage);
                
                // Open the chat window if not already open
                var existingWindow = Find.WindowStack.WindowOfType<ChatWindow>();
                if (existingWindow == null)
                {
                    Find.WindowStack.Add(new ChatWindow());
                }
            }

            Close();
        }

        private string BuildContextBlock()
        {
            var sb = new StringBuilder();
            sb.Append("[Location: x=");
            sb.Append(location.x);
            sb.Append(", z=");
            sb.Append(location.z);

            // Terrain
            var terrain = map.terrainGrid.TerrainAt(location);
            if (terrain != null)
            {
                sb.Append(" | Terrain: ");
                sb.Append(terrain.LabelCap);
            }

            // Fertility
            float fertility = map.fertilityGrid.FertilityAt(location);
            if (fertility > 0)
            {
                sb.Append(" | Fertility: ");
                sb.Append((fertility * 100f).ToString("F0"));
                sb.Append("%");
            }

            // Zone
            var zone = map.zoneManager.ZoneAt(location);
            if (zone != null)
            {
                sb.Append(" | Zone: ");
                sb.Append(zone.label);
            }

            // Buildings at this cell
            var things = location.GetThingList(map);
            var buildings = things.Where(t => t is Building).ToList();
            if (buildings.Count > 0)
            {
                sb.Append(" | Buildings: ");
                sb.Append(string.Join(", ", buildings.Select(b => b.LabelCap.ToString())));
            }
            else
            {
                sb.Append(" | Buildings: None");
            }

            // Room info
            try
            {
                var room = location.GetRoom(map);
                if (room != null && !room.TouchesMapEdge && room.Role != null)
                {
                    sb.Append(" | Room: ");
                    sb.Append(room.Role.LabelCap);
                }
            }
            catch { }

            sb.Append("]");
            return sb.ToString();
        }

        private string BuildCellInfoDisplay()
        {
            var sb = new StringBuilder();

            // Terrain
            var terrain = map.terrainGrid.TerrainAt(location);
            if (terrain != null)
            {
                sb.Append("Terrain: ");
                sb.Append(terrain.LabelCap);
            }

            // Fertility
            float fertility = map.fertilityGrid.FertilityAt(location);
            if (fertility > 0)
            {
                sb.Append(" • Fertility: ");
                sb.Append((fertility * 100f).ToString("F0"));
                sb.Append("%");
            }

            // Roof
            var roof = map.roofGrid.RoofAt(location);
            if (roof != null)
            {
                sb.Append(" • Roof: ");
                sb.Append(roof.LabelCap);
            }

            // Zone
            var zone = map.zoneManager.ZoneAt(location);
            if (zone != null)
            {
                sb.Append("\nZone: ");
                sb.Append(zone.label);
            }

            // Buildings
            var things = location.GetThingList(map);
            var buildings = things.Where(t => t is Building).ToList();
            if (buildings.Count > 0)
            {
                sb.Append("\nBuildings: ");
                sb.Append(string.Join(", ", buildings.Select(b => b.LabelCap.ToString())));
            }

            // Room
            try
            {
                var room = location.GetRoom(map);
                if (room != null && !room.TouchesMapEdge && room.Role != null)
                {
                    sb.Append("\nRoom: ");
                    sb.Append(room.Role.LabelCap);
                }
            }
            catch { }

            return sb.ToString();
        }
    }
}
