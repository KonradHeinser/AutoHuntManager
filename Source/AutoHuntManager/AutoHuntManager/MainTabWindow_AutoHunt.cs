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

        public override Vector2 InitialSize => new Vector2(1050f, 600f);

        public override void PostOpen()
        {
            base.PostOpen();
            if (Manager != null)
                RecalculateAnimals();
        }

        private void RecalculateAnimals()
        {
            animalCounts.Clear();
            foreach (AutoHuntConfig config in Manager.configs)
            {
                AnimalCountRecord value = default;
                CountAnimals(Find.CurrentMap, config, config.animal, out value.male, out value.maleYoung, out value.female, out value.femaleYoung, out value.total, out value.pregnant);
                animalCounts.Add(config.animal, value);
            }

            configsOrdered = (from c in Manager.configs
                              orderby animalCounts[c.animal].total descending, c.animal.label
                              select c).ToList();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Manager == null)
            {
                Log.Error("Attempted to open the auto hunt manager without the map component available");
                return;
            }
            Rect rect = new Rect(inRect);
            rect.yMax -= Window.CloseButSize.y;
            rect.yMin += 8f;
            Listing_Standard listing_Standard = new Listing_Standard(rect, () => scrollPos)
            {
                ColumnWidth = inRect.width - 16f - 4f
            };
            viewRect = new Rect(0f, 0f, rect.width - 16f, 30f * (float)(configsOrdered.Count + 1));
            Rect other = rect;
            other.x = scrollPos.x;
            other.y = scrollPos.y;
            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            listing_Standard.Begin(viewRect);
            DoAnimalHeader(listing_Standard.GetRect(24f), listing_Standard.GetRect(24f));
            listing_Standard.Gap(6f);
            int num = 0;
            foreach (AutoHuntConfig item in configsOrdered)
            {
                Rect rect2 = listing_Standard.GetRect(24f);
                if (rect2.Overlaps(other))
                    DoAnimalRow(rect2, item, num);

                listing_Standard.Gap(6f);
                num++;
            }

            listing_Standard.End();
            Widgets.EndScrollView();
            Rect rect3 = new Rect(inRect.x + inRect.width / 2f + Window.CloseButSize.x / 2f + 10f, inRect.y + inRect.height - 35f, 395f, 50f)
            {
                yMax = inRect.yMax,
                xMax = inRect.xMax
            };
            Color color = GUI.color;
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = (TextAnchor)3;
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            if (Text.TinyFontSupported)
                Widgets.Label(rect3, "AutoSlaugtherTip".Translate());
            else
            {
                Widgets.Label(rect3, "AutoSlaugtherTip".Translate().Truncate(rect3.width));
                TooltipHandler.TipRegion(rect3, "AutoSlaugtherTip".Translate());
            }

            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = color;
        }

        private void CountAnimals(Map map, AutoHuntConfig config, ThingDef animal, out int currentMales, out int currentMalesYoung, out int currentFemales, out int currentFemalesYoung, out int currentTotal, out int currentPregnant)
        {
            currentMales = (currentMalesYoung = (currentFemales = (currentFemalesYoung = (currentTotal = (currentPregnant = 0)))));
            foreach (Pawn spawnedColonyAnimal in map.mapPawns.SpawnedColonyAnimals)
            {
                if (spawnedColonyAnimal.def != animal || !Manager.CanEverAutoHunt(spawnedColonyAnimal))
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

        private float CalculateLabelWidth(Rect rect)
        {
            float num = 64f;
            return rect.width - 24f - 4f - 4f - num * 7f - 420f - 32f;
        }

        private void DoMaxColumn(WidgetRow row, ref int val, ref string buffer, int current)
        {
            int num = val;
            if (val == -1)
            {
                float num2 = 68f;
                float width = (60f - num2) / 2f;
                row.Gap(width);
                if (row.ButtonIconWithBG(TexButton.Infinity, 48f, "AutoSlaughterTooltipSetLimit".Translate()))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    val = current;
                }

                row.Gap(width);
            }
            else
            {
                row.CellGap = 0f;
                row.Gap(-4f);
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
            float width = CalculateLabelWidth(rect1);
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
            row.Label(string.Empty, width, "AutoHuntHeaderTooltipLabel".Translate());
            Rect item = new Rect(startX, rect1.height, row.FinalX - startX, rect2.height);
            tmpMouseoverHighlightRects.Add(item);
            tmpGroupRects.Add(item);
            AddCurrentAndMaxEntries("AutoHuntHeaderColTotal");
            AddCurrentAndMaxEntries("AnimalMaleAdult");
            AddCurrentAndMaxEntries("AnimalMaleYoung");
            AddCurrentAndMaxEntries("AnimalFemaleAdult");
            AddCurrentAndMaxEntries("AnimalFemaleYoung");
            AddCurrentAndMaxEntries("AnimalPregnant", 0f, 16f);
            Text.Anchor = anchor;
            Widgets.EndGroup();
            Widgets.BeginGroup(rect2);
            WidgetRow widgetRow = new WidgetRow(0f, 0f);
            TextAnchor anchor2 = Text.Anchor;
            Text.Anchor = (TextAnchor)4;
            widgetRow.Label(string.Empty, 24f);
            widgetRow.Label("AutoHuntHeaderColLabel".Translate(), width, "AutoHuntHeaderTooltipLabel".Translate());
            widgetRow.Label("AutoHuntHeaderColCurrent".Translate(), 60f, "AutoHuntHeaderTooltipCurrentTotal".Translate());
            widgetRow.Label("AutoHuntHeaderColMax".Translate(), 56f, "AutoHuntHeaderTooltipMaxTotal".Translate());
            widgetRow.Label("AutoHuntHeaderColCurrent".Translate(), 60f, "AutoHuntHeaderTooltipCurrentMales".Translate());
            widgetRow.Label("AutoHuntHeaderColMax".Translate(), 56f, "AutoHuntHeaderTooltipMaxMales".Translate());
            widgetRow.Label("AutoHuntHeaderColCurrent".Translate(), 60f, "AutoHuntHeaderTooltipCurrentMalesYoung".Translate());
            widgetRow.Label("AutoHuntHeaderColMax".Translate(), 56f, "AutoHuntHeaderTooltipMaxMalesYoung".Translate());
            widgetRow.Label("AutoHuntHeaderColCurrent".Translate(), 60f, "AutoHuntHeaderTooltipCurrentFemales".Translate());
            widgetRow.Label("AutoHuntHeaderColMax".Translate(), 56f, "AutoHuntHeaderTooltipMaxFemales".Translate());
            widgetRow.Label("AutoHuntHeaderColCurrent".Translate(), 60f, "AutoHuntHeaderTooltipCurrentFemalesYoung".Translate());
            widgetRow.Label("AutoHuntHeaderColMax".Translate(), 56f, "AutoHuntHeaderTooltipMaxFemalesYoung".Translate());
            widgetRow.Label("AutoHuntHeaderColCurrent".Translate(), 60f, "AutoHuntHeaderTooltipCurrentPregnant".Translate());
            widgetRow.Label("AllowHunt".Translate(), 72f, "AutoHuntHeaderTooltipAllowHuntPregnant".Translate());
            widgetRow.Label("ForceAutoHunt".Translate(), 72f, "AutoHuntHeaderTooltipForceHunt".Translate());
            Text.Anchor = anchor2;
            Widgets.EndGroup();
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(rect2.x, rect2.y + rect2.height + 1f, rect2.width);
            GUI.color = Color.white;
            void AddCurrentAndMaxEntries(string headerKey, float extraWidthFirst = 0f, float extraWidthSecond = 0f)
            {
                startX = row.FinalX;
                row.Label(string.Empty, 60f + extraWidthFirst);
                tmpMouseoverHighlightRects.Add(new Rect(startX, rect1.height, row.FinalX - startX, rect2.height));
                float finalX = row.FinalX;
                row.Label(string.Empty, 56f + extraWidthSecond);
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
            float width = CalculateLabelWidth(rect);
            Widgets.BeginGroup(rect);
            WidgetRow row = new WidgetRow(0f, 0f);
            row.DefIcon(config.animal);
            row.Gap(4f);
            GUI.color = ((animalCountRecord.total == 0) ? Color.gray : color);
            row.Label(config.animal.LabelCap.Truncate(width), width, GetTipForAnimal());
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
            row.Gap(26f);
            Widgets.Checkbox(row.FinalX, 0f, ref config.allowHuntPregnant, 24f, disabled: false, paintable: true);
            if (allowHuntPregnant != config.allowHuntPregnant)
                RecalculateAnimals();
            row.Gap(26f);
            Widgets.Checkbox(row.FinalX, 0f, ref config.forceHunt, 24f, disabled: false, paintable: true);

            row.Gap(52f);
            Text.Anchor = (TextAnchor)0;
            row.Gap(24f);

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
                    labelCap += "\n\nDEV: Animals to slaughter:\n" + Manager.AnimalsToHunt.Where(x => x.def == config.animal).Select(DevTipPartForPawn).ToLineList("  - ");

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
