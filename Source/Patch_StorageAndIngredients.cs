using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static class Patch_StorageAndIngredients
        {
            [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), typeof(Thing))]
            static class ThingFilter_Allows_Patch
            {
                [HarmonyPostfix]
                static void IsAllowed(ThingFilter __instance, ref bool __result, Thing t) {
                    if (!Find.World.GetComponent<MyWorldComponent>().thingFilterCustomFilters.TryGetValue(__instance, out var customFilter))
                        return;
                    if (!customFilter.IsAllowed(t))
                        __result = false;
                }
            }

            [HarmonyPatch(typeof(ThingFilterUI), nameof(ThingFilterUI.DoThingFilterConfigWindow))]
            static class ThingFilterUI_DoThingFilterConfigWindow_Patch
            {
                static List<CodeInstruction> codes, newCodes;
                static int i;

                static void InsertCode(int offset, Func<bool> when, Func<List<CodeInstruction>> what, bool bringLabels = false) {
                    CustomThingFilters.InsertCode(ref i, ref codes, ref newCodes, offset, when, what, bringLabels);
                }

                static void FilterRanges(ref float y, float width, ThingFilter filter) {
                    if (!Find.World.GetComponent<MyWorldComponent>().thingFilterCustomFilters.TryGetValue(filter, out var customFilter))
                        return;

                    // copying DrawQualityFilterConfig() re: setting font, width, etc.
                    width -= 20f;
                    customFilter.DrawMenu(new Rect(20f, y, width, 24f));
                    y += 24f;
                    foreach (var range in customFilter.ActiveFilterRanges) {
                        var height = range.Height(width);
                        var rect = new Rect(20f, y, width, height);
                        range.Draw(rect);
                        y += height;
                        y += 5f;
                        Text.Font = GameFont.Small;
                    }
                }

                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> CustomFilter(IEnumerable<CodeInstruction> instructions) {
                    codes = instructions.ToList();
                    newCodes = new List<CodeInstruction>();
                    i = 0;

                    InsertCode(
                        1,
                        () => codes[i].operand is MethodInfo method && method == AccessTools.Method(typeof(ThingFilterUI), "DrawQualityFilterConfig"),
                        () =>
                            new List<CodeInstruction> {
                                new CodeInstruction(codes[i - 4]),
                                new CodeInstruction(codes[i - 3]),
                                new CodeInstruction(codes[i - 2]),
                                new CodeInstruction(codes[i - 1]),
                                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThingFilterUI_DoThingFilterConfigWindow_Patch), nameof(FilterRanges))),
                            }, true);

                    for (; i < codes.Count; i++)
                        newCodes.Add(codes[i]);
                    return newCodes.AsEnumerable();
                }
            }

            [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.ExposeData))]
            static class StorageSettings_ExposeData_Patch
            {
                [HarmonyPostfix]
                static void ExposeCustomFilter(StorageSettings __instance) {
                    var world = Find.World.GetComponent<MyWorldComponent>();
                    world.ExposeCustomFilter(world.storageSettingsCustomFilters, __instance, __instance.filter, "COCTF_thingFilter");
                }
            }

            [HarmonyPatch(typeof(Bill), nameof(Bill.ExposeData))]
            static class Bill_ExposeData_Patch
            {
                [HarmonyPostfix]
                static void ExposeCustomFilters(Bill __instance) {
                    var world = Find.World.GetComponent<MyWorldComponent>();
                    world.ExposeCustomFilter(world.billTargetCountCustomFilters, __instance, null, "COCTF_targetCountFilter");
                    world.ExposeCustomFilter(world.billCustomFilters, __instance, __instance.ingredientFilter, "COCTF_thingFilter");
                }
            }

            [HarmonyPatch(typeof(StorageSettings), MethodType.Constructor)]
            static class StorageSettings_StorageSettings_Patch
            {
                [HarmonyPostfix]
                static void CreateCustomFilter(StorageSettings __instance) {
                    var world = Find.World.GetComponent<MyWorldComponent>();
                    world.CreateCustomFilter(world.storageSettingsCustomFilters, __instance, __instance.filter);
                }
            }

            [HarmonyPatch(typeof(Bill), MethodType.Constructor, typeof(RecipeDef))]
            static class Bill_Bill_Patch
            {
                [HarmonyPostfix]
                static void CreateCustomFilters(Bill __instance) {
                    var world = Find.World.GetComponent<MyWorldComponent>();
                    world.CreateCustomFilter(world.billTargetCountCustomFilters, __instance, null);
                    world.CreateCustomFilter(world.billCustomFilters, __instance, __instance.ingredientFilter);
                }
            }

            [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.CopyAllowancesFrom))]
            static class ThingFilter_CopyAllowancesFrom_Patch
            {
                [HarmonyPostfix]
                static void CopyCustomFilter(ThingFilter __instance, ThingFilter other) {
                    var world = Find.World.GetComponent<MyWorldComponent>();
                    if (!world.thingFilterCustomFilters.TryGetValue(__instance, out var customFilter))
                        return;
                    if (!world.thingFilterCustomFilters.TryGetValue(other, out var otherCustomFilter))
                        return;

                    customFilter.filterRanges.Clear();
                    foreach (var range in otherCustomFilter.filterRanges)
                        customFilter.filterRanges.Add((FilterRange) range.Clone());
                }
            }
        }
    }
}
