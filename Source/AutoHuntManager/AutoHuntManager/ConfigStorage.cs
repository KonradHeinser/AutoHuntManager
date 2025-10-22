using Verse;

namespace AutoHuntManager
{
    public class ConfigStorage : GameComponent
    {
        public ConfigStorage(Game game)
        {
            ResetConfigs();
        }

        public ConfigSet prevConfigs;
        public bool paused;

        public void SaveConfigs(ConfigSet prev, bool pause)
        {
            prevConfigs = prev;
            paused = pause;
        }

        public void ResetConfigs()
        {
            prevConfigs = new ConfigSet();
            paused = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref prevConfigs, "prevConfigs");
            Scribe_Values.Look(ref paused, "paused");
        }
    }
}
