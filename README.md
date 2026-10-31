# IchaMog - Valheim transmog

A BepInEx plugin for Valheim: equipped armor keeps its stats but renders with the look of another
armor item of the same slot.

Plugin GUID: `vpf.Transmog` - assembly: `VpfTransmog.dll` - license: MIT

## Requirements

* Valheim with [BepInEx 5](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
* Optional: InventorySlots (`sighsorry.InventorySlots`). When installed, the transmog buttons also
  appear on its equipment panel. Without it, the vanilla inventory grid is used.

## Install

Copy `VpfTransmog.dll` into `BepInEx/plugins/`.

## Use

* Open the inventory. Each equipped helmet / chest / legs / cape piece gets a small round **T** button
  in a corner of its cell (gold = a look is active).
* Click it, or press **Ctrl+T** while the inventory is open, to open the picker:
  * slot tabs at the top and a grid of looks (green dot = in your inventory, others are items you know),
  * click a look to apply it, **Clear transmog** to go back to the real model, **Hide slot** to render nothing.
* Choices are stored per character and survive relogs. They belong to the slot, not the item, so
  swapping armor keeps the look.
* Other players see your look without this mod, as long as they have the same item prefabs.
* The game's golden ("Fallen Warrior") and ghostly NPC copies of armor are hidden from the picker by
  default, so each item is listed once. They can be turned back on in the config.

## Config (`BepInEx/config/vpf.Transmog.cfg`)

| Section | Key | Default | Meaning |
|---|---|---|---|
| 1 - General | Enabled | true | master switch |
| 2 - Slots | Helmet / Chest / Legs / Shoulder / Utility | true / true / true / true / false | per-slot enable |
| 3 - Picker | AllowKnownItems | true | offer known (discovered) items, not just inventory |
| 3 - Picker | AllowAllItems | false | offer every item of the slot type |
| 3 - Picker | AllowHide | true | allow rendering a slot as empty |
| 3 - Picker | ShowGoldenVariants | false | also list the golden "Fallen Warrior" copies of armor and capes |
| 3 - Picker | ShowEtherealVariants | false | also list the ghostly spirit copies of armor and capes |
| 3 - Picker | ShowOtherVariants | false | also list any other same-name, same-model armor copies |
| 4 - Interface | ShowSlotIcons | true | show the T buttons |
| 4 - Interface | IconCorner | TopLeft | corner of the cell |
| 4 - Interface | IconSize | 20 | button size in pixels |
| 4 - Interface | OpenPickerHotkey | T + LeftControl | open the picker while the inventory is open |

## Build

```
dotnet build Transmog/Transmog.csproj -c Release -p:ValheimManaged=<Valheim>/valheim_Data/Managed -p:BepInExCore=<BepInEx>/core
```

Or set `VALHEIM_MANAGED` / `BEPINEX_CORE`. No game or BepInEx DLLs are included in this repository.

## License

MIT - see [LICENSE](LICENSE).