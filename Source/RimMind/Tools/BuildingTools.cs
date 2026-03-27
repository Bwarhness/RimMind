using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.API;
using RimMind.Core;
using RimWorld;
using Verse;
using static RimMind.Tools.BuildingValidationTools;

namespace RimMind.Tools
{
    /// <summary>
    /// Thin forwarding class for building tools. All methods are forwarded to dedicated tool classes.
    /// Query tools → BuildingQueryTools
    /// Deconstruction tools → BuildingDeconstructTools
    /// Placement tools → BuildingPlacementTools
    /// </summary>
    public static class BuildingTools
    {
        // --- Forwarding stubs for extracted query tools ---

        public static string ListBuildable(JSONNode args)
            => BuildingQueryTools.ListBuildable(args);

        public static string GetBuildingInfo(JSONNode args)
            => BuildingQueryTools.GetBuildingInfo(args);

        public static string GetRequirements(JSONNode args)
            => BuildingQueryTools.GetRequirements(args);

        // --- Forwarding stubs for extracted deconstruction tools ---

        public static string RemoveBuilding(JSONNode args)
            => BuildingDeconstructTools.RemoveBuilding(args);

        public static string DeconstructBuilding(JSONNode args)
            => BuildingDeconstructTools.DeconstructBuilding(args);

        public static string ApproveBuildings(JSONNode args)
            => BuildingDeconstructTools.ApproveBuildings(args);

// --- Forwarding stubs for placement tools ---

        public static string PlaceBuilding(JSONNode args)
            => BuildingPlacementTools.PlaceBuilding(args);

        public static string PlaceStructure(JSONNode args)
            => BuildingPlacementTools.PlaceStructure(args);

        // --- Forwarding stub for extracted validation tool ---
        public static string CheckPlacement(JSONNode args)
            => BuildingPlacementTools.CheckPlacement(args);

    }
}
