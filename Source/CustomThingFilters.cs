using System.Collections.Generic;
using Harmony;
using HugsLib;
using HugsLib.Settings;
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
        static readonly List<StatThingInfo> statThingInfos = new List<StatThingInfo>();

        static SettingHandle<bool> fixFilteredProductStackCounts;
        public override string ModIdentifier => "COCustomThingFilters";

        public override void DefsLoaded()
        {
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
                    transpiler: new HarmonyMethod(typeof(BugFixes), nameof(BugFixes.ProductStackCounts)));

            StatThingInfo.DefsLoaded();
        }

        public override void SceneLoaded(Scene scene)
        {
            StatThingInfo.SceneLoaded(scene);
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
    }
}