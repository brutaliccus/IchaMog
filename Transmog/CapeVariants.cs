using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Transmog
{
    /// <summary>
    /// Detects cape prefabs that are only re-skinned copies of another cape.
    ///
    /// The base game ships extra cape prefabs used for NPC/enemy outfits (for example
    /// "FW_" gold and "SP_" spectral copies). They reuse the real cape's item name token,
    /// icons and meshes and only swap the material, and they have no crafting recipe.
    /// Because they share the item name they also count as "known", so without this
    /// filter every such cape shows up several times in the picker.
    ///
    /// Rule (no hard-coded names): group shoulder items by (item name token + set of
    /// meshes the prefab renders). Inside a group with more than one prefab, the
    /// canonical entry is the one with an enabled recipe, then the shortest prefab name,
    /// then ordinal order. Every other member of the group is a variant copy.
    /// </summary>
    internal static class CapeVariants
    {
        private static ObjectDB _db;
        private static int _itemCount = -1;
        private static int _recipeCount = -1;
        private static readonly HashSet<string> _copies = new HashSet<string>(StringComparer.Ordinal);

        internal static bool IsVariantCopy(GameObject prefab)
        {
            if (prefab == null) return false;
            EnsureBuilt();
            return _copies.Contains(prefab.name);
        }

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
                if (shared == null || shared.m_itemType != ItemDrop.ItemData.ItemType.Shoulder) continue;
                if (!Picker.HasModel(go)) continue;
                string meshes = MeshSignature(go);
                if (meshes.Length == 0) continue;
                string key = shared.m_name + "|" + meshes;
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
                    if (g != keep) _copies.Add(g.name);
            }

            if (_copies.Count > 0)
                TransmogPlugin.Log.LogInfo("Cape variant copies hidden from the picker: " + string.Join(", ", _copies.OrderBy(n => n, StringComparer.Ordinal)));
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