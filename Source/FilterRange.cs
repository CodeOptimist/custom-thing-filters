using System;
using UnityEngine;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        const float TOLERANCE = 1E-05f;
        static bool customRangeWidget;

        public class FilterRange : ICloneable
        {
            readonly Func<FilterRange, Thing, bool> isAllowed;
            readonly string label;
            readonly float min, max;
            readonly ToStringStyle toStringStyle;
            public FloatRange inner;
            public string saveLabel;

            public FilterRange(string saveLabel, string label, ToStringStyle toStringStyle, float min, float max, Func<FilterRange, Thing, bool> isAllowed)
            {
                this.saveLabel = saveLabel;
                this.label = label;
                this.toStringStyle = toStringStyle;
                this.min = min;
                this.max = max;
                this.isAllowed = isAllowed;
                inner = new FloatRange(min, max);
            }

            public object Clone()
            {
                return new FilterRange(saveLabel, label, toStringStyle, min, max, isAllowed) {inner = {min = inner.min, max = inner.max}};
            }

            public bool IsAllowed(Thing thing)
            {
                if (AtDefault())
                    return true;
                return isAllowed(this, thing);
            }

            public bool AtDefault()
            {
                return Math.Abs(inner.min - min) < TOLERANCE && Math.Abs(inner.max - max) < TOLERANCE;
            }

            public bool Includes(float val)
            {
                return val >= inner.min && val <= inner.max;
            }

            public void Draw(Rect rect)
            {
                customRangeWidget = true;
                Widgets.FloatRange(rect, (int) rect.y, ref inner, min, max, label, toStringStyle);
                customRangeWidget = false;
            }

            public void Load()
            {
                var loadedNull = Math.Abs(inner.min - -9999999f) < TOLERANCE && Math.Abs(inner.max - -9999999f) < TOLERANCE;
                if (loadedNull) {
                    inner.min = min;
                    inner.max = max;
                }
            }

            public override string ToString()
            {
                return $"{min} {max} {inner}";
            }
        }
    }
}