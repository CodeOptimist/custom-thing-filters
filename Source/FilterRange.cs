using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static Type filterRangeTypeToDraw;

        public abstract class StatFilterRange : FilterRange
        {
            protected StatFilterRange(StatThingInfo info, string saveLabel, string widgetLabel, string menuLabel,
                Func<FilterRange, Thing, bool> isAllowed) : base(saveLabel, widgetLabel, menuLabel, info.statDef.toStringStyle, info.min, info.max, isAllowed) {
            }
        }

        public class BaseStatFilterRange : StatFilterRange
        {
            public BaseStatFilterRange(StatThingInfo info) : base(
                info,
                $"COCTF_allowedBase{info.statDef.defName}", info.statDef.label, $"<i>{info.statDef.category.label}:</i> {info.statDef.label}",
                (range, thing) => info.thingDefValues.TryGetValue(thing.def, out var value) && range.Includes(value)) {
            }
        }

        public class CurStatFilterRange : StatFilterRange
        {
            public CurStatFilterRange(StatThingInfo info) : base(
                info,
                $"COCTF_allowedCur{info.statDef.defName}", info.statDef.label, $"<i>{info.statDef.category.label}:</i> {info.statDef.label}",
                (range, thing) => info.thingDefValues.ContainsKey(thing.def) && range.Includes(thing.GetStatValue(info.statDef))) {
            }
        }

        public abstract class FilterRange : ICloneable
        {
            protected readonly float min, max; // the extreme edges
            public readonly string saveLabel, widgetLabel, menuLabel;
            protected readonly ToStringStyle toStringStyle;

            public FloatRange inner; // the user-set range within edges
            public bool isActive;
            protected Func<FilterRange, Thing, bool> isAllowed;

            protected FilterRange(string saveLabel, string widgetLabel, string menuLabel, ToStringStyle toStringStyle, float min, float max, Func<FilterRange, Thing, bool> isAllowed) {
                this.saveLabel = saveLabel;
                this.widgetLabel = widgetLabel;
                this.menuLabel = menuLabel;
                this.toStringStyle = toStringStyle;
                this.min = min;
                this.max = max;
                this.isAllowed = isAllowed;
                inner = new FloatRange(min, max);
            }

            public object Clone() {
                return (FilterRange) MemberwiseClone();
            }

            public bool IsAllowed(Thing thing) {
                return isAllowed(this, thing);
            }

            public bool AtDefault() {
                return Mathf.Approximately(inner.min, min) && Mathf.Approximately(inner.max, max);
            }

            public bool Includes(float val) {
                return val >= inner.min && val <= inner.max;
            }

            public void Draw(Rect rect) {
                filterRangeTypeToDraw = GetType();
                Widgets.FloatRange(rect, (int) rect.y, ref inner, min, max, widgetLabel, toStringStyle);
                filterRangeTypeToDraw = null;
            }

            public void Load() {
                var loadedNull = Mathf.Approximately(inner.min, -9999999f) && Mathf.Approximately(inner.max, -9999999f);
                if (loadedNull) {
                    // adapt to changing thingDef ranges due to mods
                    inner.min = min;
                    inner.max = max;
                }
            }
        }
    }
}
