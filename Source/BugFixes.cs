using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Harmony;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        static class BugFixes
        {
            public static IEnumerable<CodeInstruction> FilteredProductStackCounts(IEnumerable<CodeInstruction> instructions)
            {
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
                                new CodeInstruction(OpCodes.Callvirt, typeof(List<Thing>).GetMethod("get_Item")),
                                new CodeInstruction(OpCodes.Ldfld, typeof(Thing).GetField(nameof(Thing.stackCount))),

                                new CodeInstruction(OpCodes.Ldloc_0),
                                new CodeInstruction(OpCodes.Add),
                                new CodeInstruction(OpCodes.Stloc_0)
                            });
                        break;
                    }

                return codes.AsEnumerable();
            }

            [HarmonyPatch(typeof(Widgets), nameof(Widgets.FloatRange))]
            static class Widgets_FloatRange_Patch
            {
                [HarmonyTranspiler]
                static IEnumerable<CodeInstruction> FloatRangeDraw(IEnumerable<CodeInstruction> instructions)
                {
                    var sequence = new List<CodeInstruction> {
                        new CodeInstruction(OpCodes.Mul),
                        new CodeInstruction(OpCodes.Ldarg_3),
                        new CodeInstruction(OpCodes.Ldarg_S, (byte) 4),
                        new CodeInstruction(OpCodes.Ldarg_3),
                        new CodeInstruction(OpCodes.Sub),
                        new CodeInstruction(OpCodes.Div),
                        new CodeInstruction(OpCodes.Sub)
                    };

                    var comparer = new CodeInstructionComparer();
                    var codes = instructions.ToList();

                    for (var i = 0; i < codes.Count - sequence.Count; i++)
                        if (codes.GetRange(i, sequence.Count).SequenceEqual(sequence, comparer)) {
                            codes.RemoveRange(i, sequence.Count);
                            codes.InsertRange(
                                i,
                                new[] {
                                    new CodeInstruction(OpCodes.Ldarg_3),
                                    new CodeInstruction(OpCodes.Sub),
                                    new CodeInstruction(OpCodes.Mul),
                                    new CodeInstruction(OpCodes.Ldarg_S, (byte) 4),
                                    new CodeInstruction(OpCodes.Ldarg_3),
                                    new CodeInstruction(OpCodes.Sub),
                                    new CodeInstruction(OpCodes.Div)
                                });
                        }

                    return codes.AsEnumerable();
                }
            }

            class CodeInstructionComparer : IEqualityComparer<CodeInstruction>
            {
                public bool Equals(CodeInstruction x, CodeInstruction y)
                {
                    if (ReferenceEquals(null, x)) return false;
                    if (ReferenceEquals(null, y)) return false;
                    if (ReferenceEquals(x, y)) return true;
                    return Equals(x.opcode, y.opcode) && Equals(x.operand, y.operand);
                }

                public int GetHashCode(CodeInstruction obj)
                {
                    return obj.GetHashCode();
                }
            }
        }
    }
}