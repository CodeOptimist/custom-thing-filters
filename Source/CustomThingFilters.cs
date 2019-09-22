using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Harmony;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using RimWorld;
using UnityEngine.SceneManagement;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters : ModBase
    {
        static Dictionary<ThingFilter, CustomFilter> thingFilterCustomFilters;
        static Dictionary<StorageSettings, CustomFilter> storageSettingsCustomFilters;
        static Dictionary<Bill, CustomFilter> billCustomFilters;
        static Dictionary<Bill, CustomFilter> billTargetCountCustomFilters;

        static ModLogger _logger;

        static SettingHandle<bool> fixFilteredProductStackCounts;
        public override string ModIdentifier => "COCustomThingFilters";

        public override void DefsLoaded()
        {
            _logger = Logger;

            thingFilterCustomFilters = new Dictionary<ThingFilter, CustomFilter>();
            storageSettingsCustomFilters = new Dictionary<StorageSettings, CustomFilter>();
            billCustomFilters = new Dictionary<Bill, CustomFilter>();
            billTargetCountCustomFilters = new Dictionary<Bill, CustomFilter>();

            fixFilteredProductStackCounts = Settings.GetHandle(
                "fixFilteredProductStackCounts",
                "COCTF_fixFilteredProductStackCountsSetting_title".Translate(),
                "COCTF_fixFilteredProductStackCountsSetting_description".Translate(),
                true);

            if (fixFilteredProductStackCounts)
                HarmonyInst.Patch(
                    typeof(RecipeWorkerCounter).GetMethod("CountValidThings"),
                    transpiler: new HarmonyMethod(typeof(BugFixes), nameof(BugFixes.FilteredProductStackCounts)));

            CustomFilter.DefsLoaded();
        }

        public override void SceneLoaded(Scene scene)
        {
            CustomFilter.SceneLoaded(scene);
        }

        static void Debug(params object[] strings)
        {
#if DEBUG
            _logger.Trace(strings);
#endif
        }

        static void ExposeCustomFilter<T>(Dictionary<T, CustomFilter> dict, T t, ThingFilter filter, string label)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars) {
                var customFilter = new CustomFilter();
                Scribe_Deep.Look(ref customFilter, label);
                if (customFilter != null)
                    foreach (var range in customFilter.filterRanges)
                        range.Load();
                else
                    customFilter = new CustomFilter();

                dict[t] = customFilter;
                if (filter != null)
                    thingFilterCustomFilters[filter] = customFilter;
            } else if (Scribe.mode == LoadSaveMode.Saving && dict.ContainsKey(t)) {
                var customFilter = dict[t];
                Scribe_Deep.Look(ref customFilter, label);
            }
        }

        static void CreateCustomFilter<T>(Dictionary<T, CustomFilter> dict, T __instance, ThingFilter filter)
        {
            var customFilter = new CustomFilter();
            dict.Add(__instance, customFilter);

            if (filter != null)
                thingFilterCustomFilters.Add(filter, customFilter);
        }

        class CustomFilter : IExposable
        {
            static readonly List<ThingDefStatRange> thingDefStatsWRange = new List<ThingDefStatRange>();

            static readonly List<StatDef> needWeaponStats = new[] {
                "RangedWeapon_LongDPS", "RangedWeapon_MediumDPS", "RangedWeapon_ShortDPS", "RangedWeapon_TouchDPS"
            }.Select(DefDatabase<StatDef>.GetNamedSilentFail).Where(x => x != null).ToList();

            static readonly List<StatDef> explicitlyIntegers = new List<StatDef> {
                StatDefOf.MaxHitPoints, StatDefOf.Beauty, StatDefOf.TrapMeleeDamage, StatDefOf.CarryingCapacity, StatDefOf.MeatAmount, StatDefOf.LeatherAmount, StatDefOf.MinimumHandlingSkill
            };

            public readonly List<FilterRange> filterRanges = new List<FilterRange>();

            public CustomFilter()
            {
                foreach (var stat in thingDefStatsWRange) {
                    var filterRange = new FilterRange(
                        $"COCTF_allowed{stat.statDef.defName}", stat.statDef.label, stat.statDef.toStringStyle, stat.min, stat.max,
                        (range, thing) => range.Includes(thing.def.GetStatValueAbstract(stat.statDef)));
                    filterRanges.Add(filterRange);
                }
            }

            public void ExposeData()
            {
                foreach (var range in filterRanges) {
                    if (Scribe.mode == LoadSaveMode.Saving && range.AtDefault()) continue;
                    Scribe_Values.Look(ref range.inner, range.saveLabel, new FloatRange(-9999999f, -9999999f));
                }
            }

            public static void DefsLoaded()
            {
                var pawnCategories = new[] {
                    StatCategoryDefOf.PawnSocial, StatCategoryDefOf.PawnCombat, StatCategoryDefOf.PawnMisc, StatCategoryDefOf.PawnWork, StatCategoryDefOf.BasicsPawn
                };
                var needWeaponAndPawnStats = new[] {
                    "RangedWeapon_LongDPSPawn", "RangedWeapon_MediumDPSPawn", "RangedWeapon_ShortDPSPawn", "RangedWeapon_TouchDPSPawn"
                }.Select(DefDatabase<StatDef>.GetNamedSilentFail).Where(x => x != null).ToList();

                foreach (var statDef in DefDatabase<StatDef>.AllDefs.Where(x => !pawnCategories.Contains(x.category) && !needWeaponAndPawnStats.Contains(x) && !needWeaponStats.Contains(x)))
                    AddStatDefs(statDef);
            }

            [SuppressMessage("ReSharper", "UnusedParameter.Local")]
            public static void SceneLoaded(Scene scene)
            {
                if (!GenScene.InPlayScene)
                    return;

                foreach (var statDef in DefDatabase<StatDef>.AllDefs.Where(x => needWeaponStats.Contains(x)))
                    AddStatDefs(statDef, true);
            }

            static void AddStatDefs(StatDef statDef, bool needWeapon = false)
            {
                float? min = null, max = null;
                var foundFraction = false;
                foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading) {
                    if (needWeapon && (thingDef.Verbs.Count == 0 || thingDef.Verbs[0].defaultProjectile?.projectile == null)) continue;
                    var stat = thingDef.GetStatValueAbstract(statDef);
                    if (!foundFraction && Math.Abs(stat % 1) > TOLERANCE) foundFraction = true;
                    min = Math.Min(min ?? stat, stat);
                    max = Math.Max(max ?? stat, stat);
                }

                if (min == null || float.IsNaN((float) min) || min.Equals(max)) return;
                Log.Warning($"{statDef} {min} {max} {statDef.toStringStyle}");

                if (statDef.toStringStyle == default && !explicitlyIntegers.Contains(statDef) && foundFraction)
                    statDef.toStringStyle = ToStringStyle.FloatTwo;

                thingDefStatsWRange.Add(new ThingDefStatRange {statDef = statDef, min = (float) min, max = (float) max});
            }

            public bool IsAllowed(Thing t)
            {
                foreach (var range in filterRanges)
                    if (!range.IsAllowed(t))
                        return false;
                return true;
            }

            struct ThingDefStatRange
            {
                public StatDef statDef;
                public float min, max;

                public override string ToString()
                {
                    return $"{statDef} {min} {max}";
                }
            }
        }
    }
}