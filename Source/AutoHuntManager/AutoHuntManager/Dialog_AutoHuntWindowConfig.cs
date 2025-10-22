using System;
using UnityEngine;
using Verse;

namespace AutoHuntManager
{
    public class Dialog_AutoHuntWindowConfig : Window
    {
        protected MainTabWindow_AutoHunt huntWindow;

        public override Vector2 InitialSize => new Vector2(325f, 220f);

        public Dialog_AutoHuntWindowConfig(MainTabWindow_AutoHunt mainTabWindow)
        {
            huntWindow = mainTabWindow;
            doCloseX = true;
            optionalTitle = "AutoHuntViewConfig".Translate();
            draggable = true;
        }

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            var initialX = MathF.Min(UI.screenWidth - 10f - InitialSize.x, huntWindow.windowRect.xMax + 10f);
            windowRect = new Rect(initialX, UI.screenHeight - InitialSize.y - 60f, InitialSize.x, InitialSize.y).Rounded();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var viewRect = inRect.ContractedBy(1f);
            var num = 0;
            Widgets.BeginGroup(viewRect);
            CheckBox("AutoHuntHideAbsent".Translate(), "AutoHuntHideAbsentTooltip".Translate(), ref huntWindow.hideAbsent, true);
            CheckBox("AutoHuntHideInsect".Translate(), "AutoHuntHideInsectTooltip".Translate(), ref huntWindow.hideInsect, true);
            WidgetRow cycle = new WidgetRow(0f, 70f);
            string text;
            ConfigDisplay next;
            switch (huntWindow.curDisplay)
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
                default:
                    throw new ArgumentOutOfRangeException();
            }
            cycle.Label("AutoHuntShow".Translate(), 100f, "AutoHuntDisplayCycle".Translate());
            if (cycle.ButtonText(text, "AutoHuntDisplayCycle".Translate(), fixedWidth: viewRect.xMax - cycle.FinalX - 5f))
            {
                huntWindow.curDisplay = next;
                huntWindow.RecalculateAnimals();
            }
            
            Rect name = new Rect(viewRect)
            {
                yMin = cycle.FinalY + 40f,
                yMax = cycle.FinalY + 65f
            };
            Widgets.Label(name, "AutoHuntName".Translate());
            Rect search = new Rect(name)
            {
                xMin = viewRect.xMin + 103f,
                xMax = viewRect.xMax - 32f
            };
            var nameSearch = huntWindow.nameSearch;
            nameSearch = Widgets.TextField(search, nameSearch);
            Rect button = new Rect(search)
            {
                xMin = search.xMax + 5f,
                xMax = viewRect.xMax - 1f
            };
            if (Widgets.ButtonImage(button, TexButton.CloseXSmall, Color.white, GenUI.MouseoverColor))
                nameSearch = string.Empty;
            
            if (nameSearch != huntWindow.nameSearch)
            {
                huntWindow.nameSearch = nameSearch;
                huntWindow.RecalculateAnimals();
            }
            Widgets.EndGroup();

            void CheckBox(string label, string tooltip, ref bool toggle, bool recalc = false)
            {
                WidgetRow row = new WidgetRow(0f, 35f * num);
                row.Label(label, viewRect.width - 30f, tooltip);
                bool old = toggle;
                Widgets.Checkbox(row.FinalX, row.FinalY, ref toggle, paintable: true);
                if (recalc && toggle != old)
                    huntWindow.RecalculateAnimals();
                num++;
            }
        }
    }
}
