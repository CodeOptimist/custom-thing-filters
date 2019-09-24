using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using RimWorld;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters
    {
        class StatThingInfo
        {
            static readonly List<StatDef> explicitlyIntegers = new List<StatDef> {
                StatDefOf.MaxHitPoints, StatDefOf.Beauty, StatDefOf.TrapMeleeDamage, StatDefOf.CarryingCapacity, StatDefOf.MeatAmount, StatDefOf.LeatherAmount, StatDefOf.MinimumHandlingSkill
            };

            static readonly List<StatDef> needThingStats = new[] {
                "RangedWeapon_LongDPS", "RangedWeapon_MediumDPS", "RangedWeapon_ShortDPS", "RangedWeapon_TouchDPS"
            }.Select(DefDatabase<StatDef>.GetNamedSilentFail).Where(x => x != null).ToList();

            public readonly float max;
            public readonly float min;

            public readonly StatDef statDef;
            public readonly Dictionary<ThingDef, float> thingDefValues;

            StatThingInfo(StatDef statDef, Dictionary<ThingDef, float> thingDefValues, float min, float max)
            {
                this.statDef = statDef;
                this.thingDefValues = thingDefValues;
                this.min = min;
                this.max = max;
            }

            public static void DefsLoaded()
            {
                var pawnCategories = new[] {
                    StatCategoryDefOf.PawnSocial, StatCategoryDefOf.PawnCombat, StatCategoryDefOf.PawnMisc, StatCategoryDefOf.PawnWork, StatCategoryDefOf.BasicsPawn
                };
                var needThingAndPawnStats = new[] {
                    "RangedWeapon_LongDPSPawn", "RangedWeapon_MediumDPSPawn", "RangedWeapon_ShortDPSPawn", "RangedWeapon_TouchDPSPawn"
                }.Select(DefDatabase<StatDef>.GetNamedSilentFail).Where(x => x != null).ToList();

                foreach (var statDef in DefDatabase<StatDef>.AllDefs.Except(needThingStats).Except(needThingAndPawnStats).Where(x => !pawnCategories.Contains(x.category))) {
                    var info = CreateInstance(statDef);
                    if (info != null) statThingInfos.Add(info);
                }
            }

            [SuppressMessage("ReSharper", "UnusedParameter.Local")]
            public static void SceneLoaded(Scene scene)
            {
                if (!GenScene.InPlayScene)
                    return;

                // some mods use MakeThing() with their custom stats, which is going to fail unless a scene is loaded
                foreach (var statDef in needThingStats) {
                    var info = CreateInstance(statDef);
                    if (info != null) statThingInfos.Add(info);
                }
            }

            static StatThingInfo CreateInstance(StatDef statDef)
            {
                float? min = null, max = null;
                var foundFraction = false;
                var thingDefValues = new Dictionary<ThingDef, float>();

                foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading) {
                    if (!statDef.Worker.ShouldShowFor(StatRequest.For(thingDef, null)))
                        continue;
                    var value = thingDef.GetStatValueAbstract(statDef);
                    if (Mathf.Approximately(value, statDef.hideAtValue))
                        continue;

                    if (!foundFraction && !Mathf.Approximately(value % 1, 0)) foundFraction = true;
                    min = Math.Min(min ?? value, value);
                    max = Math.Max(max ?? value, value);
                    thingDefValues.Add(thingDef, value);
                }

                if (min == null || float.IsNaN((float) min) || min.Equals(max)) return null;
                //Trace.WriteLine($"{statDef} \"{statDef.label}\" {min} {max} {statDef.toStringStyle} \"{string.Join(", ", thingDefValues.Select(x => x.ToString()).ToArray())}\"");

                if (statDef.toStringStyle == default && !explicitlyIntegers.Contains(statDef) && foundFraction)
                    statDef.toStringStyle = ToStringStyle.FloatTwo;

                var result = new StatThingInfo(statDef, thingDefValues, (float) min, (float) max);
                return result;
            }
        }
    }
}