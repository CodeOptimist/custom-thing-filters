using System;
using System.Collections.Generic;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
// ReSharper disable once RedundantUsingDirective
using Debug = System.Diagnostics.Debug;

namespace CustomThingFilters
{
    partial class CustomThingFilters : ModBase
    {
        static string modIdentifier;
        static readonly List<StatThingInfo> statThingInfos = new List<StatThingInfo>();
        static SettingHandle<bool> fixFilteredProductStackCounts;

        public override void DefsLoaded() {
            modIdentifier = ModContentPack.PackageIdPlayerFacing;

            SettingHandle<T> GetSettingHandle<T>(string settingName, T defaultValue = default, SettingHandle.ValueIsValid validator = default,
                SettingHandle.ShouldDisplay shouldDisplay = default, string enumPrefix = default) {
                var settingHandle = Settings.GetHandle(
                    settingName, $"{modIdentifier}_SettingTitle_{settingName}".Translate(), $"{modIdentifier}_SettingDesc_{settingName}".Translate(), defaultValue, validator, enumPrefix);
                settingHandle.VisibilityPredicate = shouldDisplay;
                return settingHandle;
            }

            fixFilteredProductStackCounts = GetSettingHandle("fixFilteredProductStackCounts", true);

            Patch_BugFixes.DefsLoaded(HarmonyInst);
            StatThingInfo.DefsLoaded();
        }

        public override void WorldLoaded() {
            StatThingInfo.WorldLoaded();
        }

        static void InsertCode(ref int i, ref List<CodeInstruction> codes, ref List<CodeInstruction> newCodes, int offset, Func<bool> when, Func<List<CodeInstruction>> what,
            bool bringLabels = false) {
            for (i -= offset; i < codes.Count; i++) {
                if (i >= 0 && when()) {
                    var whatCodes = what();
                    if (bringLabels) {
                        whatCodes[0].labels.AddRange(codes[i + offset].labels);
                        codes[i + offset].labels.Clear();
                    }

                    newCodes.AddRange(whatCodes);
                    if (offset > 0)
                        i += offset;
                    else
                        newCodes.AddRange(codes.GetRange(i + offset, Math.Abs(offset)));
                    break;
                }

                newCodes.Add(codes[i + offset]);
            }
        }

        class MyWorldComponent : WorldComponent
        {
            static readonly string curDataVersion;

            static readonly List<string> releases = new List<string> {"", "1_1_1", "2_0_0", "2_0_1", "2_0_2", "2_0_3"};
            public static readonly Dictionary<string, List<(string find, string replace)>> releaseNewToOldLabels = new Dictionary<string, List<(string, string)>>();

            static readonly List<(List<string> releaseRange, List<(string pattern, string replacement)>)> newToOldLabels = new List<(List<string>, List<(string, string)>)> {
                (releases.GetRange(0, 1), new List<(string pattern, string replacement)> {("^allowedFinal", "allowed")}),
                (releases.GetRange(1, 4), new List<(string pattern, string replacement)> {("^allowedFinal", "allowedCur")}),
            };

            public readonly Dictionary<Bill, CustomFilter> billCustomFilters = new Dictionary<Bill, CustomFilter>();
            public readonly Dictionary<Bill, CustomFilter> billTargetCountCustomFilters = new Dictionary<Bill, CustomFilter>();

            public readonly Dictionary<StorageSettings, CustomFilter> storageSettingsCustomFilters = new Dictionary<StorageSettings, CustomFilter>();
            public readonly Dictionary<ThingFilter, CustomFilter> thingFilterCustomFilters = new Dictionary<ThingFilter, CustomFilter>();
            public string dataVersion;

            static MyWorldComponent() {
                curDataVersion = typeof(CustomThingFilters).Assembly.GetName().Version.ToString();
                curDataVersion = curDataVersion.Substring(0, curDataVersion.LastIndexOf(".", StringComparison.Ordinal)).Replace(".", "_");

                foreach (var (releaseRange, substitutions) in newToOldLabels)
                foreach (var release in releaseRange)
                    releaseNewToOldLabels.Add(release, substitutions);
            }

            public MyWorldComponent(World world) : base(world) {
            }

            public override void ExposeData() {
                if (Scribe.mode == LoadSaveMode.Saving)
                    dataVersion = curDataVersion;
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

        class DrawContext : IDisposable
        {
            readonly Color guiColor;

            readonly TextAnchor textAnchor;

            readonly GameFont textFont;

            public DrawContext() {
                guiColor = GUI.color;
                textFont = Text.Font;
                textAnchor = Text.Anchor;
            }

            public Color GuiColor {
                set => GUI.color = value;
            }

            public GameFont TextFont {
                set => Text.Font = value;
            }

            public TextAnchor TextAnchor {
                set => Text.Anchor = value;
            }

            public void Dispose() {
                GUI.color = guiColor;
                Text.Font = textFont;
                Text.Anchor = textAnchor;
            }
        }
    }
}
