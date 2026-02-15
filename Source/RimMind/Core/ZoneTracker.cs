using System;
using System.Collections.Generic;
using Verse;

namespace RimMind.Core
{
    public class LabeledZone : IExposable
    {
        public int id;
        public string label;
        public string purpose;
        public int x1, z1, x2, z2;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref purpose, "purpose");
            Scribe_Values.Look(ref x1, "x1");
            Scribe_Values.Look(ref z1, "z1");
            Scribe_Values.Look(ref x2, "x2");
            Scribe_Values.Look(ref z2, "z2");
        }

        public bool Contains(int x, int z)
        {
            return x >= Math.Min(x1, x2) && x <= Math.Max(x1, x2)
                && z >= Math.Min(z1, z2) && z <= Math.Max(z1, z2);
        }

        public int Width => Math.Abs(x2 - x1) + 1;
        public int Height => Math.Abs(z2 - z1) + 1;
        public int CellCount => Width * Height;
    }

    public class ZoneTracker : GameComponent
    {
        private List<LabeledZone> zones = new List<LabeledZone>();
        private int nextId = 1;

        public ZoneTracker(Game game) { }

        public List<LabeledZone> Zones => zones;

        public LabeledZone AddZone(string label, string purpose, int x1, int z1, int x2, int z2)
        {
            var zone = new LabeledZone
            {
                id = nextId++,
                label = label,
                purpose = purpose,
                x1 = Math.Min(x1, x2),
                z1 = Math.Min(z1, z2),
                x2 = Math.Max(x1, x2),
                z2 = Math.Max(z1, z2)
            };
            zones.Add(zone);
            return zone;
        }

        public bool RemoveZone(int id)
        {
            return zones.RemoveAll(z => z.id == id) > 0;
        }

        public bool RemoveZoneByLabel(string label)
        {
            return zones.RemoveAll(z => z.label.Equals(label, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        public LabeledZone GetZoneAt(int x, int z)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Contains(x, z))
                    return zones[i];
            }
            return null;
        }

        public LabeledZone GetZoneByLabel(string label)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].label.Equals(label, StringComparison.OrdinalIgnoreCase))
                    return zones[i];
            }
            return null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref zones, "rimmindZones", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "rimmindNextZoneId", 1);
            if (zones == null) zones = new List<LabeledZone>();
        }

        public static ZoneTracker Instance => Current.Game?.GetComponent<ZoneTracker>();
    }
}
