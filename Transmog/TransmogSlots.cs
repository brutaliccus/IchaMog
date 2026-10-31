using HarmonyLib;

namespace Transmog
{
    internal enum TransmogSlot { Helmet = 0, Chest = 1, Legs = 2, Shoulder = 3, Utility = 4 }

    internal static class TransmogSlots
    {
        public const int Count = 5;

        // Keys used in the saved string; never change them.
        public static readonly string[] Keys = { "helmet", "chest", "legs", "shoulder", "utility" };
        public static readonly string[] Labels = { "Helmet", "Chest", "Legs", "Shoulder", "Utility" };

        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> HelmetRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_helmetItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> ChestRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_chestItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> LegRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_legItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> ShoulderRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_shoulderItem");
        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> UtilityRef =
            AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_utilityItem");

        public static bool TryFromItemType(ItemDrop.ItemData.ItemType type, out TransmogSlot slot)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.Helmet: slot = TransmogSlot.Helmet; return true;
                case ItemDrop.ItemData.ItemType.Chest: slot = TransmogSlot.Chest; return true;
                case ItemDrop.ItemData.ItemType.Legs: slot = TransmogSlot.Legs; return true;
                case ItemDrop.ItemData.ItemType.Shoulder: slot = TransmogSlot.Shoulder; return true;
                case ItemDrop.ItemData.ItemType.Utility: slot = TransmogSlot.Utility; return true;
                default: slot = TransmogSlot.Helmet; return false;
            }
        }

        public static ItemDrop.ItemData.ItemType ToItemType(TransmogSlot slot)
        {
            switch (slot)
            {
                case TransmogSlot.Helmet: return ItemDrop.ItemData.ItemType.Helmet;
                case TransmogSlot.Chest: return ItemDrop.ItemData.ItemType.Chest;
                case TransmogSlot.Legs: return ItemDrop.ItemData.ItemType.Legs;
                case TransmogSlot.Shoulder: return ItemDrop.ItemData.ItemType.Shoulder;
                default: return ItemDrop.ItemData.ItemType.Utility;
            }
        }

        public static ItemDrop.ItemData GetEquipped(Humanoid h, TransmogSlot slot)
        {
            if (h == null) return null;
            switch (slot)
            {
                case TransmogSlot.Helmet: return HelmetRef(h);
                case TransmogSlot.Chest: return ChestRef(h);
                case TransmogSlot.Legs: return LegRef(h);
                case TransmogSlot.Shoulder: return ShoulderRef(h);
                default: return UtilityRef(h);
            }
        }

        public static bool IsActive(TransmogSlot slot)
        {
            return TransmogPlugin.Enabled != null && TransmogPlugin.Enabled.Value
                && TransmogPlugin.SlotEnabled[(int)slot] != null && TransmogPlugin.SlotEnabled[(int)slot].Value;
        }

        public static string PrefabName(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
        }

        public static int Hash(ItemDrop.ItemData item)
        {
            var name = PrefabName(item);
            return name != null ? name.GetStableHashCode() : 0;
        }
    }
}
