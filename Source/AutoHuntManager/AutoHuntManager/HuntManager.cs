using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AutoHuntManager
{
    public class HuntManager : MapComponent
    {
        protected List<Pawn> tmpAnimals = new List<Pawn>();
        protected List<Pawn> tmpAnimalsMale = new List<Pawn>();
        protected List<Pawn> tmpAnimalsMaleYoung = new List<Pawn>();
        protected List<Pawn> tmpAnimalsFemale = new List<Pawn>();
        protected List<Pawn> tmpAnimalsFemaleYoung = new List<Pawn>();
        protected List<Pawn> tmpAnimalsPregnant = new List<Pawn>();

        protected bool home;
        public bool paused;
        public bool save = true;
        protected List<Pawn> animalsToHuntCached = new List<Pawn>();
        protected List<Pawn> forcedAnimalsToHuntCached = new List<Pawn>();
        protected bool cacheDirty;
        public ConfigSet config = new ConfigSet();

        public List<Thing> forbiddenThings = new List<Thing>();
        protected List<Pawn> forbiddenHunts = new List<Pawn>();

        public void ResetConfigs()
        {
            config = new ConfigSet();
            TryPopulateMissingAnimals();
        }
        
        private List<ThingDef> forbiddenAnimals = new List<ThingDef>();
        public bool forbiddenDirty = true;
        public List<ThingDef> ForbiddenAnimals
        {
            get
            {
                if (forbiddenDirty)
                {
                    forbiddenDirty = false;
                    forgettableDirty = true;
                    forbiddenAnimals.Clear();
                    if (!forbiddenHunts.NullOrEmpty())
                        foreach (var a in forbiddenHunts.Where(a => !map.designationManager.HasMapDesignationOn(a)))
                            if (!forbiddenAnimals.Contains(a.def))
                                forbiddenAnimals.Add(a.def);
                }
                return forbiddenAnimals;
            }
        }

        private List<FloatMenuOption> forgettable = new List<FloatMenuOption>();
        public bool forgettableDirty = true;
        public List<FloatMenuOption> Forgettable
        {
            get
            {
                if (forgettableDirty)
                {
                    CleanUpForbidden();
                    forgettableDirty = false;
                    forgettable.Clear();
                    if (!ForbiddenAnimals.NullOrEmpty() || !forbiddenThings.NullOrEmpty())
                        forgettable.Add(new FloatMenuOption("AutoHuntAll".Translate(), ForgetForbidden));

                    if (!forbiddenThings.NullOrEmpty())
                        forgettable.Add(new FloatMenuOption("AutoHuntRawFood".Translate(), ForgetForbiddenThings));

                    if (!ForbiddenAnimals.NullOrEmpty())
                    {
                        var sortedAnimals = new List<ThingDef>(ForbiddenAnimals);
                        sortedAnimals.SortBy(a => a.label);
                        foreach (var animal in sortedAnimals)
                            forgettable.Add(new FloatMenuOption(animal.LabelCap, delegate
                            {
                                NarrowForbidForget(animal);
                            }));
                    }
                }
                return forgettable;
            }
        }

        private Dictionary<ThingDef, bool> anyLimits = new Dictionary<ThingDef, bool>();
        private bool limitsDirty = true;
        public Dictionary<ThingDef, bool> AnyLimits
        {
            get
            {
                if (limitsDirty)
                {
                    limitsDirty = false;
                    anyLimits.Clear();
                    if (!config.configs.NullOrEmpty())
                        foreach (var c in config.configs)
                            anyLimits.Add(c.animal, c.AnyLimit);
                }
                return anyLimits;
            }
        }


        public HuntManager(Map map) : base(map)
        {
        }

        public void CheckAnimals() // Builds the two lists
        {
            if (cacheDirty)
            {
                try
                {
                    CleanUpForbidden();
                    animalsToHuntCached.Clear();
                    forcedAnimalsToHuntCached.Clear();
                    var animals = map.mapPawns.AllPawnsSpawned.Where(a => CanEverAutoHunt(a));
                    if (animals.EnumerableNullOrEmpty())
                        return;

                    foreach (var c in config.configs.Where(config => config.AnyLimit))
                    {
                        tmpAnimals.Clear();
                        tmpAnimalsMale.Clear();
                        tmpAnimalsMaleYoung.Clear();
                        tmpAnimalsFemale.Clear();
                        tmpAnimalsFemaleYoung.Clear();
                        tmpAnimalsPregnant.Clear();
                        foreach (var spawnedAnimal in animals)
                        {
                            if (spawnedAnimal.def != c.animal)
                                continue;

                            switch (spawnedAnimal.gender)
                            {
                                case Gender.Male when spawnedAnimal.ageTracker.CurLifeStage.reproductive:
                                    tmpAnimalsMale.Add(spawnedAnimal);
                                    break;
                                case Gender.Male:
                                    tmpAnimalsMaleYoung.Add(spawnedAnimal);
                                    break;
                                case Gender.Female when spawnedAnimal.ageTracker.CurLifeStage.reproductive:
                                {
                                    if (!spawnedAnimal.health.hediffSet.HasHediff(HediffDefOf.Pregnant))
                                        tmpAnimalsFemale.Add(spawnedAnimal);
                                    else
                                    {
                                        tmpAnimalsPregnant.Add(spawnedAnimal);
                                        continue;
                                    }

                                    break;
                                }
                                case Gender.Female:
                                    tmpAnimalsFemaleYoung.Add(spawnedAnimal);
                                    break;
                                case Gender.None:
                                default:
                                    break;
                            }
                            tmpAnimals.Add(spawnedAnimal);
                        }

                        tmpAnimals.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        tmpAnimalsMale.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        tmpAnimalsMaleYoung.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        tmpAnimalsFemale.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        tmpAnimalsFemaleYoung.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        if (c.allowHuntPregnant)
                        {
                            tmpAnimalsPregnant.SortByDescending(a => a.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant).Severity);
                            tmpAnimalsFemale.AddRange(tmpAnimalsPregnant);
                            tmpAnimals.AddRange(tmpAnimalsPregnant);
                        }

                        if (c.maxMales != -1)
                            while (tmpAnimalsMale.Count > c.maxMales)
                            {
                                var a = tmpAnimalsMale.PopFront();
                                tmpAnimals.Remove(a);
                                if (c.forceHunt)
                                    forcedAnimalsToHuntCached.Add(a);
                                else
                                    animalsToHuntCached.Add(a);
                            }

                        if (c.maxFemales != -1)
                        {
                            while (tmpAnimalsFemale.Count > c.maxFemales)
                            {
                                var a = tmpAnimalsFemale.PopFront();
                                tmpAnimals.Remove(a);
                                if (c.forceHunt)
                                    forcedAnimalsToHuntCached.Add(a);
                                else
                                    animalsToHuntCached.Add(a);
                            }
                        }

                        if (c.maxMalesYoung != -1)
                            while (tmpAnimalsMaleYoung.Count > c.maxMalesYoung)
                            {
                                var a = tmpAnimalsMaleYoung.PopFront();
                                tmpAnimals.Remove(a);
                                if (c.forceHunt)
                                    forcedAnimalsToHuntCached.Add(a);
                                else
                                    animalsToHuntCached.Add(a);
                            }

                        if (c.maxFemalesYoung != -1)
                            while (tmpAnimalsFemaleYoung.Count > c.maxFemalesYoung)
                            {
                                var a = tmpAnimalsFemaleYoung.PopFront();
                                tmpAnimals.Remove(a);
                                if (c.forceHunt)
                                    forcedAnimalsToHuntCached.Add(a);
                                else
                                    animalsToHuntCached.Add(a);
                            }

                        if (c.maxTotal == -1)
                            continue;

                        while (tmpAnimals.Count > c.maxTotal)
                        {
                            var pawn = tmpAnimals.PopFront();
                            switch (pawn.gender)
                            {
                                case Gender.Male when pawn.ageTracker.CurLifeStage.reproductive:
                                    tmpAnimalsMale.Remove(pawn);
                                    break;
                                case Gender.Male:
                                    tmpAnimalsMaleYoung.Remove(pawn);
                                    break;
                                case Gender.Female when pawn.ageTracker.CurLifeStage.reproductive:
                                    tmpAnimalsFemale.Remove(pawn);
                                    break;
                                case Gender.Female:
                                    tmpAnimalsFemaleYoung.Remove(pawn);
                                    break;
                                case Gender.None:
                                default:
                                    break;
                            }

                            if (c.forceHunt)
                                forcedAnimalsToHuntCached.Add(pawn);
                            else
                                animalsToHuntCached.Add(pawn);
                        }
                    }

                    cacheDirty = false;
                }
                finally
                {
                    tmpAnimals.Clear();
                    tmpAnimalsMale.Clear();
                    tmpAnimalsFemale.Clear();
                    tmpAnimalsMaleYoung.Clear();
                    tmpAnimalsFemaleYoung.Clear();
                    tmpAnimalsPregnant.Clear();
                }
            }
        }

        public List<Pawn> ForcedAnimalsToHunt
        {
            get
            {
                CheckAnimals();
                return forcedAnimalsToHuntCached;
            }
        }

        public List<Pawn> AnimalsToHunt
        {
            get
            {
                CheckAnimals();
                return animalsToHuntCached;
            }
        }

        public bool CanEverAutoHunt(Pawn animal, bool forDisplay = false)
        {
            if (animal?.IsAnimal != true) 
                return false;


            if (animal.Faction != null && animal.Faction != Faction.OfInsects)
                return false;

            if (!animal.Spawned || animal.Fogged())
                return false;

            if (!forDisplay)
            {
                if (forbiddenHunts.Contains(animal))
                    return false;

                if (map.designationManager.HasMapDesignationOn(animal))
                    return false;
            }
            else
            {
                if (forbiddenHunts.Contains(animal) && map.designationManager.DesignationOn(animal)?.def != DesignationDefOf.Hunt)
                    return false;
            }

            return true;
        }

        public void ForbidHunt(Pawn animal)
        {
            forbiddenHunts.Add(animal);
            forbiddenDirty = true;
        }

        public override void MapGenerated()
        {
            base.MapGenerated();
            if (map.IsPlayerHome)
            {
                home = true;
                if (save && GenTicks.TicksGame > 600)
                {
                    // Rework game component and configs saving to be at the mod level instead of the game level
                    var comp = Current.Game.GetComponent<ConfigStorage>();
                    if (comp != null)
                    {
                        config = comp.prevConfigs;
                        paused = comp.paused;
                    }
                }
            }
            TryPopulateMissingAnimals();
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            var comp = Current.Game.GetComponent<ConfigStorage>();
            if (save) // As part of the mod stuff, add a previous config section to load
            {
                if (home)
                    comp?.SaveConfigs(config, paused);
            }
            else
                comp?.ResetConfigs();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref home, "home");
            Scribe_Deep.Look(ref config, "config");
            Scribe_Values.Look(ref paused, "paused");
            Scribe_Collections.Look(ref forbiddenThings, "forbiddenThings", LookMode.Reference);
            Scribe_Collections.Look(ref forbiddenHunts, "forbiddenHunts", LookMode.Reference);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                TryPopulateMissingAnimals();
                CleanUpForbidden();
            }
        }
        
        public void ForgetForbidden()
        {
            forgettableDirty = true;
            forbiddenDirty = true;
            forbiddenHunts.Clear();
            forbiddenThings.Clear();
        }

        public void NarrowForbidForget(ThingDef animal)
        {
            forgettableDirty = true;
            forbiddenDirty = true;
            forbiddenHunts.RemoveWhere(a => a.def == animal);
        }

        public void ForgetForbiddenThings()
        {
            forgettableDirty = true;
            forbiddenThings.Clear();
        }

        protected void TryPopulateMissingAnimals()
        {
            var hashSet = new HashSet<ThingDef>(config.configs.Select(c => c.animal));
            foreach (var allDef in DefDatabase<ThingDef>.AllDefs.Where(a => a.race?.Animal == true && !a.race.Dryad && !a.IsCorpse && !hashSet.Contains(a)))
                config.configs.Add(new AutoHuntConfig
                {
                    animal = allDef
                });
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!paused && GenTicks.IsTickInterval(map.GetHashCode(), 15000))
                DesignateHunting();
        }

        public void PauseToggle()
        {
            paused = !paused;
        }

        public void Notify_ConfigChanged()
        {
            limitsDirty = true;
            cacheDirty = true;
            forbiddenDirty = true;
        }

        private void CleanUpForbidden()
        {
            if (!forbiddenThings.NullOrEmpty())
                forbiddenThings.RemoveWhere(t => t == null || t.Destroyed || !t.Spawned);

            if (!forbiddenHunts.NullOrEmpty())
                if (forbiddenHunts.RemoveAll(a => a == null || a.Destroyed || a.Dead || !a.Spawned || (a.Faction != null && a.Faction != Faction.OfInsects)) != 0)
                    forbiddenDirty = true;
        }

        public void DesignateHunting()
        {
            cacheDirty = true; // Dirty the cache so we can regenerate the list
            var num = 0f; // Nutrition recorded

            var hunting = map.mapPawns.AllPawnsSpawned.Where(a => a.IsAnimal && ValidPrey(a) &&
                map.designationManager.DesignationOn(a)?.def == DesignationDefOf.Hunt);

            CleanUpForbidden();

            if (!hunting.EnumerableNullOrEmpty()) 
                num += hunting.Sum(animal => animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition));

            // See if there are any animals we absolutely need to hunt no matter what
            if (!ForcedAnimalsToHunt.NullOrEmpty())
                foreach (var animal in ForcedAnimalsToHunt)
                {
                    map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                    if (ValidPrey(animal))
                        num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                    ForbidHunt(animal);
                }

            // If existing hunts are enough to put us above the target, don't bother checking anything else
            if (num >= config.nutritionTarget)
                return;

            // If the map doesn't generally support wild animals, then the player is on their own
            if (!map.Biome.wildAnimalsCanWanderInto)
                return;

            // Locate all the viable meat
            var rawMeat = map.listerThings.GetAllThings(f => ValidFood(f) &&
                (f.def.IsMeat || (config.includeAnimalProducts && (f.HasThingCategory(ThingCategoryDefOf.EggsUnfertilized) || f.HasThingCategory(AutoHuntDefOf.AnimalProductRaw)))) && 
                (config.twistedAllow || !ModsConfig.AnomalyActive || f.def != ThingDefOf.Meat_Twisted));

            // Start with counting all the meat lying around claimed because it's the easiest to handle
            var availableMeat = rawMeat.Where(m => !m.IsForbidden(Faction.OfPlayer));
            if (!availableMeat.EnumerableNullOrEmpty())
                foreach (var meat in availableMeat)
                {
                    num += meat.GetStatValue(StatDefOf.Nutrition) * meat.stackCount;
                    if (num >= config.nutritionTarget)
                        return;
                }

            // Locate all viable corpses
            var corpses = map.listerThings.GetAllThings(f => f is Corpse c && 
                                                             c.GetRotStage() == RotStage.Fresh &&
                                                            c.InnerPawn.RaceProps.IsFlesh && ValidFood(f) &&
                                                            (config.twistedAllow || !c.InnerPawn.RaceProps.IsAnomalyEntity));

            // Check for claimed corpses that may be waiting for butchery
            var availableCorpses = corpses.Where(c => !c.IsForbidden(Faction.OfPlayer));
            if (!availableCorpses.EnumerableNullOrEmpty())
                foreach (var thing in availableCorpses)
                {
                    var corpse = thing as Corpse;
                    num += corpse.InnerPawn.GetStatValue(StatDefOf.MeatAmount) * corpse.InnerPawn.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                    if (num >= config.nutritionTarget)
                        return;
                }

            // Check for new unclaimed meat to take
            var unclaimedMeat = rawMeat.Where(m => m.IsForbidden(Faction.OfPlayer) && !forbiddenThings.Contains(m));
            if (!unclaimedMeat.EnumerableNullOrEmpty())
                foreach (var meat in unclaimedMeat)
                {
                    num += meat.GetStatValue(StatDefOf.Nutrition) * meat.stackCount;
                    meat.SetForbidden(false);
                    forbiddenThings.Add(meat);
                    if (num >= config.nutritionTarget)
                        return;
                }

            // Check for new unclaimed corpses that can be claimed
            var unclaimedCorpses = corpses.Where(c => c.IsForbidden(Faction.OfPlayer) && !forbiddenThings.Contains(c));
            if (!unclaimedCorpses.EnumerableNullOrEmpty())
                foreach (var thing in unclaimedCorpses)
                {
                    var corpse = thing as Corpse;
                    num += corpse.InnerPawn.GetStatValue(StatDefOf.MeatAmount) * corpse.InnerPawn.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                    corpse.SetForbidden(false);
                    forbiddenThings.Add(corpse);
                    if (num >= config.nutritionTarget)
                        return;
                }

            if (!AnimalsToHunt.NullOrEmpty())
            {
                // Categorize animals based on chance to fight back
                var safeAnimals = new List<Pawn>();
                var safishAnimals = new List<Pawn>();
                var unsafeAnimals = new List<Pawn>();
                var dangerousAnimals = new List<Pawn>();

                foreach (var a in AnimalsToHunt)
                {
                    if (a.RaceProps.manhunterOnDamageChance <= 0)
                        safeAnimals.Add(a);
                    else if (a.RaceProps.manhunterOnDamageChance < 0.1f)
                        safishAnimals.Add(a);
                    else if (a.RaceProps.manhunterOnDamageChance < 0.5f)
                        unsafeAnimals.Add(a);
                    else
                        dangerousAnimals.Add(a);
                }
                // Start with animals that are unlikely to cause issues
                if (!safeAnimals.EnumerableNullOrEmpty())
                    foreach (var animal in safeAnimals)
                    {
                        map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                        ForbidHunt(animal);
                        if (ValidPrey(animal))
                        {
                            num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                            if (num >= config.nutritionTarget)
                                return;
                        }
                    }

                // Target animals that might be dangerous, but are probably fine
                if (!safishAnimals.EnumerableNullOrEmpty())
                    foreach (var animal in safishAnimals)
                    {
                        map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                        ForbidHunt(animal);
                        if (ValidPrey(animal))
                        {
                            num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                            if (num >= config.nutritionTarget)
                                return;
                        }
                    }

                // Find animals that have a decent chance to be a problem, but probably aren't big predators
                if (!unsafeAnimals.EnumerableNullOrEmpty())
                    foreach (var animal in unsafeAnimals)
                    {
                        map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                        ForbidHunt(animal);
                        if (ValidPrey(animal))
                        {
                            num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                            if (num >= config.nutritionTarget)
                                return;
                        }
                    }

                // Give up and see if there's big animals to attack
                if (!dangerousAnimals.EnumerableNullOrEmpty())
                    foreach (var animal in dangerousAnimals)
                    {
                        map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                        ForbidHunt(animal);
                        if (ValidPrey(animal))
                        {
                            num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                            if (num >= config.nutritionTarget)
                                return;
                        }
                    }
            }

            bool ValidPrey(Pawn a)
            {
                return (config.humanlikeAllow || !a.RaceProps.Humanlike) &&
                    (config.insectsAllow || !a.RaceProps.Insect);
            }

            bool ValidFood(Thing t)
            {
                return (config.humanlikeAllow || !FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t))
                    && (config.insectsAllow || !FoodUtility.IsInsectCorpseOrInsectMeatIngredient(t));
            }
        }
    }
}
