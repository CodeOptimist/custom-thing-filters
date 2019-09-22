using System;
using UnityEngine;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        const float TOLERANCE = 1E-05f;
        static bool customRangeWidget;

        public abstract class FilterRange : ICloneable
        {
            protected Func<FilterRange, Thing, bool> isAllowed;
            public string saveLabel, label;

            protected FilterRange(string saveLabel, string label, Func<FilterRange, Thing, bool> isAllowed)
            {
                this.saveLabel = saveLabel;
                this.label = label;
                this.isAllowed = isAllowed;
            }

            public abstract object Clone();
            public abstract bool AtDefault();
            public abstract bool Includes(int val);
            public abstract bool Includes(float val);
            public abstract void Load();
            public abstract void Draw(Rect rect);

            public bool IsAllowed(Thing thing)
            {
                if (AtDefault())
                    return true;
                return isAllowed(this, thing);
            }
        }

        public class FilterIntRange : FilterRange
        {
            public readonly int min, max;
            public IntRange inner;

            public FilterIntRange(string saveLabel, string label, float min, float max, Func<FilterRange, Thing, bool> isAllowed) : this(saveLabel, label, (int) min, (int) max, isAllowed)
            {
            }

            public FilterIntRange(string saveLabel, string label, int min, int max, Func<FilterRange, Thing, bool> isAllowed) : base(saveLabel, label, isAllowed)
            {
                this.min = min;
                this.max = max;
                inner = new IntRange(min, max);
            }

            public override bool AtDefault()
            {
                return inner.min == min && inner.max == max;
            }

            public override bool Includes(int val)
            {
                return val >= inner.min && val <= inner.max;
            }

            public override bool Includes(float val)
            {
                return Includes((int) val);
            }

            public override void Draw(Rect rect)
            {
                customRangeWidget = true;
                Widgets.IntRange(rect, (int) rect.y, ref inner, min, max, label);
                customRangeWidget = false;
            }

            public override void Load()
            {
                var loadedNull = inner.min == -9999999 && inner.max == -9999999;
                if (loadedNull) {
                    inner.min = min;
                    inner.max = max;
                }
            }

            public override object Clone()
            {
                return new FilterIntRange(saveLabel, label, min, max, isAllowed) {inner = {min = inner.min, max = inner.max}};
            }

            public override string ToString()
            {
                return $"{min} {max} {inner}";
            }
        }

        public class FilterFloatRange : FilterRange
        {
            public readonly float min, max;
            readonly ToStringStyle toStringStyle;
            public FloatRange inner;

            public FilterFloatRange(string saveLabel, string label, ToStringStyle toStringStyle, float min, float max, Func<FilterRange, Thing, bool> isAllowed) : base(saveLabel, label, isAllowed)
            {
                this.toStringStyle = toStringStyle;
                this.min = min;
                this.max = max;
                inner = new FloatRange(min, max);
            }

            public override bool AtDefault()
            {
                return Math.Abs(inner.min - min) < TOLERANCE && Math.Abs(inner.max - max) < TOLERANCE;
            }

            public override bool Includes(int val)
            {
                return Includes((float) val);
            }

            public override bool Includes(float val)
            {
                return val >= inner.min && val <= inner.max;
            }

            public override void Draw(Rect rect)
            {
                customRangeWidget = true;
                Widgets.FloatRange(rect, (int) rect.y, ref inner, min, max, label, toStringStyle);
                customRangeWidget = false;
            }

            public override void Load()
            {
                var loadedNull = Math.Abs(inner.min - -9999999f) < TOLERANCE && Math.Abs(inner.max - -9999999f) < TOLERANCE;
                if (loadedNull) {
                    inner.min = min;
                    inner.max = max;
                }
            }

            public override object Clone()
            {
                return new FilterFloatRange(saveLabel, label, toStringStyle, min, max, isAllowed) {inner = {min = inner.min, max = inner.max}};
            }

            public override string ToString()
            {
                return $"{min} {max} {inner}";
            }
        }
    }
}