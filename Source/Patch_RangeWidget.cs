using System;
using System.Reflection;
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

            static readonly Action CheckPlayDragSliderSound = () => {
                typeof(Widgets).GetMethod("CheckPlayDragSliderSound", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, new object[] { });
            };

            static RangeEnd curDragEnd {
                get => (RangeEnd) _curDragEnd;
                set => _curDragEnd = (byte) value;
            }

            enum RangeEnd : byte { None, Min, Max }

            [HarmonyPatch(typeof(Widgets), nameof(Widgets.FloatRange))]
            static class Widgets_FloatRange_Patch
            {
                // custom widget to replace Widgets.IntRange and Widgets.FloatRange, which are almost identical to start with

                [HarmonyPrefix]
                static bool CustomRangeWidget(ref Color ___RangeControlTextColor, ref Texture2D ___FloatRangeSliderTex, ref byte ___curDragEnd, ref int ___draggingId,
                    Rect rect, int id, ref FloatRange range, float min, float max, string labelKey, ToStringStyle valueStyle)
                {
                    if (filterRangeTypeToDraw == null) return true;
                    var label = labelKey;
                    ___curDragEnd = ref _curDragEnd;

                    var rect2 = rect;
                    rect2.xMin += 8f;
                    rect2.xMax -= 8f;
                    GUI.color = ___RangeControlTextColor;
                    var text = range.min.ToStringByStyle(valueStyle) + " - " + range.max.ToStringByStyle(valueStyle);
                    if (label != null) text = $"{text} {label}";
                    var font = Text.Font;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperCenter;
                    var rect3 = rect2;
                    rect3.yMin -= 2f;
                    Widgets.Label(rect3, text);
                    Text.Anchor = TextAnchor.UpperLeft;
                    var position = new Rect(rect2.x, rect2.yMax - 8f - 1f, rect2.width, 2f);
                    GUI.DrawTexture(position, BaseContent.WhiteTex);
                    GUI.color = Color.white;
                    var num = rect2.x + rect2.width * (range.min - min) / (max - min);
                    var num2 = rect2.x + rect2.width * (range.max - min) / (max - min);
                    var x = num - 16f;
                    var center = position.center;
                    var position2 = new Rect(x, center.y - 8f, 16f, 16f);
                    GUI.DrawTexture(position2, ___FloatRangeSliderTex);
                    var x2 = num2 + 16f;
                    var center2 = position.center;
                    var position3 = new Rect(x2, center2.y - 8f, -16f, 16f);
                    GUI.DrawTexture(position3, ___FloatRangeSliderTex);
                    if (curDragEnd != 0 && (Event.current.type == EventType.MouseUp || Event.current.rawType == EventType.MouseDown)) {
                        ___draggingId = 0;
                        curDragEnd = RangeEnd.None;
                        SoundDefOf.DragSlider.PlayOneShotOnCamera();
                    }

                    var flag = false;
                    if (Mouse.IsOver(rect) || ___draggingId == id) {
                        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && id != ___draggingId) {
                            ___draggingId = id;
                            var mousePosition = Event.current.mousePosition;
                            var x3 = mousePosition.x;
                            if (x3 < position2.xMax) {
                                curDragEnd = RangeEnd.Min;
                            } else if (x3 > position3.xMin) {
                                curDragEnd = RangeEnd.Max;
                            } else {
                                var num3 = Mathf.Abs(x3 - position2.xMax);
                                var num4 = Mathf.Abs(x3 - (position3.x - 16f));
                                curDragEnd = num3 < num4 ? RangeEnd.Min : RangeEnd.Max;
                            }

                            flag = true;
                            Event.current.Use();
                            SoundDefOf.DragSlider.PlayOneShotOnCamera();
                        }

                        if (flag || curDragEnd != 0 && Event.current.type == EventType.MouseDrag) {
                            var mousePosition2 = Event.current.mousePosition;
                            var value = (mousePosition2.x - rect2.x) / rect2.width * (max - min) + min;
                            value = Mathf.Clamp(value, min, max);
                            if (valueStyle == ToStringStyle.Integer)
                                value = Mathf.RoundToInt(value);
                            // ReSharper disable CompareOfFloatsByEqualityOperator
                            if (curDragEnd == RangeEnd.Min) {
                                if (value != range.min) {
                                    range.min = value;
                                    if (range.max < range.min) range.max = range.min;
                                    CheckPlayDragSliderSound();
                                }
                            } else if (curDragEnd == RangeEnd.Max && value != range.max) {
                                range.max = value;
                                if (range.min > range.max) range.min = range.max;
                                CheckPlayDragSliderSound();
                            }
                            // ReSharper restore CompareOfFloatsByEqualityOperator

                            Event.current.Use();
                        }
                    }

                    Text.Font = font;
                    return false;
                }
            }
        }
    }
}