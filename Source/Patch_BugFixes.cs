using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static class Patch_BugFixes
        {
            static List<CodeInstruction> codes, newCodes;
            static int i;

            static void InsertCode(int offset, Func<bool> when, Func<List<CodeInstruction>> what, bool bringLabels = false) {
                CustomThingFilters.InsertCode(ref i, ref codes, ref newCodes, offset, when, what, bringLabels);
            }

            public static void DefsLoaded(Harmony harmony) {
                // AlexTD fixed this same bug
                if (fixFilteredProductStackCounts.Value && !ModLister.HasActiveModWithName("TD Enhancement Pack")) {
                    harmony.Patch(
                        AccessTools.Method(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountValidThings)),
                        transpiler: new HarmonyMethod(typeof(Patch_BugFixes), nameof(ProductStackCounts)));
                }
            }

            // in vanilla as soon as you modify a product count filter, e.g. even "hit points", stack count is ignored; this fixes that
            static IEnumerable<CodeInstruction> ProductStackCounts(IEnumerable<CodeInstruction> instructions) {
                codes = instructions.ToList();
                newCodes = new List<CodeInstruction>();
                i = 0;

                var sequence = new List<CodeInstruction> {
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Add),
                    new CodeInstruction(OpCodes.Stloc_0)
                };
                var comparer = new CodeInstructionComparer();

                InsertCode(
                    0,
                    () => codes.GetRange(i, sequence.Count).SequenceEqual(sequence, comparer),
                    () => new List<CodeInstruction> {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(List<Thing>), "Item").GetGetMethod()),
                        new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Thing), nameof(Thing.stackCount))),

                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Add),
                        new CodeInstruction(OpCodes.Stloc_0),
                    });
                i += 4;

                for (; i < codes.Count; i++)
                    newCodes.Add(codes[i]);
                return newCodes.AsEnumerable();
            }

            class CodeInstructionComparer : IEqualityComparer<CodeInstruction>
            {
                public bool Equals(CodeInstruction x, CodeInstruction y) {
                    if (ReferenceEquals(null, x)) return false;
                    if (ReferenceEquals(null, y)) return false;
                    if (ReferenceEquals(x, y)) return true;
                    return Equals(x.opcode, y.opcode) && Equals(x.operand, y.operand);
                }

                public int GetHashCode(CodeInstruction obj) {
                    return obj.GetHashCode();
                }
            }
        }
    }
}
