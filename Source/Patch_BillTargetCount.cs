using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static class Patch_BillTargetCount
        {
            [HarmonyPatch(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountValidThing))]
            static class RecipeWorkerCounter_CountValidThing_Patch
            {
                [HarmonyPostfix]
                [SuppressMessage("ReSharper", "UnusedParameter.Local")]
                static void IsAllowed(ref bool __result, Thing thing, Bill_Production bill, ThingDef def) {
                    if (__result == false) return;
                    if (!Find.World.GetComponent<World>().billTargetCountCustomFilters.TryGetValue(bill, out var customFilter))
                        return;
                    if (!customFilter.IsAllowed(thing))
                        __result = false;
                }
            }

            [HarmonyPatch(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountProducts))]
            static class RecipeWorkerCounter_CountProducts_Patch
            {
                static List<CodeInstruction> codes, newCodes;
                static int i;

                static void InsertCode(int offset, Func<bool> when, Func<List<CodeInstruction>> what, bool bringLabels = false) {
                    CustomThingFilters.InsertCode(ref i, ref codes, ref newCodes, offset, when, what, bringLabels);
                }

                static bool HasActiveNonDefaultOrReqFilters(Bill_Production bill) {
                    if (Find.World.GetComponent<World>().billTargetCountCustomFilters.TryGetValue(bill, out var customFilter))
                        return customFilter.ActiveFilterRanges.Any(x => !x.AtDefault() || x.isRequired);
                    return false;
                }

                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> SkipManualCount(IEnumerable<CodeInstruction> instructions) {
                    codes = instructions.ToList();
                    newCodes = new List<CodeInstruction>();
                    i = 0;

                    InsertCode(
                        -1,
                        () => codes[i].operand is FieldInfo field && field == AccessTools.Field(typeof(Bill_Production), nameof(Bill_Production.hpRange)),
                        () => new List<CodeInstruction> {
                            new CodeInstruction(OpCodes.Ldarg_1),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RecipeWorkerCounter_CountProducts_Patch), nameof(HasActiveNonDefaultOrReqFilters))),
                            new CodeInstruction(OpCodes.Brtrue, codes[i + 3].operand),
                        });

                    for (; i < codes.Count; i++)
                        newCodes.Add(codes[i]);
                    return newCodes.AsEnumerable();
                }
            }

            [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
            static class Dialog_BillConfig_DoWindowContents_Patch
            {
                static bool hasScrollView;
                static List<CodeInstruction> codes, newCodes;
                static int i;

                static void InsertCode(int offset, Func<bool> when, Func<List<CodeInstruction>> what, bool bringLabels = false) {
                    CustomThingFilters.InsertCode(ref i, ref codes, ref newCodes, offset, when, what, bringLabels);
                }

                static void BeginScrollView(Listing_Standard listing, Bill_Production bill) {
                    if (!Find.World.GetComponent<World>().billTargetCountCustomFilters.TryGetValue(bill, out var customFilter))
                        return;

                    hasScrollView = true;
                    var rect = listing.GetRect(0);
                    var repeatModeSubdialogHeight = (int) AccessTools.Field(typeof(Dialog_BillConfig), "RepeatModeSubdialogHeight").GetValue(null);
                    rect.height = repeatModeSubdialogHeight - rect.y;
                    var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, 100f + customFilter.billTargetCount_Height);
                    Widgets.BeginScrollView(rect, ref customFilter.billTargetCount_ScrollPosition, viewRect);
                    var beginRect = new Rect(16f, rect.y, rect.width - 32f, 9999f);
                    listing.Begin(beginRect);
                }

                static void FilterRanges(Listing_Standard listing, Bill_Production bill) {
                    if (!Find.World.GetComponent<World>().billTargetCountCustomFilters.TryGetValue(bill, out var customFilter))
                        return;

                    customFilter.DrawMenu(listing.GetRect(24f));

                    foreach (var range in customFilter.ActiveFilterRanges) {
                        var height = range.Height(listing.ColumnWidth);
                        var rect = listing.GetRect(height);
                        range.Draw(rect);
                    }

                    customFilter.billTargetCount_Height = listing.CurHeight;
                }

                static void EndScrollView(Listing_Standard listing) {
                    if (!hasScrollView) return;
                    hasScrollView = false;
                    listing.End();
                    Widgets.EndScrollView();
                }

                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> CustomFilter(IEnumerable<CodeInstruction> instructions) {
                    codes = instructions.ToList();
                    newCodes = new List<CodeInstruction>();
                    i = 0;

                    InsertCode(
                        -11,
                        () => codes[i].operand is MethodInfo hitMethod && hitMethod == AccessTools.Method(typeof(Widgets), nameof(Widgets.FloatRange))
                                                                       && codes[i - 2].operand is string name && name == "HitPoints",
                        () => new List<CodeInstruction> {
                            new CodeInstruction(codes[i - 11]),
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Dialog_BillConfig), "bill")),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Dialog_BillConfig_DoWindowContents_Patch), nameof(BeginScrollView))),
                        });

                    InsertCode(
                        1,
                        () => codes[i].operand is MethodInfo method && method == AccessTools.Method(typeof(Widgets), nameof(Widgets.QualityRange)),
                        () => new List<CodeInstruction> {
                            new CodeInstruction(codes[i - 7]),
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Dialog_BillConfig), "bill")),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Dialog_BillConfig_DoWindowContents_Patch), nameof(FilterRanges)))
                        }, true);

                    InsertCode(
                        -2,
                        () => codes[i].operand is MethodInfo endMethod && endMethod == AccessTools.Method(typeof(Listing_Standard), nameof(Listing_Standard.EndSection)),
                        () => new List<CodeInstruction> {
                            new CodeInstruction(codes[i - 1]),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Dialog_BillConfig_DoWindowContents_Patch), nameof(EndScrollView))),
                        }, true);

                    for (; i < codes.Count; i++)
                        newCodes.Add(codes[i]);
                    return newCodes.AsEnumerable();
                }
            }
        }
    }
}