using System.Collections.Generic;
using Verse;

namespace AutoHuntManager
{
    public class ConfigStorage : GameComponent
    {
        public ConfigStorage(Game game)
        {
            configs = new List<AutoHuntConfig>();
        }

        public List<AutoHuntConfig> configs = new List<AutoHuntConfig>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref configs, "configs", LookMode.Deep);
        }
    }
}
