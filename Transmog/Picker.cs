using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Transmog
{
    /// <summary>Simple uGUI window listing looks for one armor slot.</summary>
    internal static class Picker
    {
        private sealed class Candidate
        {
            public string Prefab;
            public int Variant;
            public string Name;
            public Sprite Icon;
            public bool Owned;
        }

        private static GameObject _root;
        private static RectTransform _content;
        private static ScrollRect _scroll;
        private static TMP_Text _title;
        private static TMP_Text _info;
        private static TMP_Text _hover;
        private static TMP_Text _empty;
        private static Button _hideButton;
        private static readonly Button[] Tabs = new Button[TransmogSlots.Count];
        private static readonly Image[] TabImages = new Image[TransmogSlots.Count];
        private static Player _player;
        private static TransmogSlot _slot;

        public static bool IsOpen => _root != null && _root.activeSelf;

        public static void Toggle(Player player)
        {
            if (IsOpen) { Close(); return; }
            Open(player, DefaultSlot(player));
        }

        private static TransmogSlot DefaultSlot(Player player)
        {
            for (int i = 0; i < TransmogSlots.Count; i++)
            {
                var s = (TransmogSlot)i;
                if (TransmogSlots.IsActive(s) && TransmogSlots.GetEquipped(player, s) != null) return s;
            }
            for (int i = 0; i < TransmogSlots.Count; i++)
                if (TransmogSlots.IsActive((TransmogSlot)i)) return (TransmogSlot)i;
            return TransmogSlot.Chest;
        }

        public static void Open(Player player, TransmogSlot slot)
        {
            if (player == null || !TransmogPlugin.Enabled.Value || InventoryGui.instance == null) return;
            try
            {
                if (!EnsureBuilt()) return;
                _player = player;
                _slot = TransmogSlots.IsActive(slot) ? slot : DefaultSlot(player);
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
                if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
                Rebuild();
            }
            catch (Exception ex)
            {
                TransmogPlugin.Log.LogWarning("Could not open transmog picker: " + ex);
            }
        }

        public static void Close()
        {
            if (_root != null) _root.SetActive(false);
            _player = null;
        }

        public static void RebuildIfOpen()
        {
            if (IsOpen) Rebuild();
        }

        public static void Tick()
        {
            if (!IsOpen) return;
            if (!InventoryGui.IsVisible() || Player.m_localPlayer == null || _player != Player.m_localPlayer) Close();
        }

        public static void DestroyUi()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        // ---------------------------------------------------------------- building

        private static bool EnsureBuilt()
        {
            if (_root != null) return true;
            var gui = InventoryGui.instance;
            var canvas = gui.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.rootCanvas.transform : gui.transform;
            var font = UiUtil.GameFont();

            var root = UiUtil.Rect("vpfTransmogPicker", parent);
            UiUtil.Stretch(root);
            var c = root.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 30000;
            root.gameObject.AddComponent<GraphicRaycaster>();
            _root = root.gameObject;

            // click outside closes
            var blocker = UiUtil.Rect("blocker", root);
            UiUtil.Stretch(blocker);
            var blockImg = UiUtil.Img(blocker, null, new Color(0f, 0f, 0f, 0.45f), true);
            UiUtil.Button(blocker, blockImg, Close).transition = Selectable.Transition.None;

            var panel = UiUtil.Rect("panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(660f, 580f);
            panel.anchoredPosition = Vector2.zero;
            UiUtil.Img(panel, UiUtil.Rounded, UiUtil.Gold, true);
            var inner = UiUtil.Rect("inner", panel);
            UiUtil.Place(inner, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            UiUtil.Img(inner, UiUtil.Rounded, UiUtil.Panel, true);

            var title = UiUtil.Rect("title", inner);
            UiUtil.Place(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -42), new Vector2(-16, -8));
            _title = UiUtil.Text(title, "Transmog", 24f, font, TextAlignmentOptions.Center);
            _title.color = UiUtil.Gold;

            float tabW = (660f - 4f - 32f - 4f * (TransmogSlots.Count - 1)) / TransmogSlots.Count;
            for (int i = 0; i < TransmogSlots.Count; i++)
            {
                var tab = UiUtil.Rect("tab" + i, inner);
                tab.anchorMin = tab.anchorMax = new Vector2(0, 1);
                tab.pivot = new Vector2(0, 1);
                tab.sizeDelta = new Vector2(tabW, 30f);
                tab.anchoredPosition = new Vector2(16f + i * (tabW + 4f), -48f);
                TabImages[i] = UiUtil.Img(tab, UiUtil.Rounded, UiUtil.Dark, true);
                var slot = (TransmogSlot)i;
                Tabs[i] = UiUtil.Button(tab, TabImages[i], () => { _slot = slot; Rebuild(); });
                var lt = UiUtil.Rect("label", tab);
                UiUtil.Stretch(lt);
                UiUtil.Text(lt, TransmogSlots.Labels[i], 16f, font, TextAlignmentOptions.Center);
            }

            var info = UiUtil.Rect("info", inner);
            UiUtil.Place(info, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -110), new Vector2(-16, -84));
            _info = UiUtil.Text(info, "", 16f, font, TextAlignmentOptions.Left);

            // scroll list
            var scrollRt = UiUtil.Rect("scroll", inner);
            UiUtil.Place(scrollRt, Vector2.zero, Vector2.one, new Vector2(16, 96), new Vector2(-16, -116));
            UiUtil.Img(scrollRt, UiUtil.Rounded, new Color(0f, 0f, 0f, 0.35f), true);
            _scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            var viewport = UiUtil.Rect("viewport", scrollRt);
            UiUtil.Place(viewport, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            viewport.gameObject.AddComponent<RectMask2D>();
            UiUtil.Img(viewport, null, new Color(0, 0, 0, 0.01f), true);
            _content = UiUtil.Rect("content", viewport);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;
            var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(64f, 64f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperLeft;
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;

            var empty = UiUtil.Rect("empty", scrollRt);
            UiUtil.Stretch(empty);
            _empty = UiUtil.Text(empty, "", 16f, font, TextAlignmentOptions.Center);
            _empty.textWrappingMode = TextWrappingModes.Normal;
            _empty.color = new Color(1f, 1f, 1f, 0.6f);

            var hover = UiUtil.Rect("hover", inner);
            UiUtil.Place(hover, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 62), new Vector2(-16, 90));
            _hover = UiUtil.Text(hover, "", 17f, font, TextAlignmentOptions.Center);
            _hover.color = UiUtil.Gold;

            MakeBottomButton(inner, "Clear transmog", 0, font, () => ApplyEntry(null));
            _hideButton = MakeBottomButton(inner, "Hide slot", 1, font, () => ApplyEntry(new TransmogEntry(TransmogStore.HiddenToken, 0)));
            MakeBottomButton(inner, "Close", 2, font, Close);

            _root.SetActive(false);
            return true;
        }

        private static Button MakeBottomButton(RectTransform parent, string text, int index, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
        {
            const float w = 190f, h = 40f, gap = 12f;
            var rt = UiUtil.Rect("button" + index, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2((index - 1) * (w + gap), 14f);
            var img = UiUtil.Img(rt, UiUtil.Rounded, new Color(0.30f, 0.24f, 0.14f, 1f), true);
            var b = UiUtil.Button(rt, img, onClick);
            var l = UiUtil.Rect("label", rt);
            UiUtil.Stretch(l);
            UiUtil.Text(l, text, 17f, font, TextAlignmentOptions.Center);
            return b;
        }

        // ---------------------------------------------------------------- content

        private static string Localize(string s)
        {
            return Localization.instance != null ? Localization.instance.Localize(s) : s;
        }

        private static string DisplayName(string prefab, int variant)
        {
            var db = ObjectDB.instance;
            var go = db != null ? db.GetItemPrefab(prefab) : null;
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null) return prefab;
            var name = Localize(drop.m_itemData.m_shared.m_name);
            int icons = drop.m_itemData.m_shared.m_icons?.Length ?? 0;
            return icons > 1 ? name + " (style " + (variant + 1) + ")" : name;
        }

        private static void Rebuild()
        {
            if (_root == null || _player == null) return;

            for (int i = 0; i < TransmogSlots.Count; i++)
            {
                bool active = TransmogSlots.IsActive((TransmogSlot)i);
                Tabs[i].gameObject.SetActive(active);
                TabImages[i].color = (int)_slot == i ? new Color(0.55f, 0.42f, 0.18f, 1f) : UiUtil.Dark;
            }

            for (int i = _content.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _hover.text = "";
            _hideButton.gameObject.SetActive(TransmogPlugin.AllowHide.Value);

            if (!TransmogSlots.IsActive(_slot))
            {
                _title.text = "Transmog";
                _info.text = "";
                _empty.text = "No slots are enabled in the config.";
                return;
            }

            _title.text = "Transmog - " + TransmogSlots.Labels[(int)_slot];
            var equipped = TransmogSlots.GetEquipped(_player, _slot);
            string look = "default";
            if (TransmogStore.TryGet(_player, _slot, out var cur))
                look = cur.Hidden ? "hidden" : DisplayName(cur.Prefab, cur.Variant);
            _info.text = equipped != null
                ? "Equipped: <color=#ffffff>" + Localize(equipped.m_shared.m_name) + "</color>    Look: <color=#eeb848>" + look + "</color>"
                : "Nothing equipped in this slot. Look: <color=#eeb848>" + look + "</color> (applies when you equip one)";

            var list = BuildCandidates(_player, _slot);
            _empty.text = list.Count == 0 ? "No items available for this slot yet." : "";

            foreach (var cand in list)
            {
                bool selected = cur.Prefab == cand.Prefab && (cur.Variant == cand.Variant || _slot != TransmogSlot.Shoulder);
                MakeCell(cand, selected);
            }
        }

        private static void MakeCell(Candidate cand, bool selected)
        {
            var cell = UiUtil.Rect("cell", _content);
            var bg = UiUtil.Img(cell, UiUtil.Rounded, selected ? UiUtil.Gold : new Color(0.22f, 0.20f, 0.18f, 1f), true);
            var c = cand;
            UiUtil.Button(cell, bg, () => ApplyEntry(new TransmogEntry(c.Prefab, c.Variant)));

            var iconRt = UiUtil.Rect("icon", cell);
            UiUtil.Place(iconRt, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            var icon = UiUtil.Img(iconRt, cand.Icon, Color.white, false);
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;

            if (cand.Owned)
            {
                var dot = UiUtil.Rect("owned", cell);
                dot.anchorMin = dot.anchorMax = new Vector2(1, 0);
                dot.pivot = new Vector2(1, 0);
                dot.sizeDelta = new Vector2(10, 10);
                dot.anchoredPosition = new Vector2(-4, 4);
                UiUtil.Img(dot, UiUtil.Circle, new Color(0.45f, 0.85f, 0.4f, 1f), false);
            }

            var hover = cell.gameObject.AddComponent<HoverHandler>();
            string label = cand.Name + (cand.Owned ? "  (in inventory)" : "");
            hover.OnEnter = () => { if (_hover != null) _hover.text = label; };
            hover.OnExit = () => { if (_hover != null && _hover.text == label) _hover.text = ""; };
        }

        private static void ApplyEntry(TransmogEntry? entry)
        {
            if (_player == null) return;
            var equipped = TransmogSlots.GetEquipped(_player, _slot);
            // Picking the item you already wear is the same as clearing.
            if (entry.HasValue && !entry.Value.Hidden && equipped != null && TransmogSlots.PrefabName(equipped) == entry.Value.Prefab
                && (_slot != TransmogSlot.Shoulder || equipped.m_variant == entry.Value.Variant))
                entry = null;

            if (!TransmogStore.Set(_player, _slot, entry))
            {
                TransmogPlugin.Log.LogWarning("Could not store transmog for " + _slot);
                return;
            }
            TransmogVisuals.Refresh(_player);
            Rebuild();
        }

        private static bool HasModel(GameObject prefab)
        {
            var t = prefab.transform;
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).name.StartsWith("attach", StringComparison.Ordinal)) return true;
            return false;
        }

        private static List<Candidate> BuildCandidates(Player player, TransmogSlot slot)
        {
            var result = new List<Candidate>();
            var seen = new HashSet<string>();
            var type = TransmogSlots.ToItemType(slot);
            var db = ObjectDB.instance;

            void Add(GameObject prefab, ItemDrop.ItemData data, int variant, bool owned)
            {
                if (prefab == null || data?.m_shared == null || !HasModel(prefab)) return;
                var icons = data.m_shared.m_icons;
                if (icons == null || icons.Length == 0) return;
                if (variant < 0 || variant >= icons.Length) variant = 0;
                string key = prefab.name + ":" + (slot == TransmogSlot.Shoulder ? variant : 0);
                if (!seen.Add(key)) return;
                result.Add(new Candidate
                {
                    Prefab = prefab.name,
                    Variant = slot == TransmogSlot.Shoulder ? variant : 0,
                    Name = Localize(data.m_shared.m_name) + (icons.Length > 1 && slot == TransmogSlot.Shoulder ? " (style " + (variant + 1) + ")" : ""),
                    Icon = icons[variant],
                    Owned = owned
                });
            }

            var inv = player.GetInventory();
            if (inv != null)
            {
                foreach (var item in inv.GetAllItems())
                {
                    if (item?.m_shared == null || item.m_shared.m_itemType != type || item.m_dropPrefab == null) continue;
                    GameObject prefab = db != null ? db.GetItemPrefab(item.m_dropPrefab.name) : null;
                    Add(prefab != null ? prefab : item.m_dropPrefab, item, item.m_variant, true);
                }
            }

            bool all = TransmogPlugin.AllowAllItems.Value;
            bool known = TransmogPlugin.AllowKnownItems.Value;
            if (db != null && (all || known))
            {
                foreach (var go in db.m_items)
                {
                    if (go == null) continue;
                    var drop = go.GetComponent<ItemDrop>();
                    var data = drop != null ? drop.m_itemData : null;
                    if (data?.m_shared == null || data.m_shared.m_itemType != type) continue;
                    if (!all && !player.IsMaterialKnown(data.m_shared.m_name) && !player.IsRecipeKnown(data.m_shared.m_name)) continue;
                    int variants = slot == TransmogSlot.Shoulder ? Math.Max(1, data.m_shared.m_icons?.Length ?? 1) : 1;
                    for (int v = 0; v < variants; v++) Add(go, data, v, false);
                }
            }

            result.Sort((a, b) =>
            {
                if (a.Owned != b.Owned) return a.Owned ? -1 : 1;
                int c = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                return c != 0 ? c : a.Variant.CompareTo(b.Variant);
            });
            return result;
        }
    }
}
