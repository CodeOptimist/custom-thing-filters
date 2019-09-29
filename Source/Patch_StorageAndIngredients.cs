using System.Collections.Generic;
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
        static class Patch_StorageAndIngredients
        {
            static void ThingFilterUI_AfterQualityRange(ref float y, float width, ThingFilter filter) {
                if (!thingFilterCustomFilters.ContainsKey(filter))
                    return;
                var customFilter = thingFilterCustomFilters[filter];

                // copying DrawQualityFilterConfig() re: setting font, etc.
                customFilter.DrawMenu(new Rect(20f, y, width - 20f, 24f));
                y += 24f;
                foreach (var range in customFilter.ActiveFilterRanges) {
                    var rect = new Rect(20f, y, width - 20f, 28f);
                    range.Draw(rect);
                    y += 28f;
                    y += 5f;
                    Text.Font = GameFont.Small;
                }
            }

            [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), typeof(Thing))]
            static class ThingFilter_Allows_Patch
            {
                [HarmonyPostfix]
                static void IsAllowed(ThingFilter __instance, ref bool __result, Thing t) {
                    if (!thingFilterCustomFilters.ContainsKey(__instance))
                        return;
                    var customFilter = thingFilterCustomFilters[__instance];
                    if (!customFilter.IsAllowed(t))
                        __result = false;
                }
            }

            [HarmonyPatch(typeof(ThingFilterUI), nameof(ThingFilterUI.DoThingFilterConfigWindow))]
            static class ThingFilterUI_DoThingFilterConfigWindow_Patch
            {
                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> AfterQualityRange(IEnumerable<CodeInstruction> instructions) {
                    var myMethod = AccessTools.Method(typeof(Patch_StorageAndIngredients), nameof(ThingFilterUI_AfterQualityRange));
                    var codes = instructions.ToList();
                    for (var i = 0; i < codes.Count; i++)
                        if (codes[i].operand is MethodInfo method && method == AccessTools.Method(typeof(ThingFilterUI), "DrawQualityFilterConfig")) {
                            codes.InsertRange(i + 2, codes.GetRange(i - 4, 4).Concat(new CodeInstruction(OpCodes.Call, myMethod)));
                            break;
                        }

                    return codes.AsEnumerable();
                }
            }

            [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.ExposeData))]
            static class StorageSettings_ExposeData_Patch
            {
                [HarmonyPostfix]
                static void ExposeCustomFilter(StorageSettings __instance) {
                    CustomThingFilters.ExposeCustomFilter(storageSettingsCustomFilters, __instance, __instance.filter, "COCTF_thingFilter");
                }
            }

            [HarmonyPatch(typeof(Bill), nameof(Bill.ExposeData))]
            static class Bill_ExposeData_Patch
            {
                [HarmonyPostfix]
                static void ExposeCustomFilters(Bill __instance) {
                    ExposeCustomFilter(billTargetCountCustomFilters, __instance, null, "COCTF_targetCountFilter");
                    ExposeCustomFilter(billCustomFilters, __instance, __instance.ingredientFilter, "COCTF_thingFilter");
                }
            }

            [HarmonyPatch(typeof(StorageSettings), MethodType.Constructor)]
            static class StorageSettings_StorageSettings_Patch
            {
                [HarmonyPostfix]
                static void CreateCustomFilter(StorageSettings __instance) {
                    CustomThingFilters.CreateCustomFilter(storageSettingsCustomFilters, __instance, __instance.filter);
                }
            }

            [HarmonyPatch(typeof(Bill), MethodType.Constructor, typeof(RecipeDef))]
            static class Bill_Bill_Patch
            {
                [HarmonyPostfix]
                static void CreateCustomFilters(Bill __instance) {
                    CreateCustomFilter(billTargetCountCustomFilters, __instance, null);
                    CreateCustomFilter(billCustomFilters, __instance, __instance.ingredientFilter);
                }
            }

            [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.CopyAllowancesFrom))]
            static class ThingFilter_CopyAllowancesFrom_Patch
            {
                [HarmonyPostfix]
                static void CopyCustomFilter(ThingFilter __instance, ThingFilter other) {
                    if (!thingFilterCustomFilters.ContainsKey(__instance))
                        return;
                    var customFilter = thingFilterCustomFilters[__instance];
                    if (!thingFilterCustomFilters.ContainsKey(other))
                        return;
                    var otherCustomFilter = thingFilterCustomFilters[other];

                    customFilter.filterRanges.Clear();
                    foreach (var range in otherCustomFilter.filterRanges)
                        customFilter.filterRanges.Add((FilterRange) range.Clone());
                }
            }
        }
    }
}