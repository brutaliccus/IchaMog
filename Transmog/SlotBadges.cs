using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Transmog
{
    /// <summary>
    /// Adds a small "T" button to the inventory cell of each equipped armor piece.
    /// Works with the vanilla grid and with grids rebuilt by inventory mods (e.g. InventorySlots),
    /// because it runs after them and only reads InventoryGrid.m_elements.
    /// </summary>
    internal static class SlotBadges
    {
        private const string BadgeName = "vpfTransmogBadge";

        private static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> ElementsRef =
            AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");

        private sealed class Badge
        {
            public RectTransform Root;
            public Image Ring;
            public Image Fill;
            public TMP_Text Label;
            public TransmogSlot Slot;
            public float LayoutSize = -1f;
            public IconCorner LayoutCorner;
        }

        private static readonly Dictionary<InventoryElement, Badge> Badges = new Dictionary<InventoryElement, Badge>();
        private static readonly HashSet<InventoryElement> Shown = new HashSet<InventoryElement>();
        private static readonly List<InventoryElement> Dead = new List<InventoryElement>();
        private static bool _layoutDirty;
        private static bool _loggedError;

        public static void MarkLayoutDirty() => _layoutDirty = true;

        public static void Update(InventoryGui gui, Player player)
        {
            Shown.Clear();
            var grid = gui != null ? gui.m_playerGrid : null;
            bool active = grid != null && player != null && TransmogPlugin.Enabled.Value && TransmogPlugin.ShowSlotIcons.Value;

            if (active)
            {
                var inv = grid.GetInventory();
                var elements = ElementsRef(grid);
                if (inv != null && elements != null)
                {
                    int width = inv.GetWidth();
                    foreach (var item in inv.GetAllItems())
                    {
                        if (item?.m_shared == null || !item.m_equipped) continue;
                        if (!TransmogSlots.TryFromItemType(item.m_shared.m_itemType, out var slot)) continue;
                        if (!TransmogSlots.IsActive(slot)) continue;
                        if (TransmogSlots.GetEquipped(player, slot) != item) continue;

                        var el = FindElement(elements, width, item.m_gridPos);
                        if (el == null || !el.gameObject.activeInHierarchy) continue;

                        var badge = GetOrCreate(el);
                        if (badge == null) continue;
                        Bind(badge, player, slot);
                        Shown.Add(el);
                    }
                }
            }

            Dead.Clear();
            foreach (var kv in Badges)
            {
                if (kv.Key == null || kv.Value.Root == null) { Dead.Add(kv.Key); continue; }
                if (!Shown.Contains(kv.Key) && kv.Value.Root.gameObject.activeSelf) kv.Value.Root.gameObject.SetActive(false);
            }
            foreach (var d in Dead) Badges.Remove(d);
            _layoutDirty = false;
        }

        private static InventoryElement FindElement(List<InventoryElement> elements, int width, Vector2i pos)
        {
            int idx = pos.y * width + pos.x;
            if (idx >= 0 && idx < elements.Count)
            {
                var el = elements[idx];
                if (el != null && el.Position.x == pos.x && el.Position.y == pos.y) return el;
            }
            foreach (var el in elements)
                if (el != null && el.Position.x == pos.x && el.Position.y == pos.y) return el;
            return null;
        }

        private static Badge GetOrCreate(InventoryElement el)
        {
            if (Badges.TryGetValue(el, out var b) && b.Root != null) return b;
            try
            {
                var parent = el.transform;
                var existing = parent.Find(BadgeName);
                if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);

                b = new Badge();
                b.Root = UiUtil.Rect(BadgeName, parent);
                b.Ring = UiUtil.Img(b.Root, UiUtil.Circle, UiUtil.Gold, true);

                var fill = UiUtil.Rect("fill", b.Root);
                UiUtil.Place(fill, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
                b.Fill = UiUtil.Img(fill, UiUtil.Circle, UiUtil.Dark, false);

                var label = UiUtil.Rect("label", b.Root);
                UiUtil.Stretch(label);
                b.Label = UiUtil.Text(label, "T", 12f, UiUtil.GameFont(), TextAlignmentOptions.Center);
                b.Label.fontStyle = FontStyles.Bold;

                var badgeRef = b;
                UiUtil.Button(b.Root, b.Ring, () => Picker.Open(Player.m_localPlayer, badgeRef.Slot));

                Badges[el] = b;
                return b;
            }
            catch (Exception ex)
            {
                if (!_loggedError) { _loggedError = true; TransmogPlugin.Log.LogWarning("Could not create slot icon: " + ex); }
                return null;
            }
        }

        private static void Bind(Badge b, Player player, TransmogSlot slot)
        {
            b.Slot = slot;
            if (!b.Root.gameObject.activeSelf) b.Root.gameObject.SetActive(true);

            float size = TransmogPlugin.IconSize.Value;
            var corner = TransmogPlugin.IconCornerSetting.Value;
            if (_layoutDirty || b.LayoutSize != size || b.LayoutCorner != corner)
            {
                b.LayoutSize = size;
                b.LayoutCorner = corner;
                bool right = corner == IconCorner.TopRight || corner == IconCorner.BottomRight;
                bool top = corner == IconCorner.TopLeft || corner == IconCorner.TopRight;
                var a = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
                b.Root.anchorMin = a;
                b.Root.anchorMax = a;
                b.Root.pivot = new Vector2(0.5f, 0.5f);
                b.Root.sizeDelta = new Vector2(size, size);
                float inset = size * 0.3f;
                b.Root.anchoredPosition = new Vector2(right ? -inset : inset, top ? -inset : inset);
                b.Label.fontSize = size * 0.62f;
            }

            bool hasLook = TransmogStore.TryGet(player, slot, out _);
            b.Fill.color = hasLook ? UiUtil.Gold : UiUtil.Dark;
            b.Label.color = hasLook ? new Color(0.12f, 0.08f, 0.02f, 1f) : UiUtil.Gold;
            b.Root.SetAsLastSibling();
        }

        public static void DestroyAll()
        {
            foreach (var kv in Badges)
                if (kv.Value.Root != null) UnityEngine.Object.Destroy(kv.Value.Root.gameObject);
            Badges.Clear();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateInventory")]
    [HarmonyAfter("sighsorry.InventorySlots")]
    internal static class InventoryGuiUpdateInventoryPatch
    {
        private static bool _logged;

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(InventoryGui __instance, Player player)
        {
            try
            {
                SlotBadges.Update(__instance, player);
            }
            catch (Exception ex)
            {
                if (!_logged) { _logged = true; TransmogPlugin.Log.LogWarning("Slot icon update failed: " + ex); }
            }
        }
    }
}
