using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutoHuntManager
{
    public class ConfigStorage : GameComponent
    {
        public List<AutoHuntConfig> configs = new List<AutoHuntConfig>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref configs, "configs", LookMode.Deep);
        }
    }
}
