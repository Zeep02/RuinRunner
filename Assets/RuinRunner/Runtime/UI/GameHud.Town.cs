using System;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameHud
    {
        GUIStyle compactStyle, compactCenter, compactButton, talentText;
        Vector2 serviceScroll;
        int talentFamilyTab;
        string merchantDetailOffer;
        Vector2 merchantDetailScroll;
        Vector2 exchangeDetailScroll, exchangeRecipeScroll;
        string exchangeDetailRecipe;
        string[] talentSummaries;
        readonly System.Collections.Generic.Dictionary<(string, float), string> panelText = new System.Collections.Generic.Dictionary<(string, float), string>();
        readonly System.Collections.Generic.Dictionary<string, string> merchantSummaries = new System.Collections.Generic.Dictionary<string, string>();
        string WrapPanelText(string text, float width)
        {
            var key = (text, width);
            if (panelText.TryGetValue(key, out var wrapped))
                return wrapped;
            if (panelText.Count >= 128)
                panelText.Clear();
            wrapped = GameTypography.WrapWords(text, compactStyle, width);
            panelText.Add(key, wrapped);
            return wrapped;
        }
        string MerchantSummary(string text)
        {
            if (merchantSummaries.TryGetValue(text, out var summary))
                return summary;
            if (merchantSummaries.Count >= 128)
                merchantSummaries.Clear();
            var candidate = text.Split('\n')[0];
            bool shortened = false;
            summary = GameTypography.WrapWords(candidate, talentText, 300);
            while (candidate.Length > 0 && talentText.CalcHeight(new GUIContent(summary + (shortened ? "…" : "")), 300) > 40)
            {
                int boundary = candidate.LastIndexOf(' ');
                candidate = boundary > 0 ? candidate.Substring(0, boundary) : candidate.Substring(0, candidate.Length - 1);
                shortened = true;
                summary = GameTypography.WrapWords(candidate, talentText, 300);
            }
            summary += shortened ? "…" : "";
            merchantSummaries.Add(text, summary);
            return summary;
        }
        void PrepareTownStyles()
        {
            if (compactStyle != null)
                return;
            compactStyle = new GUIStyle(detailStyle) { fontSize = 18, clipping = TextClipping.Clip, wordWrap = true };
            talentText = new GUIStyle(compactStyle) { fontSize = 16 };
            compactCenter = new GUIStyle(compactStyle) { alignment = TextAnchor.MiddleCenter };
            compactButton = new GUIStyle(buttonStyle) { fontSize = 18, clipping = TextClipping.Clip };
            talentSummaries = new string[TalentCatalog.All.Length];
            for (int i = 0; i < talentSummaries.Length; i++)
                talentSummaries[i] = GameTypography.WrapWords(TalentCatalog.All[i].Summary, talentText, 274);
        }
        void DrawSessionOverlay(float width, float height)
        {
            PrepareTownStyles();
            if (game.IsTown && !game.IsInventoryOpen && !game.IsServiceOpen && !game.IsManuallyPaused)
            {
                DrawPanel(new Rect(20, 20, 350, 74));
                GUI.Label(new Rect(36, 29, 318, 27), "길잡이 쉼터", bodyStyle);
                GUI.Label(new Rect(36, 61, 318, 24), "유산 " + game.MetaProgression.LegacyShards, compactStyle);
                if (!string.IsNullOrEmpty(game.SessionMessage))
                    GUI.Label(new Rect(width / 2 - 350, 108, 700, 30), game.SessionMessage, compactCenter);
                DrawQuickSlots(width, height);
                DrawTownControls(height);
            }
            if (game.NearbyService != null && !game.IsServiceOpen && !game.IsPaused)
            {
                var label = Key(GameAction.Interact) + " · " + game.NearbyService.Label;
                DrawPanel(new Rect(width / 2 - 220, height - 200, 440, 46));
                GUI.Label(new Rect(width / 2 - 206, height - 195, 412, 36), label, compactCenter);
            }
            if (game.IsExpedition && !game.IsPaused)
            {
                DrawGold(96, 80, compactStyle);
            }
            if (game.IsServiceOpen)
                DrawService(width, height);
            if (game.IsPreparingTownArt || game.BackendHasPending || !string.IsNullOrEmpty(game.BackendErrorCode))
            {
                DrawPanel(new Rect(width / 2 - 310, height - 58, 620, 44));
                GUI.Label(new Rect(width / 2 - 292, height - 51, 445, 30), game.IsPreparingTownArt ? "마을을 준비하는 중…" : game.BackendStatus, compactStyle);
                GUI.enabled = !game.BackendBusy && !game.SessionOperationPending;
                if (GUI.Button(new Rect(width / 2 + 164, height - 51, 128, 30), "다시 확인", compactButton))
                    game.RetrySessionConnection();
                GUI.enabled = true;
            }
        }
        GUIStyle controlKeyStyle, controlLabelStyle;
        static readonly (string label, GameAction[] keys)[] TownControlRows =
        {
            ("이동", new[] { GameAction.MoveUp, GameAction.MoveLeft, GameAction.MoveDown, GameAction.MoveRight }),
            ("공격", new[] { GameAction.Attack }),
            ("무기 스킬", new[] { GameAction.Guard }),
            ("대시", new[] { GameAction.Dash }),
            ("상호작용", new[] { GameAction.Interact }),
            ("무기 교체", new[] { GameAction.Weapon1, GameAction.Weapon2, GameAction.Weapon3 }),
            ("가방", new[] { GameAction.Inventory }),
            ("지도", new[] { GameAction.Map }),
            ("일시정지", new[] { GameAction.Pause })
        };

        // Exhibition visitors start cold in the shelter; the keys sit in a corner, out of the way of the services.
        void DrawTownControls(float height)
        {
            if (controlKeyStyle == null)
            {
                controlKeyStyle = new GUIStyle(quickKeyStyle) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                controlKeyStyle.normal.textColor = new Color(1f, .93f, .78f);
                controlLabelStyle = new GUIStyle(quickKeyStyle) { fontSize = 14 };
                controlLabelStyle.normal.textColor = new Color(.82f, .78f, .72f);
            }
            const float rowHeight = 27f, chipHeight = 23f, pad = 12f, inset = 16f, labelWidth = 74f, gap = 4f;
            float ChipWidth(string text) => Mathf.Max(chipHeight + 2f, controlKeyStyle.CalcSize(new GUIContent(text)).x + 18f);
            var widest = 0f;
            foreach (var row in TownControlRows)
            {
                var rowWidth = -gap;
                foreach (var action in row.keys)
                    rowWidth += ChipWidth(ShortKey(action)) + gap;
                widest = Mathf.Max(widest, rowWidth);
            }
            var panelHeight = pad * 2f + TownControlRows.Length * rowHeight;
            var panel = new Rect(16f, height - 16f - panelHeight, inset * 2f + labelWidth + widest, panelHeight);
            DrawPanel(panel);
            for (int row = 0; row < TownControlRows.Length; row++)
            {
                var y = panel.y + pad + row * rowHeight;
                GUI.Label(new Rect(panel.x + inset, y, labelWidth, rowHeight), TownControlRows[row].label, controlLabelStyle);
                var x = panel.x + inset + labelWidth;
                foreach (var action in TownControlRows[row].keys)
                {
                    var text = ShortKey(action);
                    var chip = new Rect(x, y + (rowHeight - chipHeight) * .5f, ChipWidth(text), chipHeight);
                    GUI.Box(chip, string.Empty, slotStyle);
                    GUI.Label(chip, text, controlKeyStyle);
                    x += chip.width + gap;
                }
            }
        }

        string ShortKey(GameAction action)
        {
            var binding = game.Settings.GetBinding(action);
            var key = binding.primary != KeyCode.None ? binding.primary : binding.alternate;
            switch (key)
            {
                case KeyCode.Mouse0: return "좌클릭";
                case KeyCode.Mouse1: return "우클릭";
                case KeyCode.Mouse2: return "휠클릭";
                case KeyCode.Escape: return "Esc";
                case KeyCode.LeftShift: return "Shift";
                default: return GameSettingsService.GetKeyName(key);
            }
        }

        void DrawService(float width, float height)
        {
            var panel = new Rect(width / 2 - 500, height / 2 - 310, 1000, 620);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x + 28, panel.y + 16, 770, 36), game.OpenService.Label, bodyStyle);
            if (GUI.Button(new Rect(panel.xMax - 112, panel.y + 14, 88, 36), "닫기", compactButton))
                game.CloseService();
            if (game.OpenService == null)
                return;
            bool old = GUI.enabled;
            GUI.enabled = !game.SessionOperationPending && !game.EconomyOperationPending && !game.BackendBusy && !game.BackendHasPending;
            var body = new Rect(panel.x + 26, panel.y + 65, panel.width - 52, panel.height - 130);
            switch (game.OpenService.Kind)
            {
                case FieldServiceKind.StartingGear:
                    DrawStartingGear(body);
                    break;
                case FieldServiceKind.Talents:
                    DrawTalents(body);
                    break;
                case FieldServiceKind.Expedition:
                    DrawExpedition(body);
                    break;
                case FieldServiceKind.Merchant:
                    DrawMerchant(body);
                    break;
                case FieldServiceKind.ShapeAnvil:
                case FieldServiceKind.OathStone:
                    DrawFieldGrowth(body);
                    break;
                case FieldServiceKind.ExchangeAltar:
                    DrawExchange(body);
                    break;
            }
            GUI.enabled = old;
            GUI.Label(new Rect(panel.x + 28, panel.yMax - 52, panel.width - 56, 36), game.SessionMessage, compactStyle);
        }
        void DrawStartingGear(Rect rect)
        {
            var list = game.BackendCatalog?.starterWeapons;
            if (list == null)
                return;
            for (int i = 0; i < list.Length; i++)
            {
                var definition = WeaponLootTable.Find(list[i].id);
                if (definition == null)
                    continue;
                var cell = new Rect(rect.x + (i % 3) * 316, rect.y + (i / 3) * 166, 302, 154);
                GUI.Box(cell, "", slotStyle);
                ItemIconLibrary.DrawGUI(new Rect(cell.x + 14, cell.y + 14, 54, 58), InventoryIconFactory.Get(definition.Id, definition.Color, InventoryIconKind.Weapon));
                GUI.Label(new Rect(cell.x + 82, cell.y + 17, 204, 32), definition.Name, compactStyle);
                GUI.Label(new Rect(cell.x + 82, cell.y + 49, 204, 36), WeaponFamilyCatalog.GetName(definition.Family), compactStyle);
                bool unlocked = Array.IndexOf(game.SaveSlots.ActiveData.unlockedWeaponIds, definition.Id) >= 0;
                bool chosen = game.SaveSlots.ActiveData.startingWeaponId == definition.Id;
                if (GUI.Button(new Rect(cell.x + 14, cell.y + 106, 274, 40), chosen ? "선택 중" : unlocked ? "선택 · 시험" : "유산 " + list[i].unlockCost + " · 해금", compactButton))
                {
                    if (unlocked)
                        game.ChooseStartingWeapon(definition.Id);
                    else
                        game.UnlockTownChoice("weapon", definition.Id);
                }
            }
        }
        static readonly string[] TalentIcons ={
            "mirror_shard","last_light_pendant","cinder_seed","shadow_twin_ring","thunder_charm","vampire_fang",
            "blood_pact","gravity_pearl","mana_thread","sage_sigil","soul_capacitor","arcane_lens",
            "frost_wave_necklace","rush_spur","time_hourglass","trail_spindle","storm_core","guardian_feather",
            "powder_satchel","phoenix_ash","holy_tithe","poison_censer","focus_prism","vine_snare"};
        void DrawTalents(Rect rect)
        {
            string[] families = { "칼날", "마나", "발놀림", "운명", "수련" };
            for (int family = 0; family < families.Length; family++)
            {
                bool active = false;
                if (family < 4)
                    foreach (var id in game.ActiveTalents.Ids)
                        if ((int)TalentCatalog.Find(id).Family == family)
                            active = true;
                var tab = new Rect(rect.x + family * 190, rect.y, 180, 44);
                if (GUI.Button(tab, families[family] + (active ? " · 활성" : ""), talentFamilyTab == family ? selectedTabStyle : compactButton))
                    talentFamilyTab = family;
            }
            GUI.Label(new Rect(rect.x, rect.y + 50, rect.width, 28), "유산 " + game.MetaProgression.LegacyShards, compactStyle);
            if (talentFamilyTab >= 4)
            {
                DrawMastery(rect);
                return;
            }
            for (int slot = 0; slot < TalentCatalog.PerFamily; slot++)
            {
                int index = talentFamilyTab * TalentCatalog.PerFamily + slot;
                var t = TalentCatalog.All[index];
                var cell = new Rect(rect.x + slot % 3 * 316, rect.y + 84 + slot / 3 * 198, 302, 186);
                bool selected = game.ActiveTalents.Has(t.Type);
                GUI.Box(cell, "", selected ? selectedSlotStyle : slotStyle);
                DrawContentIcon(new Rect(cell.x + 12, cell.y + 10, 40, 40), TalentIcons[index], "accessory");
                GUI.Label(new Rect(cell.x + 62, cell.y + 16, 230, 30), t.Name, compactStyle);
                GUI.Label(new Rect(cell.x + 14, cell.y + 56, 276, 84), talentSummaries[index], talentText);
                bool unlocked = Array.IndexOf(game.SaveSlots.ActiveData.unlockedTalentIds, t.Id) >= 0;
                var offer = Array.Find(game.BackendCatalog.talents, v => v.id == t.Id);
                bool achievement = offer == null || game.SaveSlots.ActiveData.totalRuns >= offer.requiredRuns;
                string label = selected ? "해제" : unlocked ? "선택" : !achievement ? "원정 " + offer.requiredRuns + "회 필요" : "유산 " + (offer?.unlockCost ?? 0) + " 해금";
                bool before = GUI.enabled;
                GUI.enabled = before && (unlocked || achievement);
                if (GUI.Button(new Rect(cell.x + 12, cell.y + 146, 278, 32), label, compactButton))
                {
                    if (unlocked)
                        game.ToggleTalent(t.Id);
                    else
                        game.UnlockTownChoice("talent", t.Id);
                }
                GUI.enabled = before;
            }
        }
        void DrawMastery(Rect rect)
        {
            var unlocked = game.SaveSlots.ActiveData.unlockedTalentIds;
            for (int row = 0; row < TalentCatalog.Mastery.Length; row++)
            {
                var track = TalentCatalog.Mastery[row];
                int rank = TalentCatalog.MasteryRank(unlocked, track);
                var box = new Rect(rect.x, rect.y + 84 + row * 49, rect.width, 44);
                GUI.Box(box, "", slotStyle);
                GUI.Label(new Rect(box.x + 14, box.y + 8, 430, 30), track.Name + " · " + track.Effect, compactStyle);
                var old = GUI.color;
                for (int pip = 0; pip < TalentCatalog.MasteryRanks; pip++)
                {
                    GUI.color = pip < rank ? new Color(1f, .76f, .16f) : new Color(.16f, .13f, .2f);
                    GUI.DrawTexture(new Rect(box.x + 470 + pip * 38, box.y + 13, 30, 18), Texture2D.whiteTexture);
                }
                GUI.color = old;
                bool maxed = rank >= TalentCatalog.MasteryRanks;
                int cost = maxed ? 0 : TalentCatalog.MasteryCosts[rank];
                bool before = GUI.enabled;
                GUI.enabled = before && !maxed && game.MetaProgression.LegacyShards >= cost;
                if (GUI.Button(new Rect(box.xMax - 176, box.y + 6, 168, 32), maxed ? "완료" : "유산 " + cost, compactButton))
                    game.UnlockTownChoice("talent", TalentCatalog.MasteryId(track, rank + 1));
                GUI.enabled = before;
            }
        }
        void DrawExpedition(Rect rect)
        {
            var weapon = WeaponLootTable.Find(game.SaveSlots.ActiveData.startingWeaponId);
            GUI.Label(new Rect(rect.x + 70, rect.y + 32, rect.width - 140, 45), "새 원정", titleStyle);
            GUI.Label(new Rect(rect.x + 100, rect.y + 104, rect.width - 200, 42), "시작 무기 · " + weapon.Name, bodyStyle);
            var names = new System.Collections.Generic.List<string>();
            foreach (var id in game.ActiveTalents.Ids)
                names.Add(TalentCatalog.Find(id).Name);
            GUI.Label(new Rect(rect.x + 100, rect.y + 158, rect.width - 200, 80), "재능 · " + string.Join(" / ", names), compactStyle);
            if (GUI.Button(new Rect(rect.center.x - 150, rect.y + 365, 300, 52), "출정", buttonStyle))
                game.DepartTown();
        }
        void DrawMerchant(Rect rect)
        {
            var shop = game.CurrentShop;
            if (shop == null)
                return;
            DrawGold(rect.x, rect.y, compactStyle);
            for (int i = 0; i < shop.offers.Length; i++)
            {
                var o = shop.offers[i];
                var cell = new Rect(rect.x, rect.y + 34 + i * 90, 560, 84);
                GUI.Box(cell, "", slotStyle);
                DrawContentIcon(new Rect(cell.x + 10, cell.y + 8, 68, 68), o.contentId, o.kind);
                GUI.Label(new Rect(cell.x + 90, cell.y + 6, 300, 28), ContentName(o.contentId, o.kind), compactStyle);
                GUI.Label(new Rect(cell.x + 90, cell.y + 36, 300, 40), MerchantSummary(ContentDescription(o.contentId, o.kind)), talentText);
                bool old = GUI.enabled;
                GUI.enabled = old && o.stock > 0;
                if (GUI.Button(new Rect(cell.x + 416, cell.y + 9, 130, 30), o.stock <= 0 ? "품절" : o.price + " 골드", compactButton))
                    game.BuyShopOffer(o.offerId);
                GUI.enabled = old;
                if (GUI.Button(new Rect(cell.x + 416, cell.y + 47, 130, 28), "상세", compactButton))
                {
                    merchantDetailOffer = o.offerId;
                    merchantDetailScroll = Vector2.zero;
                }
            }
            var side = new Rect(rect.x + 580, rect.y, rect.width - 580, rect.height);
            var selected = Array.Find(shop.offers, o => o.offerId == merchantDetailOffer);
            if (selected == null)
                DrawOwnedEquipmentList(side, true);
            else
                DrawMerchantDetails(side, selected);
        }
        void DrawMerchantDetails(Rect rect, RuinRunner.Backend.ShopOffer offer)
        {
            string description = WrapPanelText(ContentDescription(offer.contentId, offer.kind), rect.width - 38);
            float contentHeight = compactStyle.CalcHeight(new GUIContent(description), rect.width - 38);
            rect.height = Mathf.Clamp(130 + contentHeight + 62, 272, rect.height);
            GUI.Box(rect, "", slotStyle);
            if (GUI.Button(new Rect(rect.x, rect.y, rect.width, 30), "판매 목록으로", compactButton))
                merchantDetailOffer = null;
            DrawContentIcon(new Rect(rect.x + 12, rect.y + 44, 64, 64), offer.contentId, offer.kind);
            GUI.Label(new Rect(rect.x + 92, rect.y + 48, rect.width - 108, 64), WrapPanelText(ContentName(offer.contentId, offer.kind), rect.width - 108), compactStyle);
            var view = new Rect(rect.x + 8, rect.y + 118, rect.width - 16, rect.height - 176);
            merchantDetailScroll = GUI.BeginScrollView(view, merchantDetailScroll, new Rect(0, 0, view.width - 22, Mathf.Max(view.height, contentHeight)));
            GUI.Label(new Rect(0, 0, view.width - 22, contentHeight), description, compactStyle);
            GUI.EndScrollView();
            bool old = GUI.enabled;
            GUI.enabled = old && offer.stock > 0;
            if (GUI.Button(new Rect(rect.x + 10, rect.yMax - 48, rect.width - 20, 34), offer.stock > 0 ? offer.price + " 골드 · 구매" : "품절", compactButton))
                game.BuyShopOffer(offer.offerId);
            GUI.enabled = old;
        }
        string ContentDescription(string id, string kind)
        {
            if (kind == "heal")
                return "체력 " + (game.BackendCatalog?.economy?.healAmount ?? 0) + " 회복";
            if (kind == "accessory")
                return AccessoryLootTable.Find(id)?.Description ?? "";
            var weapon = WeaponLootTable.Find(id);
            if (weapon == null)
                return "";
            return WeaponActionCatalog.Get(weapon).Description + "\n기본 공격력 " + weapon.AttackPower.ToString("0.#") +
                (weapon.AttackType == WeaponAttackType.Ranged ? " · 탄창 " + weapon.MagazineSize + "\n장전 " + weapon.ReloadSeconds.ToString("0.#") + "초" : "");
        }
        void DrawOwnedEquipmentList(Rect rect, bool sell)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 28), sell ? "판매 / 교환할 장비" : "대상 무기", compactStyle);
            var items = new System.Collections.Generic.List<OwnedEquipment>();
            foreach (var item in game.Inventory.AllEquipment())
                if (sell || item is OwnedWeapon)
                    items.Add(item);
            var view = new Rect(rect.x, rect.y + 36, rect.width, rect.height - 36);
            serviceScroll = GUI.BeginScrollView(view, serviceScroll, new Rect(0, 0, view.width - 22, items.Count * 72));
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var cell = new Rect(0, i * 72, view.width - 22, 66);
                GUI.Box(cell, "", game.ServiceSelectedEquipment == item.Identity ? selectedSlotStyle : slotStyle);
                if (GUI.Button(new Rect(8, cell.y + 5, cell.width - 16, 30), item.Name, compactButton))
                    game.ServiceSelectedEquipment = item.Identity;
                if (sell)
                {
                    bool old = GUI.enabled;
                    GUI.enabled = old && !item.IsStartingEquipment && game.Inventory.CanDisassemble(item.Identity);
                    if (GUI.Button(new Rect(8, cell.y + 35, cell.width - 16, 25), item.IsStartingEquipment ? "시작 장비 · 판매 불가" : "판매 · " + game.SellPrice(item) + " 골드", compactButton))
                        game.SellShopItem(item.Identity);
                    GUI.enabled = old;
                }
            }
            GUI.EndScrollView();
        }
        void DrawFieldGrowth(Rect rect)
        {
            DrawOwnedEquipmentList(new Rect(rect.x, rect.y, 270, rect.height), false);
            var area = new Rect(rect.x + 294, rect.y, rect.width - 294, rect.height);
            var weapon = game.Inventory.FindOwned(game.ServiceSelectedEquipment) as OwnedWeapon;
            if (game.OpenService.Used)
                return;
            if (weapon == null)
                return;
            GUI.Label(new Rect(area.x, area.y, area.width, 50), weapon.Name, bodyStyle);
            if (game.OpenService.Kind == FieldServiceKind.ShapeAnvil)
            {
                var choices = weapon.Definition.AttackType == WeaponAttackType.Melee ? new[] { EquipmentMutation.LongFinisher, EquipmentMutation.WideFinisher } : new[] { EquipmentMutation.Ricochet, EquipmentMutation.AnchorRound };
                for (int i = 0; i < choices.Length; i++)
                {
                    var m = choices[i];
                    var box = new Rect(area.x, area.y + 64 + i * 156, area.width, 140);
                    GUI.Box(box, "", slotStyle);
                    GUI.Label(new Rect(box.x + 18, box.y + 14, box.width - 36, 58), EquipmentMutationRules.Name(m) + "\n" + EquipmentMutationRules.Summary(m), compactStyle);
                    bool old = GUI.enabled;
                    GUI.enabled = old && EquipmentMutationRules.Compatible(weapon, m);
                    if (GUI.Button(new Rect(box.x + 18, box.y + 88, box.width - 36, 38), "이 변형 선택", compactButton))
                        game.ApplyFieldMutation(m);
                    GUI.enabled = old;
                }
            }
            else
            {
                int row = 0;
                foreach (EquipmentOath oath in Enum.GetValues(typeof(EquipmentOath)))
                {
                    if (!game.CanBeginOath(weapon, oath))
                        continue;
                    var box = new Rect(area.x, area.y + 60 + row++ * 126, area.width, 118);
                    GUI.Box(box, "", slotStyle);
                    GUI.Label(new Rect(box.x + 12, box.y + 8, box.width - 150, 24), EquipmentMutationRules.OathName(oath), compactStyle);
                    GUI.Label(new Rect(box.x + 12, box.y + 36, box.width - 150, 74), EquipmentMutationRules.OathCondition(oath) + "\n" + EquipmentMutationRules.OathResult(oath), talentText);
                    if (GUI.Button(new Rect(box.xMax - 126, box.y + 42, 110, 38), "서약", compactButton))
                        game.AcceptEquipmentOath(oath);
                }

            }
        }
        void DrawExchange(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, 302, 30), game.OpenService.Used ? "교환 완료" : "장신구 2개 → 유물 1개", compactStyle);
            var list = new Rect(rect.x, rect.y + 38, 314, rect.height - 38);
            exchangeRecipeScroll = GUI.BeginScrollView(list, exchangeRecipeScroll, new Rect(0, 0, 292, game.ExchangeRecipes.Count * 142));
            for (int i = 0; i < game.ExchangeRecipes.Count; i++)
            {
                var recipe = game.ExchangeRecipes[i];
                var box = new Rect(0, i * 142, 292, 132);
                bool selected = game.PendingExchangeRecipe == recipe.Id;
                GUI.Box(box, "", selected ? selectedSlotStyle : slotStyle);
                DrawContentIcon(new Rect(14, box.y + 12, 58, 58), recipe.OutputId, "accessory");
                GUI.Label(new Rect(86, box.y + 16, 192, 50), WrapPanelText(ContentName(recipe.OutputId, "accessory"), 192), compactStyle);
                DrawContentIcon(new Rect(16, box.y + 86, 32, 32), recipe.InputIds[0], "accessory");
                GUI.Label(new Rect(51, box.y + 88, 20, 28), "+", compactCenter);
                DrawContentIcon(new Rect(74, box.y + 86, 32, 32), recipe.InputIds[1], "accessory");
                if (GUI.Button(new Rect(126, box.y + 84, 150, 34), selected ? "선택 중" : "효과 · 재료", compactButton))
                    game.PreviewExchange(recipe.Id);
            }
            GUI.EndScrollView();
            var preview = new Rect(rect.x + 330, rect.y, rect.width - 330, rect.height);
            GUI.Box(preview, "", slotStyle);
            var choice = game.FindExchangeRecipe(game.PendingExchangeRecipe);
            if (choice == null)
                return;
            if (exchangeDetailRecipe != choice.Id)
            {
                exchangeDetailRecipe = choice.Id;
                exchangeDetailScroll = Vector2.zero;
            }
            var ingredients = game.ExchangeIngredients(choice.Id);
            var output = AccessoryLootTable.Find(choice.OutputId);
            if (output == null)
                return;
            var view = new Rect(preview.x + 16, preview.y + 16, preview.width - 32, preview.height - 108);
            float contentWidth = view.width - 22, columnWidth = (contentWidth - 20) * .5f;
            string effect = WrapPanelText(EquipmentUpgradeRules.AccessoryDescription(output, 0, game.Inventory), contentWidth);
            float effectHeight = compactStyle.CalcHeight(new GUIContent(effect), contentWidth);
            float materialY = 100 + effectHeight;
            float materialHeight = 0;
            for (int i = 0; i < choice.InputIds.Count; i++)
            {
                var definition = AccessoryLootTable.Find(choice.InputIds[i]);
                if (definition == null)
                    continue;
                string description = WrapPanelText(EquipmentUpgradeRules.AccessoryDescription(definition, 0, game.Inventory), columnWidth);
                materialHeight = Mathf.Max(materialHeight, compactStyle.CalcHeight(new GUIContent(description), columnWidth));
            }
            exchangeDetailScroll = GUI.BeginScrollView(view, exchangeDetailScroll, new Rect(0, 0, contentWidth, Mathf.Max(view.height, materialY + 128 + materialHeight)));
            DrawContentIcon(new Rect(0, 0, 72, 72), choice.OutputId, "accessory");
            GUI.Label(new Rect(88, 0, contentWidth - 88, 26), "획득할 유물", talentText);
            GUI.Label(new Rect(88, 28, contentWidth - 88, 48), WrapPanelText(output.Name, contentWidth - 88), compactStyle);
            GUI.Label(new Rect(0, 84, contentWidth, effectHeight), effect, compactStyle);
            GUI.Label(new Rect(0, materialY, contentWidth, 28), "소비할 장신구", compactStyle);
            for (int i = 0; i < choice.InputIds.Count; i++)
            {
                var definition = AccessoryLootTable.Find(choice.InputIds[i]);
                if (definition == null)
                    continue;
                float x = i * (columnWidth + 20);
                int owned = 0;
                foreach (var item in game.Inventory.OwnedAccessories)
                    if (item.Definition.Id == definition.Id)
                        owned++;
                DrawContentIcon(new Rect(x, materialY + 38, 48, 48), definition.Id, "accessory");
                GUI.Label(new Rect(x + 60, materialY + 36, columnWidth - 60, 46), WrapPanelText(definition.Name, columnWidth - 60), compactStyle);
                GUI.Label(new Rect(x, materialY + 88, columnWidth, 24), owned > 0 ? "보유 " + owned + "개 · 1개 소비" : "부족 · 1개 필요", talentText);
                string description = WrapPanelText(EquipmentUpgradeRules.AccessoryDescription(definition, 0, game.Inventory), columnWidth);
                GUI.Label(new Rect(x, materialY + 116, columnWidth, materialHeight), description, compactStyle);
            }
            GUI.EndScrollView();
            bool old = GUI.enabled;
            GUI.enabled = old && ingredients.Length == 2 && !game.OpenService.Used;
            string action = game.OpenService.Used ? "이 제단은 사용했습니다" : ingredients.Length == 2 ? "장신구 두 개 소비 · 교환" : "재료 부족";
            if (GUI.Button(new Rect(preview.x + 16, preview.yMax - 46, preview.width - 32, 34), action, compactButton))
                game.ConfirmExchange();
            GUI.enabled = old;
        }
        string ContentName(string id, string kind) => kind == "heal" ? "회복 물약" : kind == "weapon" ? WeaponLootTable.Find(id)?.Name ?? id : AccessoryLootTable.Find(id)?.Name ?? id;
        void DrawContentIcon(Rect rect, string id, string kind)
        {
            if (kind == "heal")
            {
                ItemIconLibrary.DrawGUI(rect, InventoryIconFactory.Get("sapphire_flask", Color.white, InventoryIconKind.Accessory));
                return;
            }
            var weapon = kind == "weapon" ? WeaponLootTable.Find(id) : null;
            var accessory = kind == "accessory" ? AccessoryLootTable.Find(id) : null;
            if (weapon != null || accessory != null)
                ItemIconLibrary.DrawGUI(rect, InventoryIconFactory.Get(id, weapon != null ? weapon.Color : accessory.Color, weapon != null ? InventoryIconKind.Weapon : InventoryIconKind.Accessory));
        }
    }
}
