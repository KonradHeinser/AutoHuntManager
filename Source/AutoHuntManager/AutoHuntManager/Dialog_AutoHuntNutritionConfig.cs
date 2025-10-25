using System;
using UnityEngine;
using Verse;

namespace AutoHuntManager
{
    public class Dialog_AutoHuntNutritionConfig : Window
    {
        protected MainTabWindow_AutoHunt huntWindow;
        
        public override Vector2 InitialSize => new Vector2(325f, Height);

        public static float Height
        {
            get
            {
                float h = 250f;
                if (ModsConfig.AnomalyActive)
                    h += 40f;
                return h;
            }
        }
        public Dialog_AutoHuntNutritionConfig(MainTabWindow_AutoHunt mainTabWindow)
        {
            huntWindow = mainTabWindow;
            doCloseX = true;
            optionalTitle = "AutoHuntNutritionConfig".Translate();
            draggable = true;
        }
        
        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            var initialX = MathF.Min(UI.screenWidth - 10f - InitialSize.x, huntWindow.windowRect.xMax + 10f);
            windowRect = new Rect(initialX, UI.screenHeight - InitialSize.y - 300f, InitialSize.x, InitialSize.y).Rounded();
        }

        
        public override void DoWindowContents(Rect inRect)
        {
            var num = 0;
            var viewRect = inRect.ContractedBy(1f);
            Widgets.BeginGroup(viewRect);
            WidgetRow nutrition = new WidgetRow(0f, 0f);
            var nutLabel = nutrition.Label("Nutrition".Translate(), viewRect.width - 70f, "AutoHuntNutritionTooltip".Translate());
            var nutFieldRect = new Rect(nutLabel)
            {
                xMin = nutLabel.xMax,
                xMax = nutLabel.xMax + 60f
            };
            float nut = huntWindow.Manager.config.nutritionTarget;
            var buffer = nut.ToString();
            Widgets.TextFieldNumeric<float>(nutFieldRect, ref nut, ref buffer);
            huntWindow.Manager.config.nutritionTarget = nut;
            num++;
            CheckBox("AutoHuntIncludeProducts".Translate(), "AutoHuntIncludeProductsTooltip".Translate(), ref huntWindow.Manager.config.includeAnimalProducts);
            CheckBox("AutoHuntIncludeInsects".Translate(), "AutoHuntIncludeInsectsTooltip".Translate(), ref huntWindow.Manager.config.insectsAllow);
            CheckBox("AutoHuntIncludeHumanlike".Translate(), "AutoHuntIncludeHumanlikeTooltip".Translate(), ref huntWindow.Manager.config.humanlikeAllow);
            if (ModsConfig.AnomalyActive)
                CheckBox("AutoHuntIncludeTwisted".Translate(), "AutoHuntIncludeTwistedTooltip".Translate(),  ref huntWindow.Manager.config.twistedAllow);
            CheckBox("AutoHuntRememberConfigs".Translate(), "AutoHuntRememberConfigsTooltip".Translate(), ref huntWindow.Manager.save);
            Widgets.EndGroup();
            
            void CheckBox(string label, string tooltip, ref bool toggle, bool recalc = false)
            {
                WidgetRow row = new WidgetRow(0f, 35f * num);
                row.Label(label, viewRect.width - 40f, tooltip);
                bool old = toggle;
                Widgets.Checkbox(row.FinalX, row.FinalY, ref toggle, paintable: true);
                if (recalc && toggle != old)
                    huntWindow.RecalculateAnimals();
                num++;
            }
        }
    }
}