using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Harmony;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static class Patch_BugFixes
        {
            public static void DefsLoaded(HarmonyInstance harmonyInst) {
                if (fixFilteredProductStackCounts)
                    harmonyInst.Patch(
                        AccessTools.Method(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountValidThings)),
                        transpiler: new HarmonyMethod(typeof(Patch_BugFixes), nameof(ProductStackCounts)));
            }

            // in vanilla as soon as you modify a product count filter, e.g. even "hit points", stack count is ignored; this fixes that
            static IEnumerable<CodeInstruction> ProductStackCounts(IEnumerable<CodeInstruction> instructions) {
                var sequence = new List<CodeInstruction> {
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Add),
                    new CodeInstruction(OpCodes.Stloc_0)
                };

                var comparer = new CodeInstructionComparer();
                var codes = instructions.ToList();
                for (var i = 0; i < codes.Count - sequence.Count; i++)
                    if (codes.GetRange(i, sequence.Count).SequenceEqual(sequence, comparer)) {
                        codes.RemoveRange(i, sequence.Count);
                        codes.InsertRange(
                            i,
                            new[] {
                                new CodeInstruction(OpCodes.Ldarg_1),
                                new CodeInstruction(OpCodes.Ldloc_1),
                                new CodeInstruction(OpCodes.Callvirt, AccessTools.Property(typeof(List<Thing>), "Item").GetGetMethod()),
                                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Thing), nameof(Thing.stackCount))),

                                new CodeInstruction(OpCodes.Ldloc_0),
                                new CodeInstruction(OpCodes.Add),
                                new CodeInstruction(OpCodes.Stloc_0)
                            });
                        break;
                    }

                return codes.AsEnumerable();
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