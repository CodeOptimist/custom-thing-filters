using System;
using System.Globalization;
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static class Patch_RangeWidget
        {
            static byte _curDragEnd;

            static readonly Action CheckPlayDragSliderSound = () => { AccessTools.Method(typeof(Widgets), "CheckPlayDragSliderSound")?.Invoke(null, new object[] { }); };

            static RangeEnd curDragEnd {
                get => (RangeEnd) _curDragEnd;
                set => _curDragEnd = (byte) value;
            }

            static bool TryToFloatByStyle(string s, out float result, ToStringStyle style, ToStringNumberSense numberSense = ToStringNumberSense.Absolute) {
                if (style == ToStringStyle.Temperature && numberSense == ToStringNumberSense.Offset) style = ToStringStyle.TemperatureOffset;

                switch (numberSense) {
                    case ToStringNumberSense.Offset:
                        s = s.TrimStart('+');
                        break;
                    case ToStringNumberSense.Factor:
                        s = s.TrimStart('x');
                        break;
                }

                switch (style) {
                    case ToStringStyle.Integer:
                        return float.TryParse(s, NumberStyles.Integer, CultureInfo.CurrentCulture, out result);
                    case ToStringStyle.FloatOne:
                    case ToStringStyle.FloatTwo:
                    case ToStringStyle.FloatThree:
                    case ToStringStyle.FloatMaxOne:
                    case ToStringStyle.FloatMaxTwo:
                    case ToStringStyle.FloatMaxThree:
                    case ToStringStyle.FloatTwoOrThree:
                        return float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
                    case ToStringStyle.PercentZero:
                    case ToStringStyle.PercentOne:
                    case ToStringStyle.PercentTwo:
                        s = s.TrimEnd(CultureInfo.CurrentCulture.NumberFormat.PercentSymbol.ToCharArray());
                        if (!float.TryParse(s, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out result))
                            return false;
                        result /= 100;
                        return true;
                    case ToStringStyle.Temperature:
                        switch (Prefs.TemperatureMode) {
                            case TemperatureDisplayMode.Celsius:
                                s = s.TrimEnd('C');
                                return float.TryParse(s, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out result);
                            case TemperatureDisplayMode.Fahrenheit:
                                s = s.TrimEnd('F');
                                if (!float.TryParse(s, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out result))
                                    return false;
                                result = (result - 32f) * 5 / 9f;
                                return true;
                            case TemperatureDisplayMode.Kelvin:
                                s = s.TrimEnd('K');
                                if (!float.TryParse(s, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out result))
                                    return false;
                                result -= 273.15f;
                                return true;
                        }

                        break;
                    case ToStringStyle.TemperatureOffset:
                        switch (Prefs.TemperatureMode) {
                            case TemperatureDisplayMode.Celsius:
                                s = s.TrimEnd('C');
                                return float.TryParse(s, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out result);
                            case TemperatureDisplayMode.Fahrenheit:
                                s = s.TrimEnd('F');
                                if (!float.TryParse(s, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out result))
                                    return false;
                                result = result * 5 / 9f;
                                return true;
                            case TemperatureDisplayMode.Kelvin:
                                s = s.TrimEnd('K');
                                return float.TryParse(s, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out result);
                        }

                        break;
                    case ToStringStyle.WorkAmount:
                        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
                            return false;
                        result *= 60f;
                        break;
                }

                result = 0f;
                return false;
            }

            enum RangeEnd : byte { None, Min, Max }

            [HarmonyPatch(typeof(Widgets), nameof(Widgets.FloatRange))]
            static class Widgets_FloatRange_Patch
            {
                // custom widget to replace Widgets.IntRange and Widgets.FloatRange, which are almost identical to start with

                [HarmonyPrefix]
                static bool CustomRangeWidget(ref Color ___RangeControlTextColor, ref Texture2D ___FloatRangeSliderTex, ref byte ___curDragEnd, ref int ___draggingId,
                    Rect rect, int id, ref FloatRange range, float min, float max, string labelKey, ToStringStyle valueStyle) {
                    if (filterRangeTypeToDraw == null) return true;
                    ___curDragEnd = ref _curDragEnd;

                    var fieldRect = new Rect(rect) {height = 25f, width = 65f};
                    fieldRect.yMin += 4f;
                    if (TryToFloatByStyle(Widgets.TextField(fieldRect, range.min.ToStringByStyle(valueStyle)), out var inputMin, valueStyle)) {
                        range.min = inputMin;
                        if (filterRangeTypeToDraw != typeof(CurStatFilterRange) && range.min < min) range.min = min;
                        if (range.max < range.min) range.max = range.min;
                    }

                    var maxFieldRect = new Rect(fieldRect) {x = fieldRect.x + rect.width - fieldRect.width};
                    if (TryToFloatByStyle(Widgets.TextField(maxFieldRect, range.max.ToStringByStyle(valueStyle)), out var inputMax, valueStyle)) {
                        range.max = inputMax;
                        if (filterRangeTypeToDraw != typeof(CurStatFilterRange) && range.max > max) range.max = max;
                        if (range.min > range.max) range.min = range.max;
                    }

                    var hoverRect = rect;
                    // line up ends
                    rect.xMin += 8f;
                    rect.xMax -= 8f;

                    Rect leftSliderRect, rightSliderRect, lineRect;
                    using (var labelContext = new DrawContext {GuiColor = ___RangeControlTextColor, TextFont = GameFont.Tiny, TextAnchor = TextAnchor.UpperCenter}) {
                        var labelRect = new Rect(rect);
                        labelRect.xMin += fieldRect.width;
                        labelRect.xMax -= fieldRect.width;
                        labelRect.yMin -= 2f;
                        Widgets.Label(labelRect, labelKey);

                        lineRect = new Rect(rect.x, rect.yMax - 8f - 1f, rect.width, 2f);
                        GUI.DrawTexture(lineRect, BaseContent.WhiteTex);

                        using (var sliderContext = new DrawContext {GuiColor = Color.white}) {
                            var leftSliderX = rect.x + rect.width * (range.min - min) / (max - min);
                            leftSliderRect = new Rect(leftSliderX - 16f, lineRect.center.y - 8f, 16f, 16f);
                            GUI.DrawTexture(leftSliderRect, ___FloatRangeSliderTex);

                            var rightSliderX = rect.x + rect.width * (range.max - min) / (max - min);
                            rightSliderRect = new Rect(rightSliderX + 16f, lineRect.center.y - 8f, -16f, 16f);
                            GUI.DrawTexture(rightSliderRect, ___FloatRangeSliderTex);
                        }
                    }

                    // release an end
                    if (curDragEnd != 0 && (Event.current.type == EventType.MouseUp || Event.current.rawType == EventType.MouseDown)) {
                        ___draggingId = 0;
                        curDragEnd = RangeEnd.None;
                        SoundDefOf.DragSlider.PlayOneShotOnCamera();
                    }

                    var amDragging = ___draggingId == id;
                    if (Mouse.IsOver(hoverRect) || amDragging) {
                        var x = Event.current.mousePosition.x;

                        var isStartDrag = !amDragging && Event.current.type == EventType.MouseDown && Event.current.button == 0;
                        if (isStartDrag) {
                            ___draggingId = id;

                            // get which end
                            if (x < leftSliderRect.xMax)
                                curDragEnd = RangeEnd.Min;
                            else if (x > rightSliderRect.xMin)
                                curDragEnd = RangeEnd.Max;
                            else {
                                var closerToLeftEnd = Mathf.Abs(x - leftSliderRect.xMax) < Mathf.Abs(x - (rightSliderRect.x - 16f));
                                curDragEnd = closerToLeftEnd ? RangeEnd.Min : RangeEnd.Max;
                            }

                            Event.current.Use();
                            SoundDefOf.DragSlider.PlayOneShotOnCamera();
                        }

                        var isDrag = curDragEnd != 0 && Event.current.type == EventType.MouseDrag;
                        if (isStartDrag || isDrag) {
                            var value = (x - lineRect.x) / lineRect.width * (max - min) + min;
                            value = Mathf.Clamp(value, min, max);

                            // ReSharper disable CompareOfFloatsByEqualityOperator (copied vanilla so... seems fine?)
                            if (curDragEnd == RangeEnd.Min && value != range.min) {
                                range.min = value;
                                if (range.max < range.min) range.max = range.min;
                                CheckPlayDragSliderSound();
                            } else if (curDragEnd == RangeEnd.Max && value != range.max) {
                                range.max = value;
                                if (range.min > range.max) range.min = range.max;
                                CheckPlayDragSliderSound();
                            }
                            // ReSharper restore CompareOfFloatsByEqualityOperator

                            Event.current.Use();
                        }
                    }

                    return false;
                }
            }
        }
    }
}
