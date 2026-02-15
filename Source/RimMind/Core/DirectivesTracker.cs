using Verse;

namespace RimMind.Core
{
    public class DirectivesTracker : GameComponent
    {
        private string playerDirectives = "";

        public static DirectivesTracker Instance => Current.Game?.GetComponent<DirectivesTracker>();

        public string PlayerDirectives
        {
            get { return playerDirectives ?? ""; }
            set { playerDirectives = value ?? ""; }
        }

        public DirectivesTracker(Game game) { }

        public void AddDirective(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!string.IsNullOrEmpty(playerDirectives))
                playerDirectives += "\n";
            playerDirectives += text.Trim();
        }

        public bool RemoveDirective(string search)
        {
            if (string.IsNullOrEmpty(search) || string.IsNullOrEmpty(playerDirectives))
                return false;

            string searchLower = search.Trim().ToLowerInvariant();
            var lines = playerDirectives.Split('\n');
            var kept = new System.Collections.Generic.List<string>();
            bool removed = false;

            foreach (var line in lines)
            {
                if (!removed && line.Trim().ToLowerInvariant().Contains(searchLower))
                {
                    removed = true;
                    continue;
                }
                kept.Add(line);
            }

            if (removed)
                playerDirectives = string.Join("\n", kept.ToArray());

            return removed;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref playerDirectives, "playerDirectives", "");
        }
    }
}
