using System.Collections.Generic;
using Verse;

namespace AutoHuntManager
{
    public class ConfigSet : IExposable
    {
        public List<AutoHuntConfig> configs = new List<AutoHuntConfig>();
        public float nutritionTarget = 7.5f;
        public bool insectsAllow;
        public bool humanlikeAllow;
        public bool twistedAllow;
        public bool includeAnimalProducts = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref insectsAllow, "insectsAllow");
            Scribe_Values.Look(ref humanlikeAllow, "humanlikeAllow");
            Scribe_Values.Look(ref nutritionTarget, "nutritionTarget", 7.5f);
            Scribe_Values.Look(ref twistedAllow, "twistedAllow");
            Scribe_Values.Look(ref includeAnimalProducts, "includeAnimalProducts", true);
            Scribe_Collections.Look(ref configs, "configs", LookMode.Deep);
        }
    }
}
