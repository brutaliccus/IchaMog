--[[
  IchaMog — WotLK 3.3.5

  Collected means:
    • You have equipped that exact item id at least once (since install), OR
    • You have unlocked a real appearance key for the same model (never uses the
      inventory icon).

  Shared model keys:
    1) GetItemAppearanceId / GetItemDisplayId when trusted (IchaMog_UseNativeDisplayAPI)
    2) MogIt:GetData("item", id, "display") when MogIt + data modules are loaded
       (IchaMog_LoadMogItData = true before login, or /ichamog loadmogit)
    3) IchaMog_AppearanceDB[itemId] = groupId (optional static data)

  /ichamog loadmogit — load MogIt’s optional item modules
  /ichamog resetlooks — clear appearance keys; exact equipped item ids kept
  /ichamog wipe — clear collection for this character

  Renamed from HellscreamTransmogTrack: to keep your collection, in WTF copy the table
  HellscreamTransmogTrackDB → IchaMogDB inside SavedVariables.lua for this character.

  With Auctioneer / Enchantrix (LibExtraTip): IchaMog writes directly to tooltip lines
  for stable sizing across vendor, compare, atlas and hyperlink tooltips.

  Tmog-style SetText rebuild stays off by default (breaks LibExtraTip). IchaMog_UseTmogStyleTooltip
  only if you do not use LibExtraTip-style tooltips.
]]

local ADDON_NAME = ...

local SCHEMA_VERSION = 3

local autoSkipNativeDisplay = false

--- Tmog-style SetText rebuild breaks LibExtraTip (Enchantrix / Auctioneer). Prefer false.
local USE_TMOG_STYLE_TOOLTIP = false
local MAX_REBUILD_LINES = 27

local db
local everCollectedByItem = {}

local deferDecorateFrame = CreateFrame("Frame")
deferDecorateFrame:Hide()
local pendingDecorateTip

local function libExtraTipOwnsTooltip(tip)
  local ls = _G.LibStub
  if not ls then
    return false
  end
  local le = ls("LibExtraTip-1", true)
  if not le or type(le.tooltipRegistry) ~= "table" then
    return false
  end
  return le.tooltipRegistry[tip] ~= nil
end

local function ensureDB()
  if type(IchaMogDB) ~= "table" then
    IchaMogDB = {}
  end
  db = IchaMogDB
  db.items = db.items or {}
  db.looks = db.looks or {}
  return db
end

local function migrateDB()
  local ver = db.schemaVersion or 1
  if ver < 2 then
    db.looks = {}
  end
  if ver < 3 and db.looks then
    for k in pairs(db.looks) do
      if type(k) == "string" and string.sub(k, 1, 2) == "h:" then
        db.looks[k] = nil
      end
    end
  end
  db.schemaVersion = SCHEMA_VERSION
end

local function runNativeDisplaySelfTest()
  autoSkipNativeDisplay = false
  if _G.IchaMog_UseNativeDisplayAPI ~= nil then
    return
  end
  if type(GetItemDisplayId) ~= "function" then
    return
  end
  local vals = {}
  local probeIds = { 6948, 2589, 46069, 12064, 25, 159 }
  for _, id in ipairs(probeIds) do
    local v = GetItemDisplayId(id)
    if type(v) == "number" and v ~= 0 then
      table.insert(vals, v)
    end
  end
  if #vals < 2 then
    return
  end
  local first = vals[1]
  for i = 2, #vals do
    if vals[i] ~= first then
      return
    end
  end
  autoSkipNativeDisplay = true
  DEFAULT_CHAT_FRAME:AddMessage(
    "|cff00cc00IchaMog|r: GetItemDisplayId looks like a stub (same value for unrelated items). "
      .. "Ignoring it. To force using it anyway: /script IchaMog_UseNativeDisplayAPI=true"
  )
end

local function itemIdFromLink(link)
  if not link then
    return nil
  end
  return tonumber(link:match("item:(%d+)"))
end

local function getEquipLoc(link, itemId)
  local _, _, _, _, _, _, _, _, equipLoc = GetItemInfo(link or itemId)
  if equipLoc and equipLoc ~= "" then
    return equipLoc
  end
  if type(GetItemInfoInstant) == "function" then
    local _, _, _, instantLoc = GetItemInfoInstant(link or itemId)
    if instantLoc and instantLoc ~= "" then
      return instantLoc
    end
  end
  return nil
end

local function useNativeDisplayAPI()
  if _G.IchaMog_UseNativeDisplayAPI == true then
    return true
  end
  if _G.IchaMog_UseNativeDisplayAPI == false then
    return false
  end
  return not autoSkipNativeDisplay
end

local MOGIT_DATA_ADDONS = {
  "MogIt_Cloth",
  "MogIt_Leather",
  "MogIt_Mail",
  "MogIt_Plate",
  "MogIt_OneHanded",
  "MogIt_TwoHanded",
  "MogIt_Ranged",
  "MogIt_Accessories",
  "MogIt_Other",
}
local mogitAutoTried = false

local function loadAddonByFolderName(name)
  if IsAddOnLoaded(name) then
    return true
  end
  for i = 1, GetNumAddOns() do
    local n = GetAddOnInfo(i)
    if n == name then
      LoadAddOn(i)
      return IsAddOnLoaded(name)
    end
  end
  return false
end

local function tryLoadMogItDataModules(chatty)
  local loaded = 0
  for _, mod in ipairs(MOGIT_DATA_ADDONS) do
    if loadAddonByFolderName(mod) then
      loaded = loaded + 1
    end
  end
  if chatty then
    DEFAULT_CHAT_FRAME:AddMessage(
      string.format(
        "|cff00cc00IchaMog|r: MogIt data modules now loaded: %d/%d (need MogIt core enabled).",
        loaded,
        #MOGIT_DATA_ADDONS
      )
    )
  end
  return loaded
end

local function tryAutoEnableMogItData(chatty)
  if mogitAutoTried and not chatty then
    return
  end
  if not IsAddOnLoaded("MogIt") then
    return
  end
  mogitAutoTried = true
  tryLoadMogItDataModules(chatty)
end

local function mogitAppearanceKey(itemId)
  local mog = _G.MogIt
  if not mog or type(mog.GetData) ~= "function" then
    return nil
  end
  local display = mog:GetData("item", itemId, "display")
  if display and display ~= 0 then
    return "m:" .. tostring(display)
  end
  return nil
end

local function nativeAppearanceKey(itemId, link)
  if not useNativeDisplayAPI() then
    return nil
  end
  if type(GetItemAppearanceId) == "function" then
    local k = GetItemAppearanceId(link) or GetItemAppearanceId(itemId)
    if k and k ~= 0 then
      return "a:" .. tostring(k)
    end
  end
  if type(GetItemDisplayId) == "function" then
    local k = GetItemDisplayId(link) or GetItemDisplayId(itemId)
    if k and k ~= 0 then
      return "d:" .. tostring(k)
    end
  end
  return nil
end

local function bundledAppearanceKey(itemId)
  local t = _G.IchaMog_AppearanceDB
  if type(t) ~= "table" or not itemId then
    return nil
  end
  local g = t[itemId]
  if g and g ~= 0 and g ~= "" then
    return "b:" .. tostring(g)
  end
  return nil
end

local function lookKeysForItem(itemId, link)
  local keys = {}
  local native = nativeAppearanceKey(itemId, link)
  if native then
    table.insert(keys, native)
  end
  local mogKey = mogitAppearanceKey(itemId)
  if mogKey then
    table.insert(keys, mogKey)
  end
  local bundled = bundledAppearanceKey(itemId)
  if bundled then
    table.insert(keys, bundled)
  end
  return keys
end

local function slotSetForItem(link, itemId)
  local equipLoc = getEquipLoc(link, itemId)
  if not equipLoc then
    return nil
  end
  local slotsByEquipLoc = {
    INVTYPE_HEAD = { 1 },
    INVTYPE_NECK = { 2 },
    INVTYPE_SHOULDER = { 3 },
    INVTYPE_BODY = { 4 },
    INVTYPE_CHEST = { 5 },
    INVTYPE_ROBE = { 5 },
    INVTYPE_WAIST = { 6 },
    INVTYPE_LEGS = { 7 },
    INVTYPE_FEET = { 8 },
    INVTYPE_WRIST = { 9 },
    INVTYPE_HAND = { 10 },
    INVTYPE_FINGER = { 11, 12 },
    INVTYPE_TRINKET = { 13, 14 },
    INVTYPE_CLOAK = { 15 },
    INVTYPE_WEAPON = { 16, 17 },
    INVTYPE_2HWEAPON = { 16 },
    INVTYPE_WEAPONMAINHAND = { 16 },
    INVTYPE_WEAPONOFFHAND = { 17 },
    INVTYPE_SHIELD = { 17 },
    INVTYPE_HOLDABLE = { 17 },
    INVTYPE_RANGED = { 16 },
    INVTYPE_RANGEDRIGHT = { 16 },
    INVTYPE_THROWN = { 16 },
    INVTYPE_RELIC = { 18 },
    INVTYPE_TABARD = { 19 },
  }
  return slotsByEquipLoc[equipLoc]
end

local function isCollectedByTMOGAppearance(itemId, link)
  if type(_G.TMOG_CACHE) ~= "table" then
    return false
  end
  local slots = slotSetForItem(link, itemId)
  if not slots then
    return false
  end

  local wanted = {}
  for _, k in ipairs(lookKeysForItem(itemId, link)) do
    wanted[k] = true
  end
  if not next(wanted) then
    return false
  end

  for _, slot in ipairs(slots) do
    local bySlot = _G.TMOG_CACHE[slot]
    if type(bySlot) == "table" then
      if bySlot[itemId] then
        return true
      end
      for collectedItemId, hasIt in pairs(bySlot) do
        if hasIt and tonumber(collectedItemId) then
          for _, gotKey in ipairs(lookKeysForItem(tonumber(collectedItemId), nil)) do
            if wanted[gotKey] then
              return true
            end
          end
        end
      end
    end
  end
  return false
end

local function markCollected(itemId, link)
  if not itemId then
    return
  end
  db.items[itemId] = true
  for _, key in ipairs(lookKeysForItem(itemId, link)) do
    db.looks[key] = true
  end
end

local function isCollected(itemId, link)
  if not itemId then
    return false
  end
  local liveCollected = false
  -- Prefer server-backed Tmog cache when available (authoritative collected list).
  if isCollectedByTMOGAppearance(itemId, link) then
    liveCollected = true
  end
  if not liveCollected and db.items[itemId] then
    liveCollected = true
  end
  if not liveCollected then
    for _, key in ipairs(lookKeysForItem(itemId, link)) do
      if db.looks[key] then
        liveCollected = true
        break
      end
    end
  end
  if liveCollected then
    everCollectedByItem[itemId] = true
    return true
  end
  if everCollectedByItem[itemId] then
    return true
  end
  return false
end

local function frameHasCollectionStatusLine(frame)
  if not frame or type(frame.GetName) ~= "function" or type(frame.NumLines) ~= "function" then
    return false
  end
  local nm = frame:GetName()
  if not nm then
    return false
  end
  for i = 1, frame:NumLines() do
    local L = _G[nm .. "TextLeft" .. i]
    if L and L:IsShown() then
      local t = L:GetText()
      if t == "Collected" or t == "Not collected" then
        return true
      end
    end
  end
  return false
end

local function tooltipAlreadyShowsCollectionStatus(tip, le)
  if frameHasCollectionStatusLine(tip) then
    return true
  end
  local reg = le and type(le.tooltipRegistry) == "table" and le.tooltipRegistry[tip]
  if reg and reg.extraTip then
    return frameHasCollectionStatusLine(reg.extraTip)
  end
  return false
end

--- Appends Collected / Not collected at the bottom of the tooltip.
local function appendCollectionToTooltip(tip, link)
  if not tip or tip.ichamogDecorated then
    return
  end
  if tip.ichamogLastDecoratedLink and tip.ichamogLastDecoratedLink == link then
    tip.ichamogDecorated = true
    return
  end
  if not link or link == "" then
    return
  end
  local id = itemIdFromLink(link)
  if not id then
    return
  end
  local equipLoc = getEquipLoc(link, id)
  if not equipLoc or equipLoc == "" then
    return
  end

  if tooltipAlreadyShowsCollectionStatus(tip, nil) then
    tip.ichamogDecorated = true
    return
  end

  if tip.ichamogLockedLink ~= link then
    tip.ichamogLockedLink = link
    tip.ichamogLockedValue = isCollected(id, link)
  end
  local collected = tip.ichamogLockedValue
  local r, g, b, text
  if collected then
    text, r, g, b = "Collected", 0.25, 1.0, 0.35
  else
    text, r, g, b = "Not collected", 1.0, 0.4, 0.4
  end

  tip:AddLine(text, r, g, b)
  tip.ichamogDecorated = true
  tip.ichamogLastDecoratedLink = link
end

local EQUIP_SLOTS = {
  1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
}

local function scanPlayerEquipment()
  for _, slot in ipairs(EQUIP_SLOTS) do
    local link = GetInventoryItemLink("player", slot)
    if link then
      markCollected(itemIdFromLink(link), link)
    end
  end
end

local scanPending
local function scheduleScan()
  if scanPending then
    return
  end
  scanPending = true
  local q = CreateFrame("Frame")
  q:SetScript("OnUpdate", function(self)
    self:SetScript("OnUpdate", nil)
    scanPending = false
    scanPlayerEquipment()
  end)
end

local function addCollectionStatusTmogStyle(tip, collected)
  local n = tip:NumLines()
  if n < 2 or n > MAX_REBUILD_LINES then
    return false
  end
  local tname = tip:GetName()
  local lines = {}
  for i = 1, n do
    local L = _G[tname .. "TextLeft" .. i]
    local R = _G[tname .. "TextRight" .. i]
    if not L then
      return false
    end
    local lt = L:GetText()
    if i == 1 and (not lt or lt == "") then
      return false
    end
    local rt = R and R:IsShown() and R:GetText() or nil
    local lr, lg, lb = L:GetTextColor()
    local rr, rg, rb = 1, 1, 1
    if R then
      rr, rg, rb = R:GetTextColor()
    end
    lines[i] = { lt, rt, lr, lg, lb, rr, rg, rb }
  end

  local status = collected and "|cff00cc00Collected|r" or "|cffe04040Not collected|r"

  tip:SetText(lines[1][1], lines[1][3], lines[1][4], lines[1][5], 1, false)
  tip:AddLine(status)
  for i = 2, n do
    local row = lines[i]
    if row[2] then
      tip:AddDoubleLine(row[1], row[2], row[3], row[4], row[5], row[6], row[7], row[8])
    else
      local wrap = false
      local s = row[1] or ""
      if string.sub(s, 1, 1) == "\"" then
        wrap = true
      end
      tip:AddLine(row[1], row[3], row[4], row[5], wrap)
    end
  end
  return true
end

local function decorateTooltip(tip)
  if tip.ichamogDecorated then
    return
  end

  local _, link = tip:GetItem()
  if not link then
    return
  end

  local id = itemIdFromLink(link)
  if not id then
    return
  end

  local equipLoc = getEquipLoc(link, id)
  if not equipLoc or equipLoc == "" then
    return
  end

  local collected = isCollected(id, link)

  -- Default to append mode; rebuild mode can break custom tooltip layouts.
  local useRebuild = USE_TMOG_STYLE_TOOLTIP
  if _G.IchaMog_UseTmogStyleTooltip == false then
    useRebuild = false
  end
  if _G.IchaMog_UseTmogStyleTooltip == true then
    useRebuild = true
  end
  if useRebuild and libExtraTipOwnsTooltip(tip) then
    useRebuild = false
  end

  if useRebuild and addCollectionStatusTmogStyle(tip, collected) then
    tip.ichamogDecorated = true
    return
  end

  appendCollectionToTooltip(tip, link)
end

local function scheduleDecorateTooltip(tip)
  if not tip then
    return
  end
  pendingDecorateTip = tip
  deferDecorateFrame:Show()
end

local function hookTooltipHyperlinkUpdates(tip)
  if not tip then
    return
  end
  if tip.HasScript and tip:HasScript("OnTooltipSetHyperlink") then
    tip:HookScript("OnTooltipSetHyperlink", function(self)
      decorateTooltip(self)
      if not self.ichamogDecorated then
        scheduleDecorateTooltip(self)
      end
    end)
    return
  end
  if not tip.ichamogSetHyperlinkHooked and type(hooksecurefunc) == "function" then
    hooksecurefunc(tip, "SetHyperlink", function(self)
      decorateTooltip(self)
      if not self.ichamogDecorated then
        scheduleDecorateTooltip(self)
      end
    end)
    tip.ichamogSetHyperlinkHooked = true
  end
end

local function hookTooltipItemSetters(tip)
  if not tip or tip.ichamogItemSettersHooked then
    return
  end
  local setters = {
    "SetHyperlink",
    "SetBagItem",
    "SetInventoryItem",
    "SetLootItem",
    "SetLootRollItem",
    "SetMerchantItem",
    "SetAuctionItem",
    "SetInboxItem",
    "SetQuestItem",
    "SetQuestLogItem",
    "SetTradeSkillItem",
    "SetBuybackItem",
  }
  for _, method in ipairs(setters) do
    if type(tip[method]) == "function" then
      hooksecurefunc(tip, method, function(self)
        scheduleDecorateTooltip(self)
      end)
    end
  end
  tip.ichamogItemSettersHooked = true
end

deferDecorateFrame:SetScript("OnUpdate", function(self)
  self:Hide()
  local tip = pendingDecorateTip
  pendingDecorateTip = nil
  if tip and tip:IsShown() then
    decorateTooltip(tip)
  end
end)

local function hookTooltips()
  local tips = { GameTooltip, ItemRefTooltip }
  if ShoppingTooltip1 then
    table.insert(tips, ShoppingTooltip1)
  end
  if ShoppingTooltip2 then
    table.insert(tips, ShoppingTooltip2)
  end
  -- Common third-party tooltip frames (AtlasLoot, etc.) if present.
  local extraTipNames = {
    "AtlasLootTooltip",
    "AtlasLootTooltip2",
    "AtlasLootCompareTooltip1",
    "AtlasLootCompareTooltip2",
  }
  for _, n in ipairs(extraTipNames) do
    local t = _G[n]
    if t then
      table.insert(tips, t)
    end
  end

  for _, tip in ipairs(tips) do
    if tip and tip.HookScript then
      local isPrimaryTip = (tip == GameTooltip or tip == ItemRefTooltip)
      local tipName = tip.GetName and tip:GetName() or ""
      local isAtlasLike = tipName and string.find(tipName, "AtlasLoot", 1, true) ~= nil
      tip:HookScript("OnTooltipCleared", function(self)
        self.ichamogDecorated = nil
        self.ichamogLockedLink = nil
        self.ichamogLockedValue = nil
        self.ichamogLastDecoratedLink = nil
        if pendingDecorateTip == self then
          pendingDecorateTip = nil
          deferDecorateFrame:Hide()
        end
      end)
      if not isAtlasLike then
        hookTooltipItemSetters(tip)
      end
      if tip == ShoppingTooltip1 or tip == ShoppingTooltip2 then
        tip:HookScript("OnTooltipSetItem", function(self)
          decorateTooltip(self)
        end)
      else
        tip:HookScript("OnTooltipSetItem", function(self)
          decorateTooltip(self)
          if not self.ichamogDecorated then
            scheduleDecorateTooltip(self)
          end
        end)
      end
      hookTooltipHyperlinkUpdates(tip)
    end
  end
end

local function printHelp()
  DEFAULT_CHAT_FRAME:AddMessage("|cff00cc00IchaMog|r — loadmogit | resetlooks | wipe | help")
end

SLASH_ICHAMOG1 = "/ichamog"
SlashCmdList["ICHAMOG"] = function(msg)
  msg = string.lower(string.gsub(msg or "", "^%s*(.-)%s*$", "%1"))
  ensureDB()
  if msg == "loadmogit" then
    tryLoadMogItDataModules(true)
  elseif msg == "resetlooks" then
    db.looks = {}
    everCollectedByItem = {}
    DEFAULT_CHAT_FRAME:AddMessage("IchaMog: cleared shared-appearance keys. Exact item IDs kept; re-equip gear to refresh look keys.")
  elseif msg == "wipe" then
    db.items = {}
    db.looks = {}
    everCollectedByItem = {}
    DEFAULT_CHAT_FRAME:AddMessage("IchaMog: collection wiped for this character.")
  elseif msg == "help" or msg == "" then
    printHelp()
  else
    printHelp()
  end
end

ensureDB()

local f = CreateFrame("Frame")
f:RegisterEvent("ADDON_LOADED")
f:RegisterEvent("PLAYER_LOGIN")
f:RegisterEvent("UNIT_INVENTORY_CHANGED")

f:SetScript("OnEvent", function(_, event, arg1)
  if event == "ADDON_LOADED" and arg1 == ADDON_NAME then
    ensureDB()
  elseif event == "ADDON_LOADED" then
    if arg1 == "MogIt" then
      tryAutoEnableMogItData(false)
    end
  elseif event == "PLAYER_LOGIN" then
    ensureDB()
    everCollectedByItem = {}
    migrateDB()
    runNativeDisplaySelfTest()
    tryAutoEnableMogItData(false)
    if _G.IchaMog_LoadMogItData == true then
      tryLoadMogItDataModules(false)
    end
    hookTooltips()
    scanPlayerEquipment()
  elseif event == "UNIT_INVENTORY_CHANGED" and arg1 == "player" then
    ensureDB()
    scheduleScan()
  end
end)
