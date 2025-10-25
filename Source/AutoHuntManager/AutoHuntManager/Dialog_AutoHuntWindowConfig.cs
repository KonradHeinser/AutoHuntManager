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

        public override void PreClose()
        {
            base.PreClose();
            huntWindow.nameSearch = string.Empty;
            huntWindow.RecalculateAnimals();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var viewRect = inRect.ContractedBy(1f);
            var num = 0;
            Widgets.BeginGroup(viewRect);
            CheckBox("AutoHuntHideAbsent".Translate(), "AutoHuntHideAbsentTooltip".Translate(), ref huntWindow.hideAbsent, true);
            CheckBox("AutoHuntHideInsect".Translate(), "AutoHuntHideInsectTooltip".Translate(), ref huntWindow.hideInsect, true);
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
            Button("AutoHuntShow".Translate(), "AutoHuntDisplayCycle".Translate(), text, delegate
            {
                huntWindow.curDisplay = next;
                huntWindow.RecalculateAnimals();
            });

            Rect name = new Rect(viewRect);
            name.yMin += num * 35f + 3f;
            name.yMax = name.yMin + 25f;
            Widgets.Label(name, "AutoHuntName".Translate());
            TooltipHandler.TipRegion(name, "AutoHuntNameTooltip".Translate());
            Rect search = new Rect(name)
            {
                xMin = viewRect.xMin + 113f,
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
                row.Label(label, viewRect.width - 40f, tooltip);
                bool old = toggle;
                Widgets.Checkbox(row.FinalX, row.FinalY, ref toggle, paintable: true);
                if (recalc && toggle != old)
                    huntWindow.RecalculateAnimals();
                num++;
            }

            void Button(string label, string tooltip, string buttonText, Action action)
            {
                WidgetRow row = new WidgetRow(0f, 35f * num);
                var labelWidth = Mathf.Min(viewRect.width - 50f, 110f);
                row.Label(label, labelWidth, tooltip);
                if (row.ButtonText(buttonText, tooltip, fixedWidth: viewRect.width - labelWidth - 20f))
                    action();
                num++;
            }
        }
    }
}
