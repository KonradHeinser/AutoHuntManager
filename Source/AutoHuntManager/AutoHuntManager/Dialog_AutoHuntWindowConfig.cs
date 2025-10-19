using RimWorld;
using UnityEngine;
using Verse;

namespace AutoHuntManager
{
    public class Dialog_AutoHuntWindowConfig : Window
    {
        protected MainTabWindow_AutoHunt huntWindow;

        public override Vector2 InitialSize => new Vector2(100f, 140f);

        public Dialog_AutoHuntWindowConfig(MainTabWindow_AutoHunt huntWindow)
        {
            this.huntWindow = huntWindow;
            doCloseX = true;
            optionalTitle = "AutoHuntViewConfig".Translate();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var viewRect = inRect.ContractedBy(5f);
            Widgets.BeginGroup(viewRect);
            CheckBox("AutoHuntHideAbsent".Translate(), "AutoHuntHideAbsentTooltip".Translate(), 0f, ref huntWindow.hideAbsent, true);
            CheckBox("AutoHuntHideInsect".Translate(), "AutoHuntHideInsectTooltip".Translate(), 35f, ref huntWindow.hideInsect, true);
            WidgetRow cycle = new WidgetRow(0f, 70f);
            string text = string.Empty;
            ConfigDisplay next = ConfigDisplay.All;
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
            }
            if (cycle.ButtonText(text, "AutoHuntDisplayCycle".Translate(), fixedWidth: viewRect.width - 10f))
            {
                huntWindow.curDisplay = next;
                huntWindow.RecalculateAnimals();
            }
            Rect search = new Rect(viewRect)
            {
                yMin = cycle.FinalY
            };
            search.xMax -= 10f;
            huntWindow.nameSearch = Widgets.TextField(search, huntWindow.nameSearch);
            Rect button = new Rect(search)
            {
                xMin = search.xMax,
                xMax = search.xMax + 20f
            };
            if (Widgets.ButtonImage(button, TexButton.CloseXSmall, Color.white, GenUI.MouseoverColor))
                huntWindow.nameSearch = string.Empty;

            void CheckBox(string label, string tooltip, float y, ref bool toggle, bool recalc = false)
            {
                WidgetRow row = new WidgetRow(0f, y);
                row.Label(label, viewRect.width - 25f, tooltip);
                bool old = toggle;
                Widgets.Checkbox(row.FinalX, row.FinalY, ref toggle, paintable: true);
                if (recalc && toggle != old)
                    huntWindow.RecalculateAnimals();
            }
        }
    }
}
