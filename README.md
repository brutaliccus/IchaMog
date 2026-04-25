# IchaMog (WotLK 3.3.5)

IchaMog is a tooltip addon for WotLK 3.3.5 private servers (including Hellscream) that shows whether an item's transmog appearance is already collected.

It adds a simple status line to item tooltips:
- `Collected`
- `Not collected`

## Features

- Tracks collection from equipped items automatically.
- Supports appearance-level matching (not just exact item ID).
- Uses Hellscream/Tmog cache data when available for server-backed collection state.
- Supports cross-item shared model detection through MogIt data (`display`).
- Works on normal item tooltips, linked items, and common third-party tooltips (including AtlasLoot scenarios).
- Coexists with Enchantrix/LibExtraTip tooltip chains.

## Requirements

### Required
- World of Warcraft WotLK 3.3.5 client.
- `IchaMog` addon files in your `Interface\AddOns\IchaMog` folder.

### Optional (recommended)
- **MogIt**: Enables stronger cross-item model sharing detection (`MogIt:GetData("item", id, "display")`).
  - IchaMog auto-detects MogIt and auto-loads MogIt data modules when available.
- **Tmog (Hellscream transmog addon)**: If present, IchaMog can use server-fed collection cache data.
- **Enchantrix/Auctioneer**: Supported; IchaMog integrates with LibExtraTip tooltip flow.

## Installation

1. Copy the `IchaMog` folder into `World of Warcraft\Interface\AddOns\`.
2. Restart WoW or run `/reload`.
3. (Optional) Enable MogIt and/or Tmog for best collection accuracy.

## Usage

No setup is required for basic use. Hover an item and read the status line.

### Slash commands

- `/ichamog` or `/ichamog help` - show help.
- `/ichamog loadmogit` - manually load MogIt data modules.
- `/ichamog resetlooks` - clear shared appearance keys (keeps exact equipped item IDs).
- `/ichamog wipe` - wipe IchaMog collection data for this character.

## Notes

- "Collected" means the appearance is known, including via shared-model matches when data is available.
- If MogIt is not loaded, shared-model detection is reduced.
- If server APIs are stubbed on your realm, IchaMog falls back to available sources (Tmog cache, MogIt, local tracking).

## Saved Variables

- `IchaMogDB` (per character)

If you previously used the old addon name (`HellscreamTransmogTrack`), migrate your SavedVariables key manually to keep prior history.
