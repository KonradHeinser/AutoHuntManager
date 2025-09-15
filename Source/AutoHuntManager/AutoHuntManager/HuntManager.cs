using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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

        protected bool paused = false;
        protected bool insectsAllow = false;
        protected bool humanlikeAllow = false;
        protected bool saveConfigOnMapDestroy = true;
        protected List<Pawn> animalsToHuntCached = new List<Pawn>();
        protected List<AutoSlaughterConfig> configs = new List<AutoSlaughterConfig>();
        protected bool cacheDirty;
        protected float nutritionTarget = 5f;

        protected static List<AutoSlaughterConfig> savedConfigs = new List<AutoSlaughterConfig>();
        protected static bool savedInsectsAllow = false;
        protected bool savedHumanlikeAllow = false;
        protected List<Thing> forbiddenThings = new List<Thing>();
        protected List<Thing> forbiddenHunts = new List<Thing>();

        public HuntManager(Map map) : base(map)
        {
        }

        public List<Pawn> AnimalsToHunt
        {
            get
            {
                if (cacheDirty)
                {
                    try
                    {
                        animalsToHuntCached.Clear();

                        var animals = map.mapPawns.AllPawnsSpawned.Where(a => CanEverAutoHunt(a));
                        if (animals.EnumerableNullOrEmpty())
                            return animalsToHuntCached;

                        foreach (AutoSlaughterConfig config in configs)
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
                                        else if (config.allowSlaughterPregnant)
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
                            if (config.allowSlaughterPregnant)
                            {
                                tmpAnimalsPregnant.SortByDescending(a => a.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant).Severity);
                                tmpAnimalsFemale.AddRange(tmpAnimalsPregnant);
                                tmpAnimals.AddRange(tmpAnimalsPregnant);
                            }

                            if (config.maxFemales != -1)
                            {
                                while (tmpAnimalsFemale.Count > config.maxFemales)
                                {
                                    Pawn item = tmpAnimalsFemale.PopFront();
                                    tmpAnimals.Remove(item);
                                    animalsToHuntCached.Add(item);
                                }
                            }

                            if (config.maxFemalesYoung != -1)
                                while (tmpAnimalsFemaleYoung.Count > config.maxFemalesYoung)
                                {
                                    Pawn item2 = tmpAnimalsFemaleYoung.PopFront();
                                    tmpAnimals.Remove(item2);
                                    animalsToHuntCached.Add(item2);
                                }

                            if (config.maxMales != -1)
                                while (tmpAnimalsMale.Count > config.maxMales)
                                {
                                    Pawn item3 = tmpAnimalsMale.PopFront();
                                    tmpAnimals.Remove(item3);
                                    animalsToHuntCached.Add(item3);
                                }

                            if (config.maxMalesYoung != -1)
                                while (tmpAnimalsMaleYoung.Count > config.maxMalesYoung)
                                {
                                    Pawn item4 = tmpAnimalsMaleYoung.PopFront();
                                    tmpAnimals.Remove(item4);
                                    animalsToHuntCached.Add(item4);
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

                return animalsToHuntCached;
            }
        }

        public void ClearForbidden()
        {
            forbiddenThings.Clear();
        }

        protected bool CanEverAutoHunt(Pawn animal)
        {
            return animal.IsAnimal && map.designationManager.DesignationOn(animal) == null &&
                (animal.Faction == null || animal.Faction.def == FactionDefOf.Insect) && 
                !forbiddenHunts.Contains(animal);
        }

        public override void MapGenerated()
        {
            base.MapGenerated();
            LoadConfigs();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref paused, "paused", false);
            Scribe_Values.Look(ref insectsAllow, "insectsAllow", false);
            Scribe_Values.Look(ref humanlikeAllow, "humanlikeAllow", false);
            Scribe_Values.Look(ref saveConfigOnMapDestroy, "saveConfigOnMapDestroy", false);
            Scribe_Values.Look(ref nutritionTarget, "nutritionTarget", 5f);
            Scribe_Collections.Look(ref configs, "configs", LookMode.Deep);
            Scribe_Collections.Look(ref savedConfigs, "savedConfigs", LookMode.Deep);
            Scribe_Collections.Look(ref forbiddenThings, "forbiddenThings", LookMode.Reference);
            Scribe_Collections.Look(ref forbiddenHunts, "forbiddenHunts", LookMode.Reference);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (configs.RemoveAll(x => x.animal == null || x.animal.IsCorpse) != 0)
                    Log.Warning("Some auto-hunt configs had null animals after loading.");
                TryPopulateMissingAnimals();
                SaveConfigs();
            }
        }

        protected void TryPopulateMissingAnimals()
        {
            HashSet<ThingDef> hashSet = new HashSet<ThingDef>(configs.Select(c => c.animal));
            foreach (ThingDef allDef in DefDatabase<ThingDef>.AllDefs)
                if (allDef.race != null && allDef.race.Animal && !allDef.race.Dryad && !allDef.IsCorpse && !hashSet.Contains(allDef))
                    configs.Add(new AutoSlaughterConfig
                    {
                        animal = allDef
                    });
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            if (saveConfigOnMapDestroy)
                SaveConfigs();
        }

        protected void SaveConfigs()
        {
            savedConfigs = new List<AutoSlaughterConfig>(configs);
            savedHumanlikeAllow = humanlikeAllow;
            savedInsectsAllow = insectsAllow;
        }

        protected void LoadConfigs()
        {
            configs = new List<AutoSlaughterConfig>(savedConfigs);
            humanlikeAllow = savedHumanlikeAllow;
            insectsAllow = savedInsectsAllow;
        }

        protected void ResetConfigs()
        {
            configs = new List<AutoSlaughterConfig>();
            TryPopulateMissingAnimals();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!paused && GenTicks.IsTickInterval(map.GetHashCode(), 10000))
                DesignateHunting();
        }

        public void DesignateHunting()
        {
            // If the map doesn't generally support wild animals, then the player is on their own
            if (!map.Biome.wildAnimalsCanWanderInto)
                return;
            
            // If anything is already being hunted, let that continue before rethinking things
            if (!map.mapPawns.AllPawnsSpawned.Where(a => a.IsAnimal && map.designationManager.DesignationOn(a)?.def == DesignationDefOf.Hunt).EnumerableNullOrEmpty())
                return;

            if (!forbiddenThings.NullOrEmpty())
                forbiddenThings.RemoveWhere(t => t == null || t.Destroyed);

            float num = 0f; // Stored nutrition

            // Locate all the viable meat that we haven't already attempted to claim
            var rawMeat = map.listerThings.GetAllThings(f => !forbiddenThings.Contains(f) && f.def.IsMeat
                && (humanlikeAllow || !FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(f))
                && (insectsAllow || FoodUtility.IsInsectCorpseOrInsectMeatIngredient(f)));

            // Start with counting all the meat laying around claimed because it's the easiest to handle
            var availableMeat = rawMeat.Where(m => !m.IsForbidden(Faction.OfPlayer));
            if (!availableMeat.EnumerableNullOrEmpty())
                foreach (var meat in availableMeat)
                {
                    num += meat.GetStatValue(StatDefOf.Nutrition);
                    if (num >= nutritionTarget)
                        return;
                }

            // Locate all viable corpses that we haven't tried to claim
            var corpses = map.listerThings.GetAllThings(f => !forbiddenThings.Contains(f) &&
                f is Corpse c && c.InnerPawn.RaceProps.IsFlesh
                && (humanlikeAllow || !FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(f))
                && (insectsAllow || FoodUtility.IsInsectCorpseOrInsectMeatIngredient(f)));

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

            // Check for unclaimed meat to take
            var unclaimedMeat = rawMeat.Where(m => m.IsForbidden(Faction.OfPlayer));
            if (!unclaimedMeat.EnumerableNullOrEmpty())
                foreach (var meat in unclaimedMeat)
                {
                    num += meat.GetStatValue(StatDefOf.Nutrition);
                    meat.SetForbidden(false);
                    forbiddenThings.Add(meat);
                    if (num >= nutritionTarget)
                        return;
                }

            // Check for unclaimed corpses that can be claimed
            var unclaimedCorpses = corpses.Where(c => c.IsForbidden(Faction.OfPlayer));
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

            cacheDirty = true; // Dirty the cache at this point so we can regenerate the list

            if (!AnimalsToHunt.NullOrEmpty())
            {
                // Start with animals that are unlikely to cause issues
                var safeAnimals = AnimalsToHunt.Where(a => a.RaceProps.manhunterOnDamageChance <= 0);
                if (!safeAnimals.EnumerableNullOrEmpty())
                    foreach (var animal in safeAnimals)
                    {
                        map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                        num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                        forbiddenHunts.Add(animal);
                        if (num >= nutritionTarget)
                            return;
                    }

                // Target animals that might be dangerous, but are probably fine
                var safishAnimals = AnimalsToHunt.Where(a => a.RaceProps.manhunterOnDamageChance > 0f && a.RaceProps.manhunterOnDamageChance < 0.1f);
                if (!safishAnimals.EnumerableNullOrEmpty())
                    foreach (var animal in safishAnimals)
                    {
                        map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                        num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                        forbiddenHunts.Add(animal);
                        if (num >= nutritionTarget)
                            return;
                    }

                // Find animals that have a decent chance to be a problem, but probably aren't big predators
                var unsafeAnimals = AnimalsToHunt.Where(a => a.RaceProps.manhunterOnDamageChance >= 0.1f && a.RaceProps.manhunterOnDamageChance < 0.5f);
                if (!unsafeAnimals.EnumerableNullOrEmpty())
                    foreach (var animal in unsafeAnimals)
                    {
                        map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                        num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                        forbiddenHunts.Add(animal);
                        if (num >= nutritionTarget)
                            return;
                    }

                // Give up and see if there's big animals to attack
                var dangerousAnimals = AnimalsToHunt.Where(a => a.RaceProps.manhunterOnDamageChance >= 0.5f);
                if (!dangerousAnimals.EnumerableNullOrEmpty())
                    foreach (var animal in dangerousAnimals)
                    {
                        map.designationManager.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));
                        num += animal.GetStatValue(StatDefOf.MeatAmount) * animal.RaceProps.meatDef.GetStatValueAbstract(StatDefOf.Nutrition);
                        forbiddenHunts.Add(animal);
                        if (num >= nutritionTarget)
                            return;
                    }
            }
        }
    }
}
