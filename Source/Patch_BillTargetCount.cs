using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using RimWorld;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static class Patch_BillTargetCount
        {
            static bool HasNoActiveFilters(Bill_Production bill) {
                if (Find.World.GetComponent<World>().billTargetCountCustomFilters.TryGetValue(bill, out var customFilter))
                    return !customFilter.ActiveFilterRanges.Any();
                return true;
            }

            static void Dialog_BillConfig_AfterQualityRange(Listing_Standard listing, Bill_Production bill) {
                if (!Find.World.GetComponent<World>().billTargetCountCustomFilters.TryGetValue(bill, out var customFilter))
                    return;

                customFilter.DrawMenu(listing.GetRect(24f));

                foreach (var range in customFilter.ActiveFilterRanges) {
                    var rect = listing.GetRect(28f);
                    range.Draw(rect);
                }
            }

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
                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> SkipManualCount(IEnumerable<CodeInstruction> instructions) {
                    // in our case having an active filter is a deliberate action so let's apply it even if the range is the default "anything"
                    // (e.g. we may want things of a def that show a stat, period, regardless of value)
                    var myMethod = AccessTools.Method(typeof(Patch_BillTargetCount), nameof(HasNoActiveFilters));
                    var codes = instructions.ToList();
                    for (var i = 0; i < codes.Count; i++)
                        if (codes[i].operand is FieldInfo fieldInfo && fieldInfo == AccessTools.Field(typeof(Bill_Production), nameof(Bill_Production.hpRange))) {
                            codes.InsertRange(
                                i - 1, new List<CodeInstruction> {
                                    new CodeInstruction(OpCodes.Ldarg_1),
                                    new CodeInstruction(OpCodes.Call, myMethod),
                                    new CodeInstruction(OpCodes.Brfalse, codes[i + 3].operand)
                                });
                            break;
                        }

                    return codes.AsEnumerable();
                }
            }

            [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
            static class Dialog_BillConfig_DoWindowContents_Patch
            {
                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> AfterQualityRange(IEnumerable<CodeInstruction> instructions) {
                    var myMethod = AccessTools.Method(typeof(Patch_BillTargetCount), nameof(Dialog_BillConfig_AfterQualityRange));
                    var codes = instructions.ToList();
                    for (var i = 0; i < codes.Count; i++)
                        if (codes[i].operand is MethodInfo method && method == AccessTools.Method(typeof(Widgets), nameof(Widgets.QualityRange))) {
                            codes.InsertRange(
                                i + 2, new List<CodeInstruction> {
                                    codes[i - 7],
                                    codes[i - 3],
                                    codes[i - 2],
                                    new CodeInstruction(OpCodes.Call, myMethod),
                                });
                            break;
                        }

                    return codes.AsEnumerable();
                }
            }
        }
    }
}