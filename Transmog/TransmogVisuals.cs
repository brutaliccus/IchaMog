using System;
using HarmonyLib;
using UnityEngine;

namespace Transmog
{
    /// <summary>
    /// Swaps the item hash handed to VisEquipment while a Player is setting up its visuals.
    /// The swapped hash is what VisEquipment writes into the ZDO, so other clients render the
    /// chosen look too. Stats are untouched because only the visual hash changes.
    /// </summary>
    internal static class TransmogVisuals
    {
        // Player whose equipment visuals are being set up right now (main thread only).
        internal static Player Current;

        public static int Resolve(TransmogSlot slot, int itemHash, ref int variant)
        {
            var player = Current;
            if (player == null || itemHash == 0 || !TransmogSlots.IsActive(slot)) return itemHash;
            if (!TransmogStore.TryGet(player, slot, out var entry)) return itemHash;

            if (entry.Hidden)
                return TransmogPlugin.AllowHide.Value ? 0 : itemHash;

            var db = ObjectDB.instance;
            if (db == null) return itemHash;

            int hash = entry.Prefab.GetStableHashCode();
            var prefab = db.GetItemPrefab(hash);
            if (prefab == null) return itemHash;
            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData?.m_shared == null) return itemHash;
            if (drop.m_itemData.m_shared.m_itemType != TransmogSlots.ToItemType(slot)) return itemHash;

            if (slot == TransmogSlot.Shoulder)
            {
                int max = Math.Max(0, (drop.m_itemData.m_shared.m_icons?.Length ?? 1) - 1);
                variant = Mathf.Clamp(entry.Variant, 0, max);
            }
            return hash;
        }

        // Optional: mods that skip per-frame equipment visual work while nothing changed (for example Valheim Performance
        // Fixes, RenderTrim.VisUpdate.ForceRefresh) are told that this character's look was re-applied. Looked up once by
        // name; nothing happens when no such mod is installed.
        private static System.Reflection.MethodInfo _forceRefresh;
        private static bool _forceRefreshLooked;

        private static void NotifyVisualChange(VisEquipment vis)
        {
            try
            {
                if (!_forceRefreshLooked)
                {
                    _forceRefreshLooked = true;
                    var t = AccessTools.TypeByName("RenderTrim.VisUpdate");
                    _forceRefresh = t == null ? null : AccessTools.Method(t, "ForceRefresh", new[] { typeof(VisEquipment) });
                }
                _forceRefresh?.Invoke(null, new object[] { vis });
            }
            catch
            {
                _forceRefresh = null;
            }
        }

        /// <summary>Re-applies the five armor visuals of a locally owned player.</summary>
        public static void Refresh(Player player)
        {
            if (player == null) return;
            try
            {
                var vis = player.GetComponent<VisEquipment>();
                if (vis == null) return;
                var nview = player.GetComponent<ZNetView>();
                if (nview != null && nview.GetZDO() != null && !nview.IsOwner()) return;

                var prev = Current;
                Current = player;
                try
                {
                    vis.SetHelmetItem(TransmogSlots.Hash(TransmogSlots.GetEquipped(player, TransmogSlot.Helmet)));
                    vis.SetChestItem(TransmogSlots.Hash(TransmogSlots.GetEquipped(player, TransmogSlot.Chest)));
                    vis.SetLegItem(TransmogSlots.Hash(TransmogSlots.GetEquipped(player, TransmogSlot.Legs)));
                    var sh = TransmogSlots.GetEquipped(player, TransmogSlot.Shoulder);
                    vis.SetShoulderItem(TransmogSlots.Hash(sh), sh != null ? sh.m_variant : 0, sh != null ? sh.m_quality : 0);
                    vis.SetUtilityItem(TransmogSlots.Hash(TransmogSlots.GetEquipped(player, TransmogSlot.Utility)));
                    NotifyVisualChange(vis);
                }
                finally
                {
                    Current = prev;
                }
            }
            catch (Exception ex)
            {
                TransmogPlugin.Log.LogWarning("Refresh failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(Humanoid), "SetupVisEquipment")]
    internal static class SetupVisEquipmentPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Humanoid __instance, out Player __state)
        {
            __state = TransmogVisuals.Current;
            TransmogVisuals.Current = __instance as Player;
        }

        private static Exception Finalizer(Exception __exception, Player __state)
        {
            TransmogVisuals.Current = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetHelmetItem))]
    internal static class SetHelmetItemPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref int itemHash) { int v = 0; itemHash = TransmogVisuals.Resolve(TransmogSlot.Helmet, itemHash, ref v); }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetChestItem))]
    internal static class SetChestItemPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref int itemHash) { int v = 0; itemHash = TransmogVisuals.Resolve(TransmogSlot.Chest, itemHash, ref v); }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetLegItem))]
    internal static class SetLegItemPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref int itemHash) { int v = 0; itemHash = TransmogVisuals.Resolve(TransmogSlot.Legs, itemHash, ref v); }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetUtilityItem))]
    internal static class SetUtilityItemPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref int itemHash) { int v = 0; itemHash = TransmogVisuals.Resolve(TransmogSlot.Utility, itemHash, ref v); }
    }

    [HarmonyPatch(typeof(VisEquipment), nameof(VisEquipment.SetShoulderItem))]
    internal static class SetShoulderItemPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref int itemHash, ref int variant) { itemHash = TransmogVisuals.Resolve(TransmogSlot.Shoulder, itemHash, ref variant); }
    }

    // m_customData is loaded after the inventory, so re-apply once it is available.
    [HarmonyPatch(typeof(Player), nameof(Player.Load))]
    internal static class PlayerLoadPatch
    {
        private static void Postfix(Player __instance) { TransmogVisuals.Refresh(__instance); }
    }

    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal static class PlayerOnSpawnedPatch
    {
        private static void Postfix(Player __instance) { TransmogVisuals.Refresh(__instance); }
    }
}
