using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static bool customWidget;

        public class StatFilterRange : FilterRange
        {
            public StatDef statDef;

            public StatFilterRange(StatDef statDef, Dictionary<ThingDef, float> thingDefValues, float min, float max) : base(
                $"COCTF_allowed{statDef.defName}", statDef.label, $"<i>{statDef.category.label}</i> {statDef.label}",
                statDef.toStringStyle, min, max, (range, thing) => thingDefValues.ContainsKey(thing.def) && range.Includes(thingDefValues[thing.def]))
            {
                this.statDef = statDef;
            }
        }

        public class FilterRange : ICloneable
        {
            public readonly string label, menuLabel;
            readonly float min, max;
            readonly ToStringStyle toStringStyle;
            public FloatRange inner;
            public bool isActive;
            protected Func<FilterRange, Thing, bool> isAllowed;
            public string saveLabel;

            public FilterRange(string saveLabel, string label, string menuLabel, ToStringStyle toStringStyle, float min, float max, Func<FilterRange, Thing, bool> isAllowed)
            {
                this.saveLabel = saveLabel;
                this.label = label;
                this.menuLabel = menuLabel;
                this.toStringStyle = toStringStyle;
                this.min = min;
                this.max = max;
                this.isAllowed = isAllowed;
                inner = new FloatRange(min, max);
            }

            public object Clone()
            {
                return new FilterRange(saveLabel, label, menuLabel, toStringStyle, min, max, isAllowed) {isActive = isActive, inner = {min = inner.min, max = inner.max}};
            }

            public bool IsAllowed(Thing thing)
            {
                return isAllowed(this, thing);
            }

            public bool AtDefault()
            {
                return Mathf.Approximately(inner.min, min) && Mathf.Approximately(inner.max, max);
            }

            public bool Includes(float val)
            {
                return val >= inner.min && val <= inner.max;
            }

            public void Draw(Rect rect)
            {
                customWidget = true;
                Widgets.FloatRange(rect, (int) rect.y, ref inner, min, max, label, toStringStyle);
                customWidget = false;
            }

            public void Load()
            {
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