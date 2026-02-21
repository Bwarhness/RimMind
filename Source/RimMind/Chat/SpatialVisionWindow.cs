using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimMind.API;
using RimMind.Core;
using RimMind.Languages;
using RimMind.Tools;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Chat
{
    /// <summary>
    /// Debug window showing what the AI "sees" when querying map data.
    /// Visualizes the text grid that MapTools generates for AI spatial decisions.
    /// 
    /// Related Issues:
    /// - #92: Debug UI - Spatial Vision Window
    /// - #93: Debug UI - AI Vision Visualization Tab (implemented via this window)
    /// </summary>
    public class SpatialVisionWindow : Window
    {
        private int activeTab = 0; // 0=Current Map, 1=Walkability, 2=Buildability, 3=Rooms, 4=Cover
        private int regionX = 0, regionZ = 0, regionW = 50, regionH = 50;
        private bool autoRefresh = false;
        private float cellSize = 8f;
        
        private string cachedGrid = "";
        private int lastRefreshTick = 0;
        
        private Vector2 scrollPosition = Vector2.zero;
        
        // Tab names
        private readonly string[] tabNames = { "Current Map", "Walkability", "Buildability", "Rooms", "Cover" };
        
        // Color mappings for each tab
        private static readonly Dictionary<char, Color> currentMapColors = new Dictionary<char, Color>
        {
            // Terrain
            ['.'] = new Color(0.2f, 0.5f, 0.2f),   // Soil
            ['~'] = new Color(0.2f, 0.4f, 0.6f),   // Water
            ['^'] = new Color(0.4f, 0.35f, 0.25f), // Mountain/Rock
            ['#'] = new Color(0.5f, 0.5f, 0.5f), // Wall/Floor
            ['+'] = new Color(0.3f, 0.3f, 0.7f),   // Construction
            ['='] = new Color(0.6f, 0.6f, 0.6f),    // Existing structure
            
            // Doors & Furniture
            ['D'] = new Color(0.6f, 0.4f, 0.2f),    // Door
            ['B'] = new Color(0.4f, 0.3f, 0.2f),    // Bed
            ['T'] = new Color(0.5f, 0.5f, 0.3f),    // Table
            ['S'] = new Color(0.5f, 0.5f, 0.5f),    // Stove
            ['L'] = new Color(0.6f, 0.6f, 0.2f),   // Light
            ['O'] = new Color(0.3f, 0.5f, 0.3f),   // Storage
            
            // Pawns & Creatures
            ['@'] = new Color(1f, 0.9f, 0.3f),     // Colonist
            ['c'] = new Color(0.7f, 0.5f, 0.3f),   // Animal
            ['h'] = new Color(0.8f, 0.3f, 0.3f),   // Hostile
            ['!'] = new Color(1f, 0.5f, 0.5f),    // Danger
            
            // Items
            ['*'] = new Color(0.8f, 0.8f, 0.8f),  // Item/Resource
            ['x'] = new Color(0.3f, 0.3f, 0.3f),   // Item on floor
        };
        
        private static readonly Dictionary<char, Color> walkabilityColors = new Dictionary<char, Color>
        {
            ['.'] = new Color(0.2f, 0.6f, 0.2f),   // Walkable (green)
            ['#'] = new Color(0.7f, 0.2f, 0.2f),   // Blocked (red)
            ['@'] = new Color(1f, 0.9f, 0.3f),   // Pawn (yellow)
            ['~'] = new Color(0.2f, 0.4f, 0.7f), // Water (blue)
            ['^'] = new Color(0.5f, 0.45f, 0.35f), // Mountain (brown)
        };
        
        private static readonly Dictionary<char, Color> buildabilityColors = new Dictionary<char, Color>
        {
            ['+'] = new Color(0.2f, 0.6f, 0.2f),   // Buildable (green)
            ['='] = new Color(0.5f, 0.5f, 0.5f), // Existing (gray)
            ['-'] = new Color(0.7f, 0.2f, 0.2f), // Unbuildable (red)
            ['^'] = new Color(0.5f, 0.4f, 0.3f), // Mountain (brown)
            ['#'] = new Color(0.6f, 0.6f, 0.6f), // Wall (gray)
            ['~'] = new Color(0.2f, 0.4f, 0.6f), // Water (blue)
        };
        
        private static readonly Dictionary<char, Color> roomColors = new Dictionary<char, Color>
        {
            ['#'] = new Color(0.4f, 0.4f, 0.4f), // Wall
            ['D'] = new Color(0.6f, 0.4f, 0.2f), // Door
            ['.'] = new Color(0.3f, 0.5f, 0.7f), // Interior (light blue)
            ['o'] = new Color(0.15f, 0.25f, 0.35f), // Outside (dark blue)
            ['@'] = new Color(1f, 0.9f, 0.3f), // Pawn
        };
        
        private static readonly Dictionary<char, Color> coverColors = new Dictionary<char, Color>
        {
            ['█'] = new Color(0.2f, 0.2f, 0.2f), // Full cover (dark gray)
            ['▒'] = new Color(0.5f, 0.5f, 0.5f), // Partial cover (medium gray)
            ['.'] = new Color(0.9f, 0.9f, 0.9f), // No cover (white)
            ['@'] = new Color(1f, 0.9f, 0.3f), // Pawn (yellow)
        };
        
        public override Vector2 InitialSize => new Vector2(900f, 700f);
        
        public SpatialVisionWindow()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            
            // Initialize region to center of map if available
            if (Find.CurrentMap != null)
            {
                var map = Find.CurrentMap;
                regionX = Math.Max(0, (map.Size.x / 2) - 25);
                regionZ = Math.Max(0, (map.Size.z / 2) - 25);
            }
            
            RefreshGrid();
        }
        
        private void RefreshGrid()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                cachedGrid = "No active map";
                return;
            }
            
            try
            {
                // Create a minimal JSONNode args
                var args = new JSONObject();
                args["x"] = regionX.ToString();
                args["z"] = regionZ.ToString();
                args["width"] = regionW.ToString();
                args["height"] = regionH.ToString();
                
                cachedGrid = MapTools.GetMapRegion(args);
                lastRefreshTick = Find.TickManager.TicksGame;
            }
            catch (Exception ex)
            {
                cachedGrid = $"Error: {ex.Message}";
            }
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            // Auto-refresh check
            if (autoRefresh && Find.TickManager.TicksGame - lastRefreshTick > 60)
            {
                RefreshGrid();
            }
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Spatial Vision - AI Map View Debug");
            Text.Font = GameFont.Small;
            
            // Tab bar
            var tabRect = new Rect(0f, 35f, inRect.width, 30f);
            DrawTabs(tabRect);
            
            // Controls
            var controlsRect = new Rect(0f, 70f, inRect.width, 100f);
            DrawControls(controlsRect);
            
            // Grid visualization (left side)
            var gridRect = new Rect(0f, 175f, inRect.width - 200f, inRect.height - 180f);
            DrawGrid(gridRect);
            
            // Legend (right side)
            var legendRect = new Rect(inRect.width - 195f, 175f, 195f, inRect.height - 180f);
            DrawLegend(legendRect);
        }
        
        private void DrawTabs(Rect rect)
        {
            var buttonWidth = rect.width / tabNames.Length;
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                var buttonRect = new Rect(rect.x + i * buttonWidth, rect.y, buttonWidth, rect.height);
                bool isActive = activeTab == i;
                
                GUI.color = isActive ? new Color(0.3f, 0.5f, 0.7f) : new Color(0.2f, 0.2f, 0.2f);
                Widgets.DrawBoxSolid(buttonRect, GUI.color);
                GUI.color = Color.white;
                
                if (Widgets.ButtonText(buttonRect, tabNames[i], false))
                {
                    activeTab = i;
                }
            }
            
            GUI.color = Color.white;
        }
        
        private void DrawControls(Rect rect)
        {
            float curX = rect.x + 5f;
            float curY = rect.y + 5f;
            
            // Region info display
            Widgets.Label(new Rect(curX, curY, 150f, 20f), $"Region: ({regionX}, {regionZ}) {regionW}x{regionH}");
            curX += 160f;
            
            // Center button
            if (Widgets.ButtonText(new Rect(curX, curY, 65f, 20f), "Center"))
            {
                if (Find.CurrentMap != null)
                {
                    var camPos = Find.CameraDriver.MapPosition;
                    regionX = Math.Max(0, camPos.x - regionW / 2);
                    regionZ = Math.Max(0, camPos.z - regionH / 2);
                    RefreshGrid();
                }
            }
            curX += 70f;
            
            // Refresh button
            if (Widgets.ButtonText(new Rect(curX, curY, 70f, 20f), "Refresh"))
            {
                RefreshGrid();
            }
            
            curY += 30f;
            curX = rect.x + 5f;
            
            // Auto-refresh toggle
            Widgets.CheckboxLabeled(new Rect(curX, curY, 120f, 20f), "Auto-refresh", ref autoRefresh);
            curX += 130f;
            
            // Cell size label and value
            Widgets.Label(new Rect(curX, curY, 80f, 20f), $"Cell: {cellSize}px");
            curX += 90f;
            
            // Decrease/increase cell size
            if (Widgets.ButtonText(new Rect(curX, curY, 30f, 20f), "-"))
            {
                cellSize = Math.Max(4f, cellSize - 1f);
            }
            curX += 30f;
            
            if (Widgets.ButtonText(new Rect(curX, curY, 30f, 20f), "+"))
            {
                cellSize = Math.Min(16f, cellSize + 1f);
            }
            curX += 35f;
            
            // Map size info
            if (Find.CurrentMap != null)
            {
                var map = Find.CurrentMap;
                Widgets.Label(new Rect(curX, curY, 150f, 20f), $"Map: {map.Size.x}x{map.Size.z}");
            }
        }
        
        private void DrawGrid(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.9f));
            
            if (string.IsNullOrEmpty(cachedGrid) || cachedGrid.StartsWith("Error"))
            {
                var errorRect = rect.ContractedBy(10f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(errorRect, cachedGrid);
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            // Parse the JSON grid
            try
            {
                var json = JSONNode.Parse(cachedGrid);
                var gridNode = json["grid"];
                if (gridNode == null || !gridNode.AsArray)
                {
                    Widgets.Label(rect.ContractedBy(10f), "No grid data");
                    return;
                }
                
                var grid = gridNode.AsArray;
                if (grid == null || grid.Count == 0)
                {
                    Widgets.Label(rect.ContractedBy(10f), "No grid data");
                    return;
                }
                
                float gridHeight = grid.Count * cellSize;
                float gridWidth = grid[0].AsArray.Count * cellSize;
                
                var viewRect = new Rect(0f, 0f, gridWidth, gridHeight);
                Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
                
                float y = 0f;
                foreach (var row in grid.Children)
                {
                    var rowArray = row.AsArray;
                    if (rowArray == null) continue;
                    
                    float x = 0f;
                    
                    foreach (var cell in rowArray.Children)
                    {
                        string cellStr = cell.Value ?? " ";
                        char code = cellStr.Length > 0 ? cellStr[0] : ' ';
                        Color cellColor = GetColorForSymbol(code, activeTab);
                        
                        var cellRect = new Rect(x, y, cellSize, cellSize);
                        Widgets.DrawBoxSolid(cellRect, cellColor);
                        
                        // Draw cell border
                        Widgets.DrawBox(cellRect, 1);
                        
                        x += cellSize;
                    }
                    y += cellSize;
                }
                
                Widgets.EndScrollView();
            }
            catch (Exception ex)
            {
                Widgets.Label(rect.ContractedBy(10f), $"Parse error: {ex.Message}");
            }
        }
        
        private void DrawLegend(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.12f, 0.9f));
            
            var innerRect = rect.ContractedBy(5f);
            var viewRect = new Rect(0f, 0f, innerRect.width - 20f, 400f);
            
            Widgets.BeginScrollView(innerRect, ref scrollPosition, viewRect);
            
            float curY = 0f;
            
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, innerRect.width, 20f), "Legend");
            curY += 25f;
            Text.Font = GameFont.Small;
            
            var colors = GetColorsForTab(activeTab);
            foreach (var kvp in colors)
            {
                var colorRect = new Rect(0f, curY, 15f, 15f);
                Widgets.DrawBoxSolid(colorRect, kvp.Value);
                Widgets.DrawBox(colorRect);
                
                var labelRect = new Rect(20f, curY, innerRect.width - 20f, 15f);
                Widgets.Label(labelRect, $"{kvp.Key}");
                
                curY += 18f;
            }
            
            Widgets.EndScrollView();
        }
        
        private Color GetColorForSymbol(char code, int tab)
        {
            var colors = GetColorsForTab(tab);
            
            if (colors.TryGetValue(code, out Color c))
                return c;
            
            // Default color for unknown symbols
            return new Color(0.3f, 0.3f, 0.3f);
        }
        
        private Dictionary<char, Color> GetColorsForTab(int tab)
        {
            switch (tab)
            {
                case 0: return currentMapColors;
                case 1: return walkabilityColors;
                case 2: return buildabilityColors;
                case 3: return roomColors;
                case 4: return coverColors;
                default: return currentMapColors;
            }
        }
    }
}
