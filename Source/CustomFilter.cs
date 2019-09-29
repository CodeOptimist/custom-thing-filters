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

            public CustomFilter() {
                foreach (var info in statThingInfos) {
                    filterRanges.Add(new BaseStatFilterRange(info));
                    filterRanges.Add(new CurStatFilterRange(info));
                }
            }

            public IEnumerable<FilterRange> ActiveFilterRanges {
                get { return filterRanges.Where(x => x.isActive); }
            }

            public void ExposeData() {
                foreach (var range in filterRanges) {
                    if (Scribe.mode != LoadSaveMode.Saving || range.isActive || !range.AtDefault())
                        Scribe_Values.Look(ref range.isActive, $"{range.saveLabel}_isActive", forceSave: !range.AtDefault());
                    // don't save unchanged ranges, it isn't meaningful because mods can affect the min/max of thingdefs
                    if (Scribe.mode != LoadSaveMode.Saving || !range.AtDefault())
                        Scribe_Values.Look(ref range.inner, $"{range.saveLabel}_range", new FloatRange(-9999999f, -9999999f));
                }
            }

            public bool IsAllowed(Thing t) {
                return ActiveFilterRanges.All(range => range.IsAllowed(t));
            }

            public void DrawMenu(Rect rect) {
                var font = Text.Font;
                Text.Font = GameFont.Small;

                FloatMenu NewMenuFromRanges(IEnumerable<FilterRange> ranges) {
                    var floatMenuOptions = ranges.OrderBy(x => x.menuLabel).Select(x => new FloatMenuOption((x.isActive ? "✔ " : " ") + x.menuLabel, () => { x.isActive = !x.isActive; }));
                    return new FloatMenu(floatMenuOptions.ToList());
                }

                if (Widgets.ButtonText(new Rect(rect.x, rect.y, rect.width / 2, rect.height), "Base stat"))
                    Find.WindowStack.Add(NewMenuFromRanges(filterRanges.OfType<BaseStatFilterRange>().Cast<FilterRange>()));
                if (Widgets.ButtonText(new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height), "Current stat"))
                    Find.WindowStack.Add(NewMenuFromRanges(filterRanges.OfType<CurStatFilterRange>().Cast<FilterRange>()));

                Text.Font = font;
            }
        }
    }
}