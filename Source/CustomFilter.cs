using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        class CustomFilter : IExposable
        {
            public readonly List<FilterRange> filterRanges = new List<FilterRange>();

            public CustomFilter()
            {
                foreach (var info in statThingInfos)
                    filterRanges.Add(new StatFilterRange(info.statDef, info.thingDefValues, info.min, info.max));
            }

            public IEnumerable<FilterRange> ActiveFilterRanges {
                get { return filterRanges.Where(x => x.isActive); }
            }

            public void ExposeData()
            {
                foreach (var range in filterRanges) {
                    if (Scribe.mode != LoadSaveMode.Saving || range.isActive || !range.AtDefault())
                        Scribe_Values.Look(ref range.isActive, $"{range.saveLabel}_isActive", forceSave: !range.AtDefault());
                    // don't save unchanged ranges, it isn't meaningful because mods can affect the min/max of thingdefs
                    if (Scribe.mode != LoadSaveMode.Saving || !range.AtDefault())
                        Scribe_Values.Look(ref range.inner, $"{range.saveLabel}_range", new FloatRange(-9999999f, -9999999f));
                }
            }

            public bool IsAllowed(Thing t)
            {
                return ActiveFilterRanges.All(range => range.IsAllowed(t));
            }

            public void DrawMenu(Rect rect)
            {
                var font = Text.Font;
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(rect, "Stat filters")) {
                    var list = filterRanges.OrderBy(x => x.menuLabel).Select(x => new FloatMenuOption((x.isActive ? "✔ " : " ") + x.menuLabel, () => { x.isActive = !x.isActive; })).ToList();
                    Find.WindowStack.Add(new FloatMenu(list));
                }

                Text.Font = font;
            }
        }
    }
}