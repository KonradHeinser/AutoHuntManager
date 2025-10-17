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

        public bool save = true;
        protected bool home = false;
        public bool paused = false;
        public bool insectsAllow = false;
        public bool humanlikeAllow = false;
        public bool twistedAllow = false;
        public bool includeAnimalProducts = true;

        protected List<Pawn> animalsToHuntCached = new List<Pawn>();
        protected List<Pawn> forcedAnimalsToHuntCached = new List<Pawn>();
        public List<AutoHuntConfig> configs = new List<AutoHuntConfig>();
        protected bool cacheDirty;
        public float nutritionTarget = 7.5f;

        public List<Thing> forbiddenThings = new List<Thing>();
        protected List<Pawn> forbiddenHunts = new List<Pawn>();
        
        private List<ThingDef> forbiddenAnimals = new List<ThingDef>();
        private bool forbiddenDirty = true;
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
                        foreach (var a in forbiddenHunts)
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
                        forgettable.Add(new FloatMenuOption("AutoHuntAll".Translate(), delegate
                        {
                            ForgetForbidden();
                        }));

                    if (!forbiddenThings.NullOrEmpty())
                        forgettable.Add(new FloatMenuOption("AutoHuntRawFood".Translate(), delegate
                        {
                            ForgetForbiddenThings();
                        }));

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
                    if (!configs.NullOrEmpty())
                        foreach (var config in configs)
                            anyLimits.Add(config.animal, config.AnyLimit);
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

                    foreach (AutoHuntConfig config in configs)
                    {
                        if (!config.AnyLimit)
                            continue;

                        tmpAnimals.Clear();
                        tmpAnimalsMale.Clear();
                        tmpAnimalsMaleYoung.Clear();
                        tmpAnimalsFemale.Clear();
                        tmpAnimalsFemaleYoung.Clear();
                        tmpAnimalsPregnant.Clear();
                        foreach (Pawn spawnedAnimal in animals)
                        {
                            if (spawnedAnimal.def != config.animal)
                                continue;

                            if (spawnedAnimal.gender == Gender.Male)
                                if (spawnedAnimal.ageTracker.CurLifeStage.reproductive)
                                    tmpAnimalsMale.Add(spawnedAnimal);
                                else
                                    tmpAnimalsMaleYoung.Add(spawnedAnimal);
                            else if (spawnedAnimal.gender == Gender.Female)
                                if (spawnedAnimal.ageTracker.CurLifeStage.reproductive)
                                {
                                    if (!spawnedAnimal.health.hediffSet.HasHediff(HediffDefOf.Pregnant))
                                        tmpAnimalsFemale.Add(spawnedAnimal);
                                    else
                                    {
                                        tmpAnimalsPregnant.Add(spawnedAnimal);
                                        continue;
                                    }
                                }
                                else
                                    tmpAnimalsFemaleYoung.Add(spawnedAnimal);
                            tmpAnimals.Add(spawnedAnimal);
                        }

                        tmpAnimals.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        tmpAnimalsMale.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        tmpAnimalsMaleYoung.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        tmpAnimalsFemale.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        tmpAnimalsFemaleYoung.SortByDescending(a => a.ageTracker.AgeBiologicalTicks);
                        if (config.allowHuntPregnant)
                        {
                            tmpAnimalsPregnant.SortByDescending(a => a.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant).Severity);
                            tmpAnimalsFemale.AddRange(tmpAnimalsPregnant);
                            tmpAnimals.AddRange(tmpAnimalsPregnant);
                        }

                        if (config.maxMales != -1)
                            while (tmpAnimalsMale.Count > config.maxMales)
                            {
                                Pawn a = tmpAnimalsMale.PopFront();
                                tmpAnimals.Remove(a);
                                if (config.forceHunt)
                                    forcedAnimalsToHuntCached.Add(a);
                                else
                                    animalsToHuntCached.Add(a);
                            }

                        if (config.maxFemales != -1)
                        {
                            while (tmpAnimalsFemale.Count > config.maxFemales)
                            {
                                Pawn a = tmpAnimalsFemale.PopFront();
                                tmpAnimals.Remove(a);
                                if (config.forceHunt)
                                    forcedAnimalsToHuntCached.Add(a);
                                else
                                    animalsToHuntCached.Add(a);
                            }
                        }

                        if (config.maxMalesYoung != -1)
                            while (tmpAnimalsMaleYoung.Count > config.maxMalesYoung)
                            {
                                Pawn a = tmpAnimalsMaleYoung.PopFront();
                                tmpAnimals.Remove(a);
                                if (config.forceHunt)
                                    forcedAnimalsToHuntCached.Add(a);
                                else
                                    animalsToHuntCached.Add(a);
                            }

                        if (config.maxFemalesYoung != -1)
                            while (tmpAnimalsFemaleYoung.Count > config.maxFemalesYoung)
                            {
                                Pawn a = tmpAnimalsFemaleYoung.PopFront();
                                tmpAnimals.Remove(a);
                                if (config.forceHunt)
                                    forcedAnimalsToHuntCached.Add(a);
                                else
                                    animalsToHuntCached.Add(a);
                            }

                        if (config.maxTotal == -1)
                            continue;

                        while (tmpAnimals.Count > config.maxTotal)
                        {
                            Pawn pawn = tmpAnimals.PopFront();
                            if (pawn.gender == Gender.Male)
                                if (pawn.ageTracker.CurLifeStage.reproductive)
                                    tmpAnimalsMale.Remove(pawn);
                                else
                                    tmpAnimalsMaleYoung.Remove(pawn);
                            else if (pawn.gender == Gender.Female)
                                if (pawn.ageTracker.CurLifeStage.reproductive)
                                    tmpAnimalsFemale.Remove(pawn);
                                else
                                    tmpAnimalsFemaleYoung.Remove(pawn);

                            if (config.forceHunt)
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

        public void ClearForbidden()
        {
            forbiddenThings.Clear();
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

        public int ForbiddenCount(ThingDef animal)
        {
            return forbiddenHunts.Where(a => a.def == animal && a.Spawned && !a.Fogged() && !a.Dead).Count();
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
                if (save)
                {
                    var comp = Current.Game.GetComponent<ConfigStorage>();
                    if (comp?.configs.NullOrEmpty() == false)
                    {
                        configs = new List<AutoHuntConfig>(comp.configs);
                        nutritionTarget = comp.nutritionTarget;
                        paused = comp.paused;
                        insectsAllow = comp.insectsAllow;
                        humanlikeAllow = comp.humanlikeAllow;
                        twistedAllow = comp.twistedAllow;
                        includeAnimalProducts = comp.includeAnimalProducts;
                    }
                }
            }
            TryPopulateMissingAnimals();
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            var comp = Current.Game.GetComponent<ConfigStorage>();
            if (save)
            {
                if (home)
                    comp?.SaveConfigs(configs, nutritionTarget, paused, insectsAllow, humanlikeAllow, twistedAllow, includeAnimalProducts);
            }
            else
                comp?.ResetConfigs();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref home, "home", false);
            Scribe_Values.Look(ref save, "save", true);
            Scribe_Values.Look(ref paused, "paused", false);
            Scribe_Values.Look(ref insectsAllow, "insectsAllow", false);
            Scribe_Values.Look(ref humanlikeAllow, "humanlikeAllow", false);
            Scribe_Values.Look(ref twistedAllow, "twistedAllow", false);
            Scribe_Values.Look(ref includeAnimalProducts, "includeAnimalProducts", true);
            Scribe_Values.Look(ref nutritionTarget, "nutritionTarget", 7.5f);
            Scribe_Collections.Look(ref configs, "configs", LookMode.Deep);
            Scribe_Collections.Look(ref forbiddenThings, "forbiddenThings", LookMode.Reference);
            Scribe_Collections.Look(ref forbiddenHunts, "forbiddenHunts", LookMode.Reference);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (configs.RemoveAll(x => x.animal == null || x.animal.IsCorpse) != 0)
                    Log.Warning("Some auto-hunt configs had null animals after loading.");
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
            HashSet<ThingDef> hashSet = new HashSet<ThingDef>(configs.Select(c => c.animal));
            foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
                if (allDef.race != null && allDef.race.Animal && !allDef.race.Dryad && !allDef.IsCorpse && !hashSet.Contains(allDef))
                    configs.Add(new AutoHuntConfig
                    {
                        animal = allDef
                    });
        }

        protected void ResetConfigs()
        {
            configs = new List<AutoHuntConfig>();
            TryPopulateMissingAnimals();
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
        }

        private void CleanUpForbidden()
        {
            if (!forbiddenThings.NullOrEmpty())
                forbiddenThings.RemoveWhere(t => t == null || t.Destroyed || !t.Spawned);

            if (!forbiddenHunts.NullOrEmpty())
                if (forbiddenHunts.RemoveAll(a => a == null || a.Destroyed || a.Dead || !a.Spawned) != 0)
                    forbiddenDirty = true;
        }

        public void DesignateHunting()
        {
            cacheDirty = true; // Dirty the cache so we can regenerate the list
            float num = 0f; // Nutrition recorded

            var hunting = map.mapPawns.AllPawnsSpawned.Where(a => a.IsAnimal && ValidPrey(a) &&
                map.designationManager.DesignationOn(a)?.def == DesignationDefOf.Hunt);

            CleanUpForbidden();

            if (!hunting.EnumerableNullOrEmpty())
                foreach (var animal in hunting)
                    num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);

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
            if (num >= nutritionTarget)
                return;

            // If the map doesn't generally support wild animals, then the player is on their own
            if (!map.Biome.wildAnimalsCanWanderInto)
                return;

            // Locate all the viable meat
            var rawMeat = map.listerThings.GetAllThings(f => ValidFood(f) &&
                (f.def.IsMeat || (includeAnimalProducts && (f.HasThingCategory(ThingCategoryDefOf.EggsUnfertilized) || f.HasThingCategory(AutoHuntDefOf.AnimalProductRaw)))) && 
                (twistedAllow || !ModsConfig.AnomalyActive || f.def != ThingDefOf.Meat_Twisted));

            // Start with counting all the meat laying around claimed because it's the easiest to handle
            var availableMeat = rawMeat.Where(m => !m.IsForbidden(Faction.OfPlayer));
            if (!availableMeat.EnumerableNullOrEmpty())
                foreach (var meat in availableMeat)
                {
                    num += meat.GetStatValue(StatDefOf.Nutrition) * meat.stackCount;
                    if (num >= nutritionTarget)
                        return;
                }

            // Locate all viable corpses
            var corpses = map.listerThings.GetAllThings(f => f is Corpse c && 
                c.InnerPawn.RaceProps.IsFlesh && ValidFood(f) &&
                (twistedAllow || !c.InnerPawn.RaceProps.IsAnomalyEntity));

            // Check for claimed corpses that may be waiting for butchery
            var availableCorpses = corpses.Where(c => !c.IsForbidden(Faction.OfPlayer));
            if (!availableCorpses.EnumerableNullOrEmpty())
                foreach (var thing in availableCorpses)
                {
                    var corpse = thing as Corpse;
                    num += corpse.InnerPawn.GetStatValue(StatDefOf.MeatAmount) * corpse.InnerPawn.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                    if (num >= nutritionTarget)
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
                    if (num >= nutritionTarget)
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
                    if (num >= nutritionTarget)
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
                            if (num >= nutritionTarget)
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
                            if (num >= nutritionTarget)
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
                            if (num >= nutritionTarget)
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
                            if (num >= nutritionTarget)
                                return;
                        }
                    }
            }

            bool ValidPrey(Pawn a)
            {
                return (humanlikeAllow || !a.RaceProps.Humanlike) &&
                    (insectsAllow || !a.RaceProps.Insect);
            }

            bool ValidFood(Thing t)
            {
                return (humanlikeAllow || !FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t))
                    && (insectsAllow || !FoodUtility.IsInsectCorpseOrInsectMeatIngredient(t));
            }
        }
    }
}
