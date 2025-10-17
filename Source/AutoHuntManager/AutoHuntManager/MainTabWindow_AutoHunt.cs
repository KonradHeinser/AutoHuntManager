using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AutoHuntManager
{
    public class MainTabWindow_AutoHunt : MainTabWindow
    {
        private enum ConfigDisplay
        {
            All,
            Configured,
            Unconfigured
        }

        private ConfigDisplay curDisplay;

        private struct AnimalCountRecord
        {
            public int total;

            public int male;

            public int maleYoung;

            public int female;

            public int femaleYoung;

            public int pregnant;

            public AnimalCountRecord(int total, int male, int maleYoung, int female, int femaleYoung, int pregnant)
            {
                this.total = total;
                this.male = male;
                this.maleYoung = maleYoung;
                this.female = female;
                this.femaleYoung = femaleYoung;
                this.pregnant = pregnant;
            }
        }

        protected HuntManager manager = null;

        protected HuntManager Manager
        {
            get
            {
                if (manager == null)
                    manager = Find.CurrentMap?.GetComponent<HuntManager>();
                return manager;
            }
        }

        private Vector2 scrollPos;

        private Rect viewRect;

        private Dictionary<ThingDef, AnimalCountRecord> animalCounts = new Dictionary<ThingDef, AnimalCountRecord>();

        private List<AutoHuntConfig> configsOrdered = new List<AutoHuntConfig>();

        private List<Rect> tmpMouseoverHighlightRects = new List<Rect>();

        private List<Rect> tmpGroupRects = new List<Rect>();

        public bool hideInsect = false;

        public bool hideAbsent = true;

        public override Vector2 RequestedTabSize => new Vector2(1000f, Mathf.Min(configsOrdered.Count * 30f + 210f, UI.screenHeight));

        public override void PreOpen()
        {
            base.PreOpen();
            if (Manager != null)
                RecalculateAnimals();
            else
                Log.Error("Attempted to open the auto hunt manager without the map component available");
        }

        private void RecalculateAnimals()
        {
            animalCounts.Clear();
            var animals = Find.CurrentMap.mapPawns.AllPawnsSpawned.Where(a => Manager.CanEverAutoHunt(a, true)).ToList();

            foreach (AutoHuntConfig config in Manager.configs)
            {
                AnimalCountRecord value = default;
                CountAnimals(animals, config, config.animal, out value.male, out value.maleYoung, out value.female, out value.femaleYoung, out value.total, out value.pregnant);
                animalCounts.Add(config.animal, value);
            }
            configsOrdered = (from c in Manager.configs 
                              where CanDisplay(c.animal) && (!hideAbsent || animalCounts[c.animal].total > 0) && (!hideInsect || !c.animal.race.Insect)
                              orderby animalCounts[c.animal].total descending, c.animal.label
                              select c).ToList();

            bool CanDisplay(ThingDef animal)
            {
                switch (curDisplay)
                {
                    case ConfigDisplay.Configured:
                        return Manager.AnyLimits[animal];
                    case ConfigDisplay.Unconfigured:
                        return !Manager.AnyLimits[animal];
                    default:
                        return true;
                }
            }
            Manager.forgettableDirty = true;
            SetInitialSizeAndPosition();
        }

        private float labelWidth = 100f;

        public override void DoWindowContents(Rect inRect)
        {
            if (Manager == null)
            {
                Close();
                return;
            }

            Rect rect = new Rect(inRect);
            rect.yMax -= CloseButSize.y + 60f;
            rect.yMin += 6f;
            Listing_Standard listing_Standard = new Listing_Standard(rect, () => scrollPos)
            {
                ColumnWidth = inRect.width - 20f
            };
            viewRect = new Rect(0f, 0f, rect.width - 16f, 30f * (float)(configsOrdered.Count) + 65f);
            labelWidth = viewRect.width - 840f;

            Rect other = new Rect(rect)
            {
                x = scrollPos.x,
                y = scrollPos.y
            };
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            listing_Standard.Begin(viewRect);
            DoAnimalHeader(listing_Standard.GetRect(24f), listing_Standard.GetRect(24f));
            int num = 0;
            if (!configsOrdered.NullOrEmpty())
                foreach (AutoHuntConfig item in configsOrdered)
                {
                    listing_Standard.Gap(6f);
                    Rect rect2 = listing_Standard.GetRect(24f);
                    if (rect2.Overlaps(other))
                        DoAnimalRow(rect2, item, num);
                    num++;
                }

            listing_Standard.End();
            Widgets.EndScrollView();
            Color color = GUI.color;
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = (TextAnchor)3;
            GUI.color = Color.gray;

            Rect lower = new Rect(inRect) // The lower area of the manager which will be split up
            {
                yMin = rect.yMax + 5f,
                xMax = inRect.xMax + 5f // Slight increase in width because we don't have to worry about the bar
            };

            GUI.color = color;

            // Pause, Start Search, and Forget Forbidden
            float gapAmount = 30f;
            Widgets.BeginGroup(lower);
            var actionWidth = Mathf.Min(100f, lower.width / 4f);
            WidgetRow pause = new WidgetRow(0f, 0f);
            if (pause.ButtonText(Manager.paused ? "Paused".Translate().CapitalizeFirst() : "Searching".Translate(), "AutoHuntPauseTooltip".Translate(), fixedWidth: actionWidth))
                Manager.PauseToggle();

            WidgetRow search = new WidgetRow(0f, 35f);
            if (search.ButtonText("AutoHuntDoCheck".Translate(), "AutoHuntDoCheckTooltip".Translate(), fixedWidth: actionWidth))
                Manager.DesignateHunting();

            WidgetRow forget = new WidgetRow(0f, 70f);
            if (Manager.Forgettable.NullOrEmpty())
                GUI.color = Color.gray;
            if (forget.ButtonText("AutoHuntForget".Translate(), "AutoHuntForgetTooltip".Translate(), true, true, !Manager.Forgettable.NullOrEmpty(), actionWidth))
                Find.WindowStack.Add(new FloatMenu(Manager.Forgettable));
            GUI.color = color;

            Widgets.EndGroup();

            lower.xMin += actionWidth + gapAmount;
            var width = lower.width / 3f;

            // Hiding options on the left
            Rect display = new Rect(lower)
            {
                xMax = lower.xMin + width
            };
            lower.xMin += width; // Remove the display from the remaining space

            Widgets.BeginGroup(display);
            CheckBox("AutoHuntHideAbsent".Translate(), "AutoHuntHideAbsentTooltip".Translate(), 0f, ref hideAbsent, true);
            CheckBox("AutoHuntHideInsect".Translate(), "AutoHuntHideInsectTooltip".Translate(), 35f, ref hideInsect, true);

            WidgetRow cycle = new WidgetRow(0f, 70f);
            string text = string.Empty;
            ConfigDisplay next = ConfigDisplay.All;
            switch (curDisplay)
            {
                case ConfigDisplay.All:
                    text = "AutoHuntAll".Translate();
                    next = ConfigDisplay.Configured;
                    break;
                case ConfigDisplay.Configured:
                    text = "AutoHuntConfigured".Translate();
                    next = ConfigDisplay.Unconfigured; 
                    break;
                case ConfigDisplay.Unconfigured:
                    text = "AutoHuntUnconfigured".Translate();
                    next = ConfigDisplay.All;
                    break;
            }
            cycle.Gap(gapAmount);
            if (cycle.ButtonText(text, "AutoHuntDisplayCycle".Translate(), fixedWidth: display.width - gapAmount - 10f))
            {
                curDisplay = next;
                RecalculateAnimals();
            }

            Widgets.EndGroup();

            // Basic Configurations in the middle
            Rect basicConfigs = new Rect(lower)
            {
                xMax = lower.xMin + width
            };
            lower.xMin = basicConfigs.xMax; // Remove the basic configs rect from the remaining space
            Widgets.BeginGroup(basicConfigs);
            WidgetRow nutrition = new WidgetRow(0f, 0f);
            nutrition.Gap(gapAmount);
            var nutLabel = nutrition.Label("Nutrition".Translate(), basicConfigs.width - gapAmount - 30f, "AutoHuntNutritionTooltip".Translate());
            var nutFieldRect = new Rect()
            {
                xMin = nutLabel.xMax,
                xMax = nutLabel.xMax + 30f,
                yMin = nutLabel.yMin,
                yMax = nutLabel.yMax
            };
            float nut = Manager.nutritionTarget;
            string buffer = nut.ToString();
            Widgets.TextFieldNumeric<float>(nutFieldRect, ref nut, ref buffer, 0f, 99f);
            Manager.nutritionTarget = nut;

            CheckBox("AutoHuntIncludeProducts".Translate(), "AutoHuntIncludeProductsTooltip".Translate(), 35f, ref Manager.includeAnimalProducts);
            CheckBox("AutoHuntRememberConfigs".Translate(), "AutoHuntRememberConfigsTooltip".Translate(), 70f, ref Manager.save);
            Widgets.EndGroup();

            // Include Humanlike, Insect, and Twisted on the right
            Widgets.BeginGroup(lower); // lower is already set to the rightmost area at this point
            CheckBox("AutoHuntIncludeInsects".Translate(), "AutoHuntIncludeInsectsTooltip".Translate(), 0f, ref Manager.insectsAllow);
            CheckBox("AutoHuntIncludeHumanlike".Translate(), "AutoHuntIncludeHumanlikeTooltip".Translate(), 35f, ref Manager.humanlikeAllow);
            if (ModsConfig.AnomalyActive)
                CheckBox("AutoHuntIncludeTwisted".Translate(), "AutoHuntIncludeTwistedTooltip".Translate(), 70f, ref Manager.twistedAllow);
            Widgets.EndGroup();

            Text.Font = font;
            Text.Anchor = anchor;

            void CheckBox(string label, string tooltip, float y, ref bool toggle, bool recalc = false)
            {
                WidgetRow row = new WidgetRow(0f, y);
                row.Gap(gapAmount);
                row.Label(label, width - 25f - gapAmount, tooltip);
                bool old = toggle;
                Widgets.Checkbox(row.FinalX, row.FinalY, ref toggle, paintable: true);
                if (recalc && toggle != old)
                    RecalculateAnimals();
            }
        }

        private void CountAnimals(List<Pawn> pawns, AutoHuntConfig config, ThingDef animal, out int currentMales, out int currentMalesYoung, out int currentFemales, out int currentFemalesYoung, out int currentTotal, out int currentPregnant)
        {
            currentMales = (currentMalesYoung = (currentFemales = (currentFemalesYoung = (currentTotal = (currentPregnant = 0)))));
            if (pawns.NullOrEmpty()) return;
            foreach (Pawn spawnedColonyAnimal in pawns)
            {
                if (spawnedColonyAnimal.def != animal)
                    continue;

                if (spawnedColonyAnimal.gender == Gender.Male)
                    if (spawnedColonyAnimal.ageTracker.CurLifeStage.reproductive)
                        currentMales++;
                    else
                        currentMalesYoung++;
                else if (spawnedColonyAnimal.gender == Gender.Female)
                {
                    if (spawnedColonyAnimal.ageTracker.CurLifeStage.reproductive)
                    {
                        Hediff firstHediffOfDef = spawnedColonyAnimal.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant);
                        if (firstHediffOfDef != null && firstHediffOfDef.Visible)
                        {
                            currentPregnant++;
                            if (!config.allowHuntPregnant)
                                continue;
                            currentFemales++;
                        }
                        else
                            currentFemales++;
                    }
                    else
                        currentFemalesYoung++;
                }

                currentTotal++;
            }
        }

        private void DoMaxColumn(WidgetRow row, ref int val, ref string buffer, int current)
        {
            int num = val;
            if (val == -1)
            {
                row.Gap(-4f);
                if (row.ButtonIconWithBG(TexButton.Infinity, 48f, "AutoSlaughterTooltipSetLimit".Translate()))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    val = current;
                }

                row.Gap(-4f);
            }
            else
            {
                row.CellGap = 0f;
                row.Gap(-5f);
                row.TextFieldNumeric<int>(ref val, ref buffer, 40f);
                val = Mathf.Max(0, val);
                if (row.ButtonIcon(TexButton.CloseXSmall, null, Color.white, null, null, doMouseoverSound: true, 16f))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    val = -1;
                    buffer = null;
                }

                row.CellGap = 4f;
                row.Gap(4f);
            }

            if (num != val)
                Manager.Notify_ConfigChanged();
        }

        private void DoAnimalHeader(Rect rect1, Rect rect2)
        {
            Widgets.BeginGroup(new Rect(rect1.x, rect1.y, rect1.width, rect1.height + rect2.height + 1f));
            int num = 0;
            foreach (Rect tmpGroupRect in tmpGroupRects)
            {
                if (num % 2 == 1)
                {
                    Widgets.DrawLightHighlight(tmpGroupRect);
                    Widgets.DrawLightHighlight(tmpGroupRect);
                }
                else
                {
                    Widgets.DrawLightHighlight(tmpGroupRect);
                }

                GUI.color = Color.gray;
                if (num > 0)
                    Widgets.DrawLineVertical(tmpGroupRect.xMin, 0f, rect1.height + rect2.height + 1f);

                if (num < tmpGroupRects.Count - 1)
                    Widgets.DrawLineVertical(tmpGroupRect.xMax, 0f, rect1.height + rect2.height + 1f);

                GUI.color = Color.white;
                num++;
            }

            foreach (Rect tmpMouseoverHighlightRect in tmpMouseoverHighlightRects)
                Widgets.DrawHighlightIfMouseover(tmpMouseoverHighlightRect);

            Widgets.EndGroup();
            tmpMouseoverHighlightRects.Clear();
            tmpGroupRects.Clear();
            Widgets.BeginGroup(rect1);
            WidgetRow row = new WidgetRow(0f, 0f);
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = (TextAnchor)4;
            row.Label(string.Empty, 24f);
            float startX = row.FinalX;
            row.Label(string.Empty, labelWidth, "AutoHuntHeaderTooltipLabel".Translate());
            Rect item = new Rect(startX, rect1.height, row.FinalX - startX, rect2.height);
            tmpMouseoverHighlightRects.Add(item);
            tmpGroupRects.Add(item);
            AddCurrentAndMaxEntries("AutoSlaugtherHeaderColTotal");
            AddCurrentAndMaxEntries("AnimalMaleAdult");
            AddCurrentAndMaxEntries("AnimalMaleYoung");
            AddCurrentAndMaxEntries("AnimalFemaleAdult");
            AddCurrentAndMaxEntries("AnimalFemaleYoung");
            AddCurrentAndMaxEntries("AnimalPregnant");
            float forceXStart = row.FinalX;
            row.Label(string.Empty, 62f, "AutoHuntHeaderTooltipForceHunt".Translate());
            Rect item2 = new Rect(forceXStart, rect1.height, row.FinalX - forceXStart, rect2.height);
            tmpMouseoverHighlightRects.Add(item2);
            tmpGroupRects.Add(item2);

            Text.Anchor = anchor;
            Widgets.EndGroup();
            Widgets.BeginGroup(rect2);
            WidgetRow widgetRow = new WidgetRow(0f, 0f);
            TextAnchor anchor2 = Text.Anchor;
            Text.Anchor = (TextAnchor)4;
            widgetRow.Label(string.Empty, 24f);
            widgetRow.Label("AutoSlaugtherHeaderColLabel".Translate(), labelWidth, "AutoHuntHeaderTooltipLabel".Translate());
            CurMaxPair("Total");
            CurMaxPair("Males");
            CurMaxPair("MalesYoung");
            CurMaxPair("Females");
            CurMaxPair("FemalesYoung");
            widgetRow.Label("AutoSlaugtherHeaderColCurrent".Translate(), 60f, "AutoHuntHeaderTooltipCurrentPregnant".Translate());
            widgetRow.Label("AllowHunt".Translate(), 56f, "AutoHuntHeaderTooltipAllowHuntPregnant".Translate());
            widgetRow.Label("ForceAutoHunt".Translate(), 62f, "AutoHuntHeaderTooltipForceHunt".Translate());
            Text.Anchor = anchor2;
            Widgets.EndGroup();
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(rect2.x, rect2.y + rect2.height + 1f, rect2.width);
            GUI.color = Color.white;

            void CurMaxPair(string str)
            {
                widgetRow.Label("AutoSlaugtherHeaderColCurrent".Translate(), 60f, $"AutoHuntHeaderTooltipCurrent{str}".Translate());
                widgetRow.Label("AutoSlaugtherHeaderColMax".Translate(), 56f, $"AutoHuntHeaderTooltipMax{str}".Translate());
            }

            void AddCurrentAndMaxEntries(string headerKey)
            {
                startX = row.FinalX;
                row.Label(string.Empty, 60f);
                tmpMouseoverHighlightRects.Add(new Rect(startX, rect1.height, row.FinalX - startX, rect2.height));
                float finalX = row.FinalX;
                row.Label(string.Empty, 56f);
                tmpMouseoverHighlightRects.Add(new Rect(finalX, rect1.height, row.FinalX - finalX, rect2.height));
                Rect rect3 = new Rect(startX, 0f, row.FinalX - startX, rect2.height);
                Widgets.Label(rect3, headerKey.Translate());
                tmpGroupRects.Add(rect3);
            }
        }

        private void DoAnimalRow(Rect rect, AutoHuntConfig config, int index)
        {
            if (index % 2 == 1)
                Widgets.DrawLightHighlight(rect);

            Color color = GUI.color;
            AnimalCountRecord animalCountRecord = animalCounts[config.animal];
            Widgets.BeginGroup(rect);
            WidgetRow row = new WidgetRow(0f, 0f);
            row.DefIcon(config.animal);
            row.Gap(4f);
            GUI.color = ((animalCountRecord.total == 0) ? Color.gray : color);
            row.Label(config.animal.LabelCap.Truncate(labelWidth - 3f), labelWidth - 3f, GetTipForAnimal());
            GUI.color = color;
            DrawCurrentCol(animalCountRecord.total, config.maxTotal);
            DoMaxColumn(row, ref config.maxTotal, ref config.uiMaxTotalBuffer, animalCountRecord.total);
            DrawCurrentCol(animalCountRecord.male, config.maxMales);
            DoMaxColumn(row, ref config.maxMales, ref config.uiMaxMalesBuffer, animalCountRecord.male);
            DrawCurrentCol(animalCountRecord.maleYoung, config.maxMalesYoung);
            DoMaxColumn(row, ref config.maxMalesYoung, ref config.uiMaxMalesYoungBuffer, animalCountRecord.maleYoung);
            DrawCurrentCol(animalCountRecord.female, config.maxFemales);
            DoMaxColumn(row, ref config.maxFemales, ref config.uiMaxFemalesBuffer, animalCountRecord.female);
            DrawCurrentCol(animalCountRecord.femaleYoung, config.maxFemalesYoung);
            DoMaxColumn(row, ref config.maxFemalesYoung, ref config.uiMaxFemalesYoungBuffer, animalCountRecord.femaleYoung);
            Text.Anchor = (TextAnchor)4;
            row.Label(animalCountRecord.pregnant.ToString(), 60f);
            Text.Anchor = (TextAnchor)0;
            bool allowHuntPregnant = config.allowHuntPregnant;
            row.Gap(19f);
            Widgets.Checkbox(row.FinalX, 0f, ref config.allowHuntPregnant, paintable: true);
            if (allowHuntPregnant != config.allowHuntPregnant)
                RecalculateAnimals();
            row.Gap(60f);
            Widgets.Checkbox(row.FinalX, 0f, ref config.forceHunt, paintable: true);
            Widgets.EndGroup();

            void DrawCurrentCol(int val, int? limit = null)
            {
                Color? color2 = null;
                if (val == 0)
                    color2 = Color.gray;
                else if (limit.HasValue && limit != -1 && val > limit)
                    color2 = ColorLibrary.RedReadable;

                Color color3 = GUI.color;
                TextAnchor anchor = Text.Anchor;
                Text.Anchor = (TextAnchor)4;
                GUI.color = color2 ?? Color.white;
                row.Label(val.ToString(), 60f);
                Text.Anchor = anchor;
                GUI.color = color3;
            }

            string GetTipForAnimal()
            {
                TaggedString labelCap = config.animal.LabelCap;
                if (Prefs.DevMode)
                    labelCap += "\n\nDEV: Animals to hunt:\n" + Manager.AnimalsToHunt.Where(x => x.def == config.animal).Select(DevTipPartForPawn).ToLineList("  - ");

                return labelCap;
            }
        }

        static string DevTipPartForPawn(Pawn pawn)
        {
            string text = pawn.LabelShortCap + " " + pawn.gender.GetLabel() + " (" + pawn.ageTracker.AgeBiologicalYears + "y)";
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant);
            if (firstHediffOfDef != null)
                text = text + ", pregnant (" + firstHediffOfDef.Severity.ToStringPercent() + ")";

            return text;
        }
    }
}
