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
        static class BillTargetCount
        {
            [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
            public static bool IsCountFilterAtDefaults(Bill_Production bill)
            {
                if (billTargetCountCustomFilters.ContainsKey(bill)) {
                    var customFilter = billTargetCountCustomFilters[bill];
                    foreach (var range in customFilter.filterRanges)
                        if (!range.AtDefault())
                            return false;
                }

                return true;
            }

            [SuppressMessage("ReSharper", "MemberCanBePrivate.Local")]
            public static void Dialog_BillConfig_AfterQualityRange(Listing_Standard standard, Bill_Production bill)
            {
                if (!billTargetCountCustomFilters.ContainsKey(bill))
                    return;
                var customFilter = billTargetCountCustomFilters[bill];

                foreach (var range in customFilter.filterRanges) {
                    var rect = standard.GetRect(28f);
                    range.Draw(rect);
                }
            }

            [HarmonyPatch(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountValidThing))]
            static class RecipeWorkerCounter_CountValidThing_Patch
            {
                [HarmonyPostfix]
                [SuppressMessage("ReSharper", "UnusedParameter.Local")]
                static void ExcludeFiltered(ref bool __result, Thing thing, Bill_Production bill, ThingDef def)
                {
                    if (!billTargetCountCustomFilters.ContainsKey(bill))
                        return;
                    var customFilter = billTargetCountCustomFilters[bill];
                    if (!customFilter.IsAllowed(thing))
                        __result = false;
                }
            }

            [HarmonyPatch(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountProducts))]
            static class RecipeWorkerCounter_CountProducts_Patch
            {
                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> IsTargetCountFilterSet(IEnumerable<CodeInstruction> instructions)
                {
                    var myMethod = typeof(BillTargetCount).GetMethod(nameof(IsCountFilterAtDefaults));
                    var codes = instructions.ToList();
                    for (var i = 0; i < codes.Count; i++)
                        if (codes[i].operand is FieldInfo fieldInfo && fieldInfo == typeof(Bill_Production).GetField(nameof(Bill_Production.hpRange))) {
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
                static IEnumerable<CodeInstruction> AfterQualityRange(IEnumerable<CodeInstruction> instructions)
                {
                    var myMethod = typeof(BillTargetCount).GetMethod(nameof(Dialog_BillConfig_AfterQualityRange));
                    var codes = instructions.ToList();
                    for (var i = 0; i < codes.Count; i++)
                        if (codes[i].operand is MethodInfo method && method == typeof(Widgets).GetMethod("QualityRange")) {
                            codes.InsertRange(
                                i + 2, new List<CodeInstruction> {
                                    codes[i - 7],
                                    codes[i - 3],
                                    codes[i - 2]
                                });

                            codes.Insert(i + 5, new CodeInstruction(OpCodes.Call, myMethod));
                            break;
                        }

                    return codes.AsEnumerable();
                }
            }
        }
    }
}