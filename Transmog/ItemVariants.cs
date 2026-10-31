using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Transmog
{
    internal enum VariantKind { Golden, Ethereal, Other }

    /// <summary>
    /// Detects armor prefabs that are only re-skinned copies of another item.
    ///
    /// The base game ships extra armor prefabs for NPC/enemy outfits: "Fallen Warrior"
    /// gold copies (FW_ materials) and ghostly spirit copies (SP_ materials). They reuse
    /// the real item's name token, icons and meshes and only swap the material, and
    /// they have no crafting recipe. Because they share the item name they also count as
    /// "known", so without this filter such items show up several times in the picker.
    ///
    /// Grouping rule (no hard-coded item names): group armor items by (item type + item
    /// name token + set of meshes the prefab renders). In a group with more than one
    /// prefab, the canonical entry is the one with an enabled recipe, then the shortest
    /// prefab name, then ordinal order. Every other member is a variant copy.
    ///
    /// Each copy is classified from its renderer material names (FW_ = golden,
    /// SP_ or "spiritcaller" = ethereal), falling back to the prefab name prefix.
    /// Anything else is "Other". Styles of one prefab (m_icons variants) are untouched.
    /// </summary>
    internal static class ItemVariants
    {
        private static ObjectDB _db;
        private static int _itemCount = -1;
        private static int _recipeCount = -1;
        private static readonly Dictionary<string, VariantKind> _copies = new Dictionary<string, VariantKind>(StringComparer.Ordinal);

        /// <summary>True when the prefab is a variant copy whose kind is not enabled in the config.</summary>
        internal static bool IsHidden(GameObject prefab)
        {
            if (prefab == null) return false;
            EnsureBuilt();
            if (!_copies.TryGetValue(prefab.name, out var kind)) return false;
            switch (kind)
            {
                case VariantKind.Golden: return !TransmogPlugin.ShowGoldenVariants.Value;
                case VariantKind.Ethereal: return !TransmogPlugin.ShowEtherealVariants.Value;
                default: return !TransmogPlugin.ShowOtherVariants.Value;
            }
        }

        private static bool IsArmorType(ItemDrop.ItemData.ItemType t) => TransmogSlots.TryFromItemType(t, out _);

        private static void EnsureBuilt()
        {
            var db = ObjectDB.instance;
            if (db == null) return;
            int items = db.m_items?.Count ?? 0;
            int recipes = db.m_recipes?.Count ?? 0;
            if (db == _db && items == _itemCount && recipes == _recipeCount) return;
            _db = db; _itemCount = items; _recipeCount = recipes;
            _copies.Clear();

            var crafted = new HashSet<string>(StringComparer.Ordinal);
            if (db.m_recipes != null)
                foreach (var r in db.m_recipes)
                    if (r != null && r.m_enabled && r.m_item != null) crafted.Add(r.m_item.gameObject.name);

            var groups = new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
            if (db.m_items == null) return;
            foreach (var go in db.m_items)
            {
                if (go == null) continue;
                var drop = go.GetComponent<ItemDrop>();
                var shared = drop != null ? drop.m_itemData?.m_shared : null;
                if (shared == null || !IsArmorType(shared.m_itemType)) continue;
                if (!Picker.HasModel(go)) continue;
                string meshes = MeshSignature(go);
                if (meshes.Length == 0) continue;
                string key = (int)shared.m_itemType + "|" + shared.m_name + "|" + meshes;
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<GameObject>();
                if (!list.Any(g => g.name == go.name)) list.Add(go);
            }

            foreach (var list in groups.Values)
            {
                if (list.Count < 2) continue;
                var keep = list
                    .OrderByDescending(g => crafted.Contains(g.name))
                    .ThenBy(g => g.name.Length)
                    .ThenBy(g => g.name, StringComparer.Ordinal)
                    .First();
                foreach (var g in list)
                    if (g != keep) _copies[g.name] = Classify(g);
            }

            if (_copies.Count > 0)
            {
                foreach (VariantKind k in Enum.GetValues(typeof(VariantKind)))
                {
                    var names = _copies.Where(p => p.Value == k).Select(p => p.Key).OrderBy(n => n, StringComparer.Ordinal).ToList();
                    if (names.Count > 0)
                        TransmogPlugin.Log.LogInfo(k + " variant copies (" + names.Count + "): " + string.Join(", ", names));
                }
            }
        }

        private static VariantKind Classify(GameObject go)
        {
            bool golden = false, ethereal = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string n = m.name;
                    if (n.StartsWith("SP_", StringComparison.OrdinalIgnoreCase) || n.IndexOf("spiritcaller", StringComparison.OrdinalIgnoreCase) >= 0) ethereal = true;
                    else if (n.StartsWith("FW_", StringComparison.OrdinalIgnoreCase)) golden = true;
                }
            }
            if (ethereal != golden) return ethereal ? VariantKind.Ethereal : VariantKind.Golden;
            if (go.name.StartsWith("SP_", StringComparison.OrdinalIgnoreCase)) return VariantKind.Ethereal;
            if (go.name.StartsWith("FW_", StringComparison.OrdinalIgnoreCase)) return VariantKind.Golden;
            return VariantKind.Other;
        }

        private static string MeshSignature(GameObject go)
        {
            var ids = new List<int>();
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) ids.Add(smr.sharedMesh.GetInstanceID());
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) ids.Add(mf.sharedMesh.GetInstanceID());
            ids.Sort();
            return string.Join(",", ids.Distinct());
        }
    }
}