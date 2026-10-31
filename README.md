# Transmog (BepInEx plugin for Valheim)

Equipped armor keeps its stats but renders with the model of another armor item of the same slot.

GUID: `vpf.Transmog` · Assembly: `VpfTransmog.dll` · License: MIT

## Use

* Open the inventory. Each equipped helmet / chest / legs / shoulder (cape) piece gets a small round **T**
  button in a corner of its cell (gold = a look is active). This works in the vanilla grid and in
  inventory mods that reuse the player grid (e.g. InventorySlots' equipment panel).
* Click it (or press **Ctrl+T** while the inventory is open) to open the picker:
  * slot tabs at the top, a grid of looks (green dot = in your inventory, others are items you know),
  * click a look to apply it, **Clear transmog** to go back to the real model, **Hide slot** to render nothing,
  * click outside / **Close** / close the inventory to dismiss.
* Choices are stored per character in `Player.m_customData["vpf.Transmog"]` and survive relogs.
  They belong to the slot, not the item, so swapping armor keeps the look.

## How it works

* While `Humanoid.SetupVisEquipment` runs for a `Player`, the hashes passed to
  `VisEquipment.Set{Helmet,Chest,Leg,Shoulder,Utility}Item` are replaced with the chosen prefab's hash.
* VisEquipment writes those hashes into the player's ZDO, so other clients see the look
  **without this mod**, as long as they have the item prefab (same mod set).
* Invalid choices (prefab missing or wrong type) fall back to the real item.

## Config (`BepInEx/config/vpf.Transmog.cfg`)

| Section | Key | Default | |
|---|---|---|---|
| 1 - General | Enabled | true | master switch |
| 2 - Slots | Helmet / Chest / Legs / Shoulder / Utility | true/true/true/true/false | per-slot enable |
| 3 - Picker | AllowKnownItems | true | offer known (discovered) items, not just inventory |
| 3 - Picker | AllowAllItems | false | offer every item of the slot type |
| 3 - Picker | AllowHide | true | allow rendering a slot as empty |
| 3 - Picker | ShowGoldenVariants | false | also list the game's golden "Fallen Warrior" (FW_) copies of armor and capes |
| 3 - Picker | ShowEtherealVariants | false | also list the game's ghostly spirit (SP_) copies of armor and capes |
| 3 - Picker | ShowOtherVariants | false | also list any other same-name, same-model armor copies |
| 4 - Interface | ShowSlotIcons | true | the T buttons |
| 4 - Interface | IconCorner | TopLeft | corner of the cell |
| 4 - Interface | IconSize | 20 | pixels |
| 4 - Interface | OpenPickerHotkey | T + LeftControl | open the picker while inventory is open |

## Build

```
dotnet build Transmog/Transmog.csproj -c Release -p:ValheimManaged=<Valheim>/valheim_Data/Managed -p:BepInExCore=<BepInEx>/core
```
(or set `VALHEIM_MANAGED` / `BEPINEX_CORE`). No game DLLs are included in this repository.
