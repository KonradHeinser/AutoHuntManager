using System.Collections.Generic;
using Verse;

namespace AutoHuntManager
{
    public class ConfigStorage : GameComponent
    {
        public ConfigStorage(Game game)
        {
            ResetConfigs();
        }

        public List<AutoHuntConfig> configs = new List<AutoHuntConfig>();
        public bool paused = false;
        public bool insectsAllow = false;
        public bool humanlikeAllow = false;
        public bool twistedAllow = false;
        public bool includeAnimalProducts = true;
        public int nutritionTarget = 15;

        public void SaveConfigs(List<AutoHuntConfig> configs, int nutritionTarget, bool paused, bool insectsAllow, bool humanlikeAllow, bool twistedAllow, bool includeAnimalProducts)
        {
            this.configs = configs;
            this.nutritionTarget = nutritionTarget;
            this.paused = paused;
            this.insectsAllow = insectsAllow;
            this.humanlikeAllow = humanlikeAllow;
            this.twistedAllow = twistedAllow;
            this.includeAnimalProducts = includeAnimalProducts;
        }

        public void ResetConfigs()
        {
            configs = new List<AutoHuntConfig>();
            paused = false;
            insectsAllow = false;
            humanlikeAllow = false;
            twistedAllow = false;
            includeAnimalProducts = true;
            nutritionTarget = 15;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref paused, "paused", false);
            Scribe_Values.Look(ref insectsAllow, "insectsAllow", false);
            Scribe_Values.Look(ref humanlikeAllow, "humanlikeAllow", false);
            Scribe_Values.Look(ref nutritionTarget, "nutritionTarget", 15);
            Scribe_Values.Look(ref twistedAllow, "twistedAllow", false);
            Scribe_Values.Look(ref includeAnimalProducts, "includeAnimalProducts", true);
            Scribe_Collections.Look(ref configs, "configs", LookMode.Deep);
        }
    }
}
