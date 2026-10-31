using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Transmog
{
    public enum IconCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    [BepInPlugin(Guid, PluginName, Version)]
    [BepInDependency("sighsorry.InventorySlots", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class TransmogPlugin : BaseUnityPlugin
    {
        public const string Guid = "vpf.Transmog";
        public const string PluginName = "Transmog";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static readonly ConfigEntry<bool>[] SlotEnabled = new ConfigEntry<bool>[TransmogSlots.Count];
        internal static ConfigEntry<bool> AllowKnownItems;
        internal static ConfigEntry<bool> AllowAllItems;
        internal static ConfigEntry<bool> AllowHide;
        internal static ConfigEntry<bool> ShowGoldenVariants;
        internal static ConfigEntry<bool> ShowEtherealVariants;
        internal static ConfigEntry<bool> ShowOtherVariants;
        internal static ConfigEntry<bool> ShowSlotIcons;
        internal static ConfigEntry<IconCorner> IconCornerSetting;
        internal static ConfigEntry<float> IconSize;
        internal static ConfigEntry<KeyboardShortcut> PickerHotkey;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("1 - General", "Enabled", true,
                "Master switch. When off, every piece renders with its real model and the slot icons are hidden.");

            bool[] defaults = { true, true, true, true, false };
            for (int i = 0; i < TransmogSlots.Count; i++)
            {
                SlotEnabled[i] = Config.Bind("2 - Slots", TransmogSlots.Labels[i], defaults[i],
                    "Allow transmog on the " + TransmogSlots.Labels[i].ToLowerInvariant() + " slot.");
            }

            AllowKnownItems = Config.Bind("3 - Picker", "AllowKnownItems", true,
                "Besides items in your inventory, also offer items your character has discovered (known material or recipe).");
            AllowAllItems = Config.Bind("3 - Picker", "AllowAllItems", false,
                "Offer every armor item of the slot type, even ones never seen (cheat-ish, off by default).");
            AllowHide = Config.Bind("3 - Picker", "AllowHide", true,
                "Allow the 'Hide' choice, which renders the slot as empty.");
            ShowGoldenVariants = Config.Bind("3 - Picker", "ShowGoldenVariants", false,
                "Also list the game's golden 'Fallen Warrior' copies of armor and capes (NPC versions that share the real item's name and model). Off: each item is listed once.");
            ShowEtherealVariants = Config.Bind("3 - Picker", "ShowEtherealVariants", false,
                "Also list the game's ghostly/ethereal spirit copies of armor and capes (NPC versions that share the real item's name and model). Off: each item is listed once.");
            ShowOtherVariants = Config.Bind("3 - Picker", "ShowOtherVariants", false,
                "Also list any other same-name, same-model copies of an armor item that are neither golden nor ethereal.");
            MigrateOldVariantSetting();

            ShowSlotIcons = Config.Bind("4 - Interface", "ShowSlotIcons", true,
                "Show the small transmog button on equipped armor in the inventory.");
            IconCornerSetting = Config.Bind("4 - Interface", "IconCorner", IconCorner.TopLeft,
                "Corner of the item slot where the transmog button is drawn.");
            IconSize = Config.Bind("4 - Interface", "IconSize", 20f,
                new ConfigDescription("Size of the transmog button in pixels.", new AcceptableValueRange<float>(10f, 48f)));
            PickerHotkey = Config.Bind("4 - Interface", "OpenPickerHotkey", new KeyboardShortcut(KeyCode.T, KeyCode.LeftControl),
                "Opens/closes the transmog picker while the inventory is open (works without any inventory mod).");

            Enabled.SettingChanged += (_, __) => RefreshLocal();
            AllowHide.SettingChanged += (_, __) => RefreshLocal();
            foreach (var e in SlotEnabled) e.SettingChanged += (_, __) => RefreshLocal();
            IconCornerSetting.SettingChanged += (_, __) => SlotBadges.MarkLayoutDirty();
            IconSize.SettingChanged += (_, __) => SlotBadges.MarkLayoutDirty();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(TransmogPlugin).Assembly);
            Log.LogInfo(PluginName + " " + Version + " loaded");
        }

        private static void RefreshLocal()
        {
            if (Player.m_localPlayer != null) TransmogVisuals.Refresh(Player.m_localPlayer);
            Picker.RebuildIfOpen();
        }

        // 1.0 builds had a single "ShowCapeVariants" switch; carry its value over once.
        private void MigrateOldVariantSetting()
        {
            try
            {
                var prop = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (!(prop?.GetValue(Config) is Dictionary<ConfigDefinition, string> orphans)) return;
                var def = new ConfigDefinition("3 - Picker", "ShowCapeVariants");
                if (!orphans.TryGetValue(def, out var raw)) return;
                orphans.Remove(def);
                if (bool.TryParse(raw?.Trim(), out bool old) && old)
                {
                    ShowGoldenVariants.Value = true;
                    ShowEtherealVariants.Value = true;
                    ShowOtherVariants.Value = true;
                }
                Config.Save();
                Log.LogInfo("Migrated ShowCapeVariants=" + raw + " to the per-kind variant settings.");
            }
            catch (Exception e)
            {
                Log.LogWarning("Could not migrate ShowCapeVariants: " + e.Message);
            }
        }
        private void Update()
        {
            if (!Enabled.Value)
            {
                if (Picker.IsOpen) Picker.Close();
                return;
            }

            Picker.Tick();

            var player = Player.m_localPlayer;
            if (player != null && InventoryGui.IsVisible() && PickerHotkey.Value.IsDown())
            {
                Picker.Toggle(player);
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Picker.DestroyUi();
            SlotBadges.DestroyAll();
        }
    }
}
