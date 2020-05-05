using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;
// ReSharper disable once RedundantUsingDirective
using Debug = System.Diagnostics.Debug;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        class CustomFilter : IExposable
        {
            public readonly List<FilterRange> filterRanges = new List<FilterRange>();
            public float billTargetCount_Height;
            public Vector2 billTargetCount_ScrollPosition;

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
                if (Scribe.mode != LoadSaveMode.LoadingVars && Scribe.mode != LoadSaveMode.Saving)
                    return;

                var world = Find.World.GetComponent<MyWorldComponent>();
                foreach (var range in filterRanges) {
                    var label = "";

                    if (Scribe.mode == LoadSaveMode.LoadingVars) {
                        var verPrefix = world.dataVersion.EndsWith("_") ? $"COCTF_{world.dataVersion}" : world.dataVersion == "" ? "COCTF_" : "";
                        label = range.saveLabel;
                        // substitute in old data names for compatibility
                        if (MyWorldComponent.newToOldLabels.TryGetValue(world.dataVersion.TrimEnd('_'), out var subs)) {
                            foreach (var sub in subs)
                                label = Regex.Replace(label, sub.Key, sub.Value);
                        }

                        label = verPrefix + label;
                    } else if (Scribe.mode == LoadSaveMode.Saving) {
                        // I decided having the data version elsewhere in the save is enough; readability counts
                        label = range.saveLabel;
                        var hasDefaults = range.AtDefault() && !range.isActive && !range.isRequired;
                        if (hasDefaults) continue;
                    }

                    // don't save unchanged ranges, it isn't meaningful because mods can affect the min/max of thingdefs
                    if (Scribe.mode != LoadSaveMode.Saving || !range.AtDefault())
                        Scribe_Values.Look(ref range.inner, $"{label}_range", new FloatRange(-9999999f, -9999999f));

                    Scribe_Values.Look(ref range.isActive, $"{label}_isActive", forceSave: true);
                    Scribe_Values.Look(ref range.isRequired, $"{label}_isRequired", forceSave: true);
                    if (Scribe.mode == LoadSaveMode.LoadingVars && !range.AtDefault() && world.dataVersion == "")
                        range.isRequired = true; // preserve behavior of first mod version
                }
            }

            public bool IsAllowed(Thing t) {
                return ActiveFilterRanges.All(range => range.IsAllowed(t));
            }

            public void DrawMenu(Rect rect) {
                var font = Text.Font;
                Text.Font = GameFont.Small;

                void MenuFromRanges(IEnumerable<FilterRange> ranges, string title, Func<FilterRange, bool, bool> checkFunc, Func<FilterRange, string> labelFunc) {
                    var menuOptions = ranges.OrderBy(x => x.menuLabel(x))
                        .Select(
                            x => new FloatMenuOption(
                                $"{(checkFunc(x, false) ? "✔ " : "")}{labelFunc(x)}",
                                () => { checkFunc(x, true); }))
                        .ToList();
                    if (menuOptions.Any()) Find.WindowStack.Add(new FloatMenu(menuOptions, title));
                }

                bool Active(FilterRange range, bool isFlip) {
                    if (isFlip) range.isActive = !range.isActive;
                    return range.isActive;
                }

                bool Required(FilterRange range, bool isFlip) {
                    if (isFlip) range.isRequired = !range.isRequired;
                    return range.isRequired;
                }

                if (Widgets.ButtonText(new Rect(rect.x + rect.width * 0 / 8, rect.y, rect.width * 3 / 8, rect.height), "Base stat"))
                    MenuFromRanges(filterRanges.OfType<BaseStatFilterRange>(), "Base stat filters", Active, x => x.menuLabel(x));
                if (Widgets.ButtonText(new Rect(rect.x + rect.width * 3 / 8, rect.y, rect.width * 1 / 8, rect.height), "A"))
                    MenuFromRanges(filterRanges.OfType<StatFilterRange>().Where(x => x.isActive), "Active stat filters", Active, x => x.widgetLabel(x));
                if (Widgets.ButtonText(new Rect(rect.x + rect.width * 4 / 8, rect.y, rect.width * 1 / 8, rect.height), "R"))
                    MenuFromRanges(filterRanges.OfType<StatFilterRange>().Where(x => x.isActive), "Required stats", Required, x => x.widgetLabel(x));
                if (Widgets.ButtonText(new Rect(rect.x + rect.width * 5 / 8, rect.y, rect.width * 3 / 8, rect.height), "Cur. stat"))
                    MenuFromRanges(filterRanges.OfType<CurStatFilterRange>(), "Current stat filters", Active, x => x.menuLabel(x));

                Text.Font = font;
            }
        }
    }
}
