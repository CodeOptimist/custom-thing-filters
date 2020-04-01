﻿using System;
using System.Collections.Generic;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

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
            public static string modVersion;

            public static readonly Dictionary<string, Dictionary<string, string>> versionCompatibilityLabelMap = new Dictionary<string, Dictionary<string, string>> {
                {"", new Dictionary<string, string> {{"allowedCur", "allowed"}}}
            };

            public readonly Dictionary<Bill, CustomFilter> billCustomFilters = new Dictionary<Bill, CustomFilter>();
            public readonly Dictionary<Bill, CustomFilter> billTargetCountCustomFilters = new Dictionary<Bill, CustomFilter>();
            public readonly Dictionary<StorageSettings, CustomFilter> storageSettingsCustomFilters = new Dictionary<StorageSettings, CustomFilter>();
            public readonly Dictionary<ThingFilter, CustomFilter> thingFilterCustomFilters = new Dictionary<ThingFilter, CustomFilter>();
            public string dataVersion;

            public MyWorldComponent(World world) : base(world) {
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
