using System;
using System.Collections.Generic;
using HugsLib;
using HugsLib.Settings;
using RimWorld;
using RimWorld.Planet;
using UnityEngine.SceneManagement;
using Verse;

namespace CustomThingFilters
{
    partial class CustomThingFilters : ModBase
    {
        static readonly List<StatThingInfo> statThingInfos = new List<StatThingInfo>();
        static SettingHandle<bool> fixFilteredProductStackCounts;
        public override string ModIdentifier => "COCustomThingFilters";

        public override void DefsLoaded() {
            SettingHandle<T> GetSettingHandle<T>(string settingName, T defaultValue) {
                return Settings.GetHandle(settingName, $"COCTF_{settingName}Setting_title".Translate(), $"COCTF_{settingName}Setting_description".Translate(), defaultValue);
            }

            fixFilteredProductStackCounts = GetSettingHandle("fixFilteredProductStackCounts", true);

            Patch_BugFixes.DefsLoaded(HarmonyInst);
            StatThingInfo.DefsLoaded();
        }

        public override void SceneLoaded(Scene scene) {
            StatThingInfo.SceneLoaded(scene);
        }

        class World : WorldComponent
        {
            public static string modVersion;

            public static readonly Dictionary<string, Dictionary<string, string>> versionCompatibilityLabelMap = new Dictionary<string, Dictionary<string, string>> {
                {"", new Dictionary<string, string> {{"allowedCur", "allowed"}}}
            };

            public readonly Dictionary<Bill, CustomFilter> billCustomFilters = new Dictionary<Bill, CustomFilter>();
            public readonly Dictionary<Bill, CustomFilter> billTargetCountCustomFilters = new Dictionary<Bill, CustomFilter>();
            public readonly Dictionary<StorageSettings, CustomFilter> storageSettingsCustomFilters = new Dictionary<StorageSettings, CustomFilter>();
            public readonly Dictionary<ThingFilter, CustomFilter> thingFilterCustomFilters = new Dictionary<ThingFilter, CustomFilter>();
            public string dataVersion;

            public World(RimWorld.Planet.World world) : base(world) {
                modVersion = typeof(CustomThingFilters).Assembly.GetName().Version.ToString();
                modVersion = modVersion.Substring(0, modVersion.LastIndexOf(".", StringComparison.Ordinal)).Replace(".", "_");
            }

            public override void ExposeData() {
                dataVersion = modVersion + "_";
                Scribe_Values.Look(ref dataVersion, "version");
            }

            public override void FinalizeInit() {
                dataVersion = dataVersion ?? "";
            }

            public void ExposeCustomFilter<T>(Dictionary<T, CustomFilter> dict, T t, ThingFilter filter, string label) {
                dict.TryGetValue(t, out var customFilter);
                Scribe_Deep.Look(ref customFilter, label);

                if (Scribe.mode == LoadSaveMode.LoadingVars) {
                    if (customFilter == null)
                        customFilter = new CustomFilter();

                    foreach (var range in customFilter.filterRanges)
                        range.Load();

                    dict[t] = customFilter;
                    if (filter != null)
                        thingFilterCustomFilters[filter] = customFilter;
                }
            }

            public void CreateCustomFilter<T>(Dictionary<T, CustomFilter> dict, T __instance, ThingFilter filter) {
                var customFilter = new CustomFilter();
                dict.Add(__instance, customFilter);

                if (filter != null)
                    thingFilterCustomFilters.Add(filter, customFilter);
            }
        }
    }
}
