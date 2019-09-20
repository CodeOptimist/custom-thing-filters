using System.Collections.Generic;
using System.Linq;
using Harmony;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using RimWorld;
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
            static readonly float maxMeatAmount = ThingCategoryDefOf.Corpses.ThisAndChildCategoryDefs.SelectMany(x => x.childThingDefs)
                .Where(x => x.ingestible?.sourceDef?.race?.meatDef != null).Max(x => x.ingestible.sourceDef.GetStatValueAbstract(StatDefOf.MeatAmount));

            static readonly float maxMarketValue = DefDatabase<ThingDef>.AllDefs.Max(x => x.GetStatValueAbstract(StatDefOf.MarketValue));
            static readonly float minBeauty = DefDatabase<ThingDef>.AllDefs.Min(x => x.GetStatValueAbstract(StatDefOf.Beauty));
            static readonly float maxBeauty = DefDatabase<ThingDef>.AllDefs.Max(x => x.GetStatValueAbstract(StatDefOf.Beauty));
            static readonly float maxFlammability = DefDatabase<ThingDef>.AllDefs.Max(x => x.GetStatValueAbstract(StatDefOf.Flammability));
            static readonly float maxWorkToMake = DefDatabase<ThingDef>.AllDefs.Max(x => x.GetStatValueAbstract(StatDefOf.WorkToMake));

            public readonly List<FilterRange> filterRanges = new List<FilterRange> {
                new FilterIntRange(
                    "COCTF_allowedMeatAmount", 0, maxMeatAmount,
                    (range, t) => t.def.ingestible?.sourceDef?.race?.meatDef == null || range.Includes(t.def.ingestible.sourceDef.GetStatValueAbstract(StatDefOf.MeatAmount))),
                new FilterIntRange(
                    "COCTF_allowedMarketValue", 0, maxMarketValue,
                    (range, t) => range.Includes(t.GetStatValue(StatDefOf.MarketValue))),
                new FilterIntRange(
                    "COCTF_allowedBeauty", minBeauty, maxBeauty,
                    (range, t) => range.Includes(t.GetStatValue(StatDefOf.Beauty))),
                new FilterIntRange(
                    "COCTF_allowedFlammability", 0, maxFlammability,
                    (range, t) => range.Includes(t.GetStatValue(StatDefOf.Flammability))),
                new FilterIntRange(
                    "COCTF_allowedWorkToMake", 0, maxWorkToMake,
                    (range, t) => range.Includes(t.GetStatValue(StatDefOf.WorkToMake)))
            };

            public void ExposeData()
            {
                foreach (var range in filterRanges) {
                    if (Scribe.mode == LoadSaveMode.Saving && range.AtDefault()) continue;
                    if (range is FilterIntRange intRange)
                        Scribe_Values.Look(ref intRange.inner, intRange.label, new IntRange(-9999999, -9999999));
                    if (range is FilterFloatRange floatRange)
                        Scribe_Values.Look(ref floatRange.inner, floatRange.label, new FloatRange(-9999999f, -9999999f));
                }
            }

            public static void DefsLoaded()
            {
            }

            public bool IsAllowed(Thing t)
            {
                foreach (var range in filterRanges)
                    if (!range.IsAllowed(t))
                        return false;
                return true;
            }
        }
    }
}