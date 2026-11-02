using System;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class PrototypeHud
    {
        GUIStyle compactStyle,compactCenter,compactButton,talentText;
        Vector2 serviceScroll;
        int talentFamilyTab;
        string merchantDetailOffer;
        Vector2 merchantDetailScroll;
        Vector2 exchangeDetailScroll,exchangeRecipeScroll;
        string exchangeDetailRecipe;
        string[] talentSummaries;
        readonly System.Collections.Generic.Dictionary<(string, float), string> panelText = new System.Collections.Generic.Dictionary<(string, float), string>();
        readonly System.Collections.Generic.Dictionary<string,string> merchantSummaries = new System.Collections.Generic.Dictionary<string,string>();
        string WrapPanelText(string text,float width)
        {
            var key=(text,width);
            if(panelText.TryGetValue(key,out var wrapped))return wrapped;
            if(panelText.Count>=128)panelText.Clear();
            wrapped=GameTypography.WrapWords(text,compactStyle,width);panelText.Add(key,wrapped);return wrapped;
        }
        string MerchantSummary(string text)
        {
            if(merchantSummaries.TryGetValue(text,out var summary))return summary;
            if(merchantSummaries.Count>=128)merchantSummaries.Clear();
            var candidate=text.Split('\n')[0];bool shortened=false;
            summary=GameTypography.WrapWords(candidate,talentText,300);
            while(candidate.Length>0&&talentText.CalcHeight(new GUIContent(summary+(shortened?"…":"")),300)>40)
            {
                int boundary=candidate.LastIndexOf(' ');
                candidate=boundary>0?candidate.Substring(0,boundary):candidate.Substring(0,candidate.Length-1);
                shortened=true;summary=GameTypography.WrapWords(candidate,talentText,300);
            }
            summary+=shortened?"…":"";merchantSummaries.Add(text,summary);return summary;
        }
        void PrepareTownStyles()
        {
            if(compactStyle!=null)return;
            compactStyle=new GUIStyle(detailStyle){fontSize=18,clipping=TextClipping.Clip,wordWrap=true};
            talentText=new GUIStyle(compactStyle){fontSize=16};
            compactCenter=new GUIStyle(compactStyle){alignment=TextAnchor.MiddleCenter};
            compactButton=new GUIStyle(buttonStyle){fontSize=18,clipping=TextClipping.Clip};
            talentSummaries=new string[TalentCatalog.All.Length];
            for(int i=0;i<talentSummaries.Length;i++)
                talentSummaries[i]=GameTypography.WrapWords(TalentCatalog.All[i].Summary,compactStyle,266);
        }
        void DrawSessionOverlay(float width,float height)
        {
            PrepareTownStyles();
            if(game.IsTown&&!game.IsInventoryOpen&&!game.IsServiceOpen&&!game.IsManuallyPaused)
            {
                DrawPanel(new Rect(20,20,350,74));GUI.Label(new Rect(36,29,318,27),"길잡이 쉼터",bodyStyle);
                GUI.Label(new Rect(36,61,318,24),"유산 "+game.MetaProgression.LegacyShards+" · 활성 재능 "+game.ActiveTalents.Ids.Length+"/3",compactStyle);
                if(!string.IsNullOrEmpty(game.SessionMessage))GUI.Label(new Rect(width/2-350,108,700,30),game.SessionMessage,compactCenter);
                DrawQuickSlots(width,height);
            }
            if(game.NearbyService!=null&&!game.IsServiceOpen&&!game.IsPaused)
            {
                var label=Key(GameAction.Interact)+" · "+game.NearbyService.Label;
                DrawPanel(new Rect(width/2-220,height-200,440,46));GUI.Label(new Rect(width/2-206,height-195,412,36),label,compactCenter);
            }
            if(game.IsExpedition&&!game.IsPaused)
            {
                GUI.Label(new Rect(24,178,200,28),"골드 "+game.RunGold,compactStyle);
                float y=height-202;
                if(game.ActiveTalents.Has(TalentId.ChestIntent))
                {if(GUI.Button(new Rect(24,y,218,34),"상자 · "+(game.ChestPrefersAccessories?"장신구":"무기"),compactButton))game.ChestPrefersAccessories=!game.ChestPrefersAccessories;y-=38;}
                if(game.ActiveTalents.Has(TalentId.SupplyChoice))
                {if(GUI.Button(new Rect(24,y,218,34),"보급 · "+(game.SupplyUsesMana?"마나":"체력"),compactButton))game.SupplyUsesMana=!game.SupplyUsesMana;y-=38;}
                if(game.IsRewardReserved)
                {bool enabled=GUI.enabled;GUI.enabled=game.EnemiesAlive==0;if(GUI.Button(new Rect(24,y,218,34),"보관한 보상 선택",compactButton))game.ResolveReservedReward();GUI.enabled=enabled;}
            }
            if(game.IsServiceOpen)DrawService(width,height);
            if(game.SessionOperationPending||game.EconomyOperationPending||game.BackendHasPending||!string.IsNullOrEmpty(game.BackendErrorCode))
            {
                DrawPanel(new Rect(width/2-310,height-58,620,44));GUI.Label(new Rect(width/2-292,height-51,445,30),game.IsPreparingTownArt?"마을을 준비하는 중…":game.BackendStatus,compactStyle);
                GUI.enabled=!game.BackendBusy&&!game.SessionOperationPending;
                if(GUI.Button(new Rect(width/2+164,height-51,128,30),"다시 확인",compactButton))game.RetrySessionConnection();GUI.enabled=true;
            }
        }
        void DrawService(float width,float height)
        {
            var panel=new Rect(width/2-500,height/2-310,1000,620);DrawPanel(panel);
            GUI.Label(new Rect(panel.x+28,panel.y+16,770,36),game.OpenService.Label,bodyStyle);
            if(GUI.Button(new Rect(panel.xMax-112,panel.y+14,88,36),"닫기",compactButton))game.CloseService();
            if(game.OpenService==null)return;
            bool old=GUI.enabled;GUI.enabled=!game.SessionOperationPending&&!game.EconomyOperationPending&&!game.BackendBusy&&!game.BackendHasPending;
            var body=new Rect(panel.x+26,panel.y+65,panel.width-52,panel.height-130);
            switch(game.OpenService.Kind)
            {
                case FieldServiceKind.StartingGear:DrawStartingGear(body);break;
                case FieldServiceKind.Talents:DrawTalents(body);break;
                case FieldServiceKind.Expedition:DrawExpedition(body);break;
                case FieldServiceKind.Merchant:DrawMerchant(body);break;
                case FieldServiceKind.ShapeAnvil:case FieldServiceKind.OathStone:DrawFieldGrowth(body);break;
                case FieldServiceKind.ExchangeAltar:DrawExchange(body);break;
            }
            GUI.enabled=old;
            GUI.Label(new Rect(panel.x+28,panel.yMax-52,panel.width-56,36),string.IsNullOrEmpty(game.SessionMessage)?game.BackendStatus:game.SessionMessage,compactStyle);
        }
        void DrawStartingGear(Rect rect)
        {
            var list=game.BackendCatalog?.starterWeapons;if(list==null)return;
            GUI.Label(new Rect(rect.x,rect.y,rect.width,38),"출정할 무기를 고르세요. 옆 수련 석상에서 시험할 수 있습니다.",compactStyle);
            for(int i=0;i<list.Length;i++)
            {
                var definition=WeaponLootTable.Find(list[i].id);if(definition==null)continue;
                var cell=new Rect(rect.x+(i%3)*316,rect.y+50+(i/3)*180,302,162);GUI.Box(cell,"",slotStyle);
                ItemIconLibrary.DrawGUI(new Rect(cell.x+14,cell.y+14,54,58),InventoryIconFactory.Get(definition.Id,definition.Color,InventoryIconKind.Weapon));
                GUI.Label(new Rect(cell.x+82,cell.y+17,204,32),definition.Name,compactStyle);
                GUI.Label(new Rect(cell.x+82,cell.y+49,204,36),WeaponFamilyCatalog.GetName(definition.Family),compactStyle);
                bool unlocked=Array.IndexOf(game.SaveSlots.ActiveData.unlockedWeaponIds,definition.Id)>=0;
                bool chosen=game.SaveSlots.ActiveData.startingWeaponId==definition.Id;
                if(GUI.Button(new Rect(cell.x+14,cell.y+106,274,40),chosen?"선택 중":unlocked?"선택 · 시험":"유산 "+list[i].unlockCost+" · 해금",compactButton))
                {if(unlocked)game.ChooseStartingWeapon(definition.Id);else game.UnlockTownChoice("weapon",definition.Id);}
            }
        }
        void DrawTalents(Rect rect)
        {
            GUI.Label(new Rect(rect.x,rect.y,rect.width,32),"활성 "+game.ActiveTalents.Ids.Length+"/3 · 계열당 하나 · 마을에서 무료 재배치",compactStyle);
            string[] families={"전투 운용","기동","탐색","보상 운용"};
            for(int family=0;family<4;family++)
            {
                bool active=false;foreach(var id in game.ActiveTalents.Ids)if((int)TalentCatalog.Find(id).Family==family)active=true;
                var tab=new Rect(rect.x+family*238,rect.y+42,226,44);
                if(GUI.Button(tab,families[family]+(active?" · 활성":""),talentFamilyTab==family?selectedTabStyle:compactButton))talentFamilyTab=family;
            }
            string[] icons={"rust_sword","guard_bell","mana_thread","storm_feather","guardian_seal","rush_spur",
                "storm_feather","sapphire_flask","rush_spur","guard_bell","guardian_seal","mana_thread"};
            for(int column=0;column<3;column++)
            {
                int index=talentFamilyTab*3+column;
                var t=TalentCatalog.All[index];var cell=new Rect(rect.x+column*316,rect.y+108,302,308);
                bool selected=game.ActiveTalents.Has(t.Type);
                GUI.Box(cell,"",selected?selectedSlotStyle:slotStyle);
                DrawContentIcon(new Rect(cell.x+16,cell.y+16,56,56),icons[index],index==0?"weapon":"accessory");
                GUI.Label(new Rect(cell.x+86,cell.y+18,200,54),t.Name,compactStyle);
                GUI.Label(new Rect(cell.x+18,cell.y+96,266,110),talentSummaries[index],compactStyle);
                bool unlocked=Array.IndexOf(game.SaveSlots.ActiveData.unlockedTalentIds,t.Id)>=0;
                var offer=Array.Find(game.BackendCatalog.talents,v=>v.id==t.Id);
                bool achievement=offer==null||game.SaveSlots.ActiveData.totalRuns>=offer.requiredRuns;
                string state=selected?"이번 출정에 적용":unlocked?"해금 완료 · 무료 선택":!achievement?"원정 "+offer.requiredRuns+"회 필요":"해금 비용 · 유산 "+(offer?.unlockCost??0);
                GUI.Label(new Rect(cell.x+18,cell.y+210,266,30),state,compactStyle);
                string label=selected?"선택 해제":unlocked?"이 재능 선택":!achievement?"아직 잠겨 있음":"유산 "+(offer?.unlockCost??0)+" · 해금";
                bool before=GUI.enabled;GUI.enabled=before&&(unlocked||achievement);
                if(GUI.Button(new Rect(cell.x+16,cell.y+252,270,40),label,compactButton)){if(unlocked)game.ToggleTalent(t.Id);else game.UnlockTownChoice("talent",t.Id);}
                GUI.enabled=before;
            }
            GUI.Label(new Rect(rect.x,rect.y+438,rect.width,42),"같은 계열의 다른 재능을 고르면 교체됩니다. 출정 후에는 이번 원정의 선택이 고정됩니다.",compactStyle);
        }
        void DrawExpedition(Rect rect)
        {
            var weapon=WeaponLootTable.Find(game.SaveSlots.ActiveData.startingWeaponId);
            GUI.Label(new Rect(rect.x+70,rect.y+32,rect.width-140,45),"새 원정",titleStyle);
            GUI.Label(new Rect(rect.x+100,rect.y+104,rect.width-200,42),"시작 무기 · "+weapon.Name,bodyStyle);
            var names=new System.Collections.Generic.List<string>();foreach(var id in game.ActiveTalents.Ids)names.Add(TalentCatalog.Find(id).Name);
            GUI.Label(new Rect(rect.x+100,rect.y+158,rect.width-200,80),"재능 · "+string.Join(" / ",names)+"\n출정하면 이번 원정 동안 고정됩니다.",compactStyle);
            GUI.Label(new Rect(rect.x+100,rect.y+246,rect.width-200,70),"골드는 이번 원정에서만 사용합니다.\n유산과 해금은 귀환 뒤에도 남습니다.",compactStyle);
            if(game.BackendHasUnfinishedRun)
            {GUI.Label(new Rect(rect.x+100,rect.y+318,rect.width-200,40),"정리되지 않은 이전 원정이 있습니다.",compactStyle);if(GUI.Button(new Rect(rect.center.x-150,rect.y+380,300,52),"이전 원정 정리",compactButton))game.ResolveInterruptedExpedition();}
            else if(GUI.Button(new Rect(rect.center.x-150,rect.y+365,300,52),"출정",buttonStyle))game.DepartTown();
        }
        void DrawMerchant(Rect rect)
        {
            var shop=game.CurrentShop;if(shop==null){GUI.Label(rect,"상점 재고를 확인하고 있습니다.",compactStyle);return;}
            GUI.Label(new Rect(rect.x,rect.y,580,30),"골드 "+game.RunGold+" · 재방문해도 재고는 유지됩니다",compactStyle);
            for(int i=0;i<shop.offers.Length;i++)
            {
                var o=shop.offers[i];var cell=new Rect(rect.x,rect.y+34+i*90,560,84);GUI.Box(cell,"",slotStyle);
                DrawContentIcon(new Rect(cell.x+10,cell.y+8,68,68),o.contentId,o.kind);
                GUI.Label(new Rect(cell.x+90,cell.y+6,300,28),ContentName(o.contentId,o.kind),compactStyle);
                GUI.Label(new Rect(cell.x+90,cell.y+36,300,40),MerchantSummary(ContentDescription(o.contentId,o.kind)),talentText);
                bool old=GUI.enabled;GUI.enabled=old&&o.stock>0;
                if(GUI.Button(new Rect(cell.x+416,cell.y+9,130,30),o.stock<=0?"품절":o.price+" 골드",compactButton))game.BuyShopOffer(o.offerId);
                GUI.enabled=old;
                if(GUI.Button(new Rect(cell.x+416,cell.y+47,130,28),"상세",compactButton))
                {merchantDetailOffer=o.offerId;merchantDetailScroll=Vector2.zero;}
            }
            var side=new Rect(rect.x+580,rect.y,rect.width-580,rect.height);
            var selected=Array.Find(shop.offers,o=>o.offerId==merchantDetailOffer);
            if(selected==null)DrawOwnedEquipmentList(side,true);
            else DrawMerchantDetails(side,selected,shop.barterUsed);
        }
        void DrawMerchantDetails(Rect rect,RuinRunner.Backend.ShopOffer offer,bool barterUsed)
        {
            bool canBarter=game.ActiveTalents.Has(TalentId.MerchantBarter);
            string description=WrapPanelText(ContentDescription(offer.contentId,offer.kind),rect.width-38);
            float contentHeight=compactStyle.CalcHeight(new GUIContent(description),rect.width-38);
            rect.height=Mathf.Clamp(130+contentHeight+(canBarter?104:62),272,rect.height);
            GUI.Box(rect,"",slotStyle);
            if(GUI.Button(new Rect(rect.x,rect.y,rect.width,30),"판매 목록으로",compactButton))merchantDetailOffer=null;
            DrawContentIcon(new Rect(rect.x+12,rect.y+44,64,64),offer.contentId,offer.kind);
            GUI.Label(new Rect(rect.x+92,rect.y+48,rect.width-108,64),WrapPanelText(ContentName(offer.contentId,offer.kind),rect.width-108),compactStyle);
            var view=new Rect(rect.x+8,rect.y+118,rect.width-16,rect.height-(canBarter?218:176));
            merchantDetailScroll=GUI.BeginScrollView(view,merchantDetailScroll,new Rect(0,0,view.width-22,Mathf.Max(view.height,contentHeight)));
            GUI.Label(new Rect(0,0,view.width-22,contentHeight),description,compactStyle);GUI.EndScrollView();
            bool old=GUI.enabled;GUI.enabled=old&&offer.stock>0;
            if(GUI.Button(new Rect(rect.x+10,rect.yMax-(canBarter?84:48),rect.width-20,34),offer.stock>0?offer.price+" 골드 · 구매":"품절",compactButton))game.BuyShopOffer(offer.offerId);
            GUI.enabled=old&&offer.stock>0&&!barterUsed&&game.Inventory.FindOwned(game.ServiceSelectedEquipment)!=null;
            if(canBarter)
                if(GUI.Button(new Rect(rect.x+10,rect.yMax-42,rect.width-20,34),barterUsed?"현물 교환 사용 완료":"선택한 장비로 현물 교환",compactButton))game.BuyShopOffer(offer.offerId,true);
            GUI.enabled=old;
        }
        string ContentDescription(string id,string kind)
        {
            if(kind=="heal")return "체력 "+(game.BackendCatalog?.economy?.healAmount??0)+" 회복\n구매 즉시 사용 · 최대 체력을 넘지 않습니다.";
            if(kind=="accessory")return AccessoryLootTable.Find(id)?.Description??"";
            var weapon=WeaponLootTable.Find(id);if(weapon==null)return "";
            return WeaponActionCatalog.Get(weapon).Description+"\n기본 공격력 "+weapon.AttackPower.ToString("0.#")+
                (weapon.AttackType==WeaponAttackType.Ranged?" · 탄창 "+weapon.MagazineSize+"\n장전 "+weapon.ReloadSeconds.ToString("0.#")+"초":" · "+MeleeComboCatalog.Get(weapon).LightSteps.Length+"연속 콤보");
        }
        void DrawOwnedEquipmentList(Rect rect,bool sell)
        {
            GUI.Label(new Rect(rect.x,rect.y,rect.width,28),sell?"판매 / 교환할 장비":"대상 무기",compactStyle);
            var items=new System.Collections.Generic.List<OwnedEquipment>();foreach(var item in game.Inventory.AllEquipment())if(sell||item is OwnedWeapon)items.Add(item);
            var view=new Rect(rect.x,rect.y+36,rect.width,rect.height-36);
            serviceScroll=GUI.BeginScrollView(view,serviceScroll,new Rect(0,0,view.width-22,items.Count*72));
            for(int i=0;i<items.Count;i++)
            {
                var item=items[i];var cell=new Rect(0,i*72,view.width-22,66);GUI.Box(cell,"",game.ServiceSelectedEquipment==item.Identity?selectedSlotStyle:slotStyle);
                if(GUI.Button(new Rect(8,cell.y+5,cell.width-16,30),item.Name,compactButton))game.ServiceSelectedEquipment=item.Identity;
                if(sell){bool old=GUI.enabled;GUI.enabled=old&&!item.IsStartingEquipment&&game.Inventory.CanDisassemble(item.Identity);if(GUI.Button(new Rect(8,cell.y+35,cell.width-16,25),item.IsStartingEquipment?"시작 장비 · 판매 불가":"판매 · "+game.SellPrice(item)+" 골드",compactButton))game.SellShopItem(item.Identity);GUI.enabled=old;}
            }
            GUI.EndScrollView();
        }
        void DrawFieldGrowth(Rect rect)
        {
            DrawOwnedEquipmentList(new Rect(rect.x,rect.y,270,rect.height),false);
            var area=new Rect(rect.x+294,rect.y,rect.width-294,rect.height);
            var weapon=game.Inventory.FindOwned(game.ServiceSelectedEquipment) as OwnedWeapon;
            if(game.OpenService.Used){GUI.Label(area,"이 장소는 이미 사용했습니다.",compactStyle);return;}
            if(weapon==null){GUI.Label(area,"변화를 새길 무기를 고르세요.",compactStyle);return;}
            GUI.Label(new Rect(area.x,area.y,area.width,50),weapon.Name+" · 사본 한 개에만 적용",bodyStyle);
            if(game.OpenService.Kind==FieldServiceKind.ShapeAnvil)
            {
                var choices=weapon.Definition.AttackType==WeaponAttackType.Melee?new[]{EquipmentMutation.LongFinisher,EquipmentMutation.WideFinisher}:new[]{EquipmentMutation.Ricochet,EquipmentMutation.AnchorRound};
                for(int i=0;i<choices.Length;i++)
                {var m=choices[i];var box=new Rect(area.x,area.y+64+i*156,area.width,140);GUI.Box(box,"",slotStyle);GUI.Label(new Rect(box.x+18,box.y+14,box.width-36,58),EquipmentMutationRules.Name(m)+"\n"+EquipmentMutationRules.Summary(m),compactStyle);bool old=GUI.enabled;GUI.enabled=old&&EquipmentMutationRules.Compatible(weapon,m);if(GUI.Button(new Rect(box.x+18,box.y+88,box.width-36,38),"이 변형 선택",compactButton))game.ApplyFieldMutation(m);GUI.enabled=old;}
                GUI.Label(new Rect(area.x,area.y+395,area.width,60),"한 번만 선택 · 다른 변형과 함께 적용 불가\n무기의 전용 특수행동은 유지됩니다.",compactStyle);
            }
            else
            {
                int row=0;foreach(EquipmentOath oath in Enum.GetValues(typeof(EquipmentOath)))
                {if(!game.CanBeginOath(weapon,oath))continue;var box=new Rect(area.x,area.y+60+row++*126,area.width,118);GUI.Box(box,"",slotStyle);GUI.Label(new Rect(box.x+12,box.y+8,box.width-150,24),EquipmentMutationRules.OathName(oath),compactStyle);GUI.Label(new Rect(box.x+12,box.y+36,box.width-150,74),EquipmentMutationRules.OathCondition(oath)+"\n"+EquipmentMutationRules.OathResult(oath),talentText);if(GUI.Button(new Rect(box.xMax-126,box.y+42,110,38),"서약",compactButton))game.AcceptEquipmentOath(oath);}
                if(row==0)GUI.Label(new Rect(area.x,area.y+66,area.width,80),"진행 중인 서약이 있거나 이 장비의 조건을 이미 선택했습니다.",compactStyle);
            }
        }
        void DrawExchange(Rect rect)
        {
            GUI.Label(new Rect(rect.x,rect.y,302,30),game.OpenService.Used?"교환 완료":"장신구 2개 → 유물 1개",compactStyle);
            var list=new Rect(rect.x,rect.y+38,314,rect.height-38);
            exchangeRecipeScroll=GUI.BeginScrollView(list,exchangeRecipeScroll,new Rect(0,0,292,game.ExchangeRecipes.Count*142));
            for(int i=0;i<game.ExchangeRecipes.Count;i++)
            {
                var recipe=game.ExchangeRecipes[i];var box=new Rect(0,i*142,292,132);
                bool selected=game.PendingExchangeRecipe==recipe.Id;
                GUI.Box(box,"",selected?selectedSlotStyle:slotStyle);
                DrawContentIcon(new Rect(14,box.y+12,58,58),recipe.OutputId,"accessory");
                GUI.Label(new Rect(86,box.y+16,192,50),WrapPanelText(ContentName(recipe.OutputId,"accessory"),192),compactStyle);
                DrawContentIcon(new Rect(16,box.y+86,32,32),recipe.InputIds[0],"accessory");
                GUI.Label(new Rect(51,box.y+88,20,28),"+",compactCenter);
                DrawContentIcon(new Rect(74,box.y+86,32,32),recipe.InputIds[1],"accessory");
                if(GUI.Button(new Rect(126,box.y+84,150,34),selected?"선택 중":"효과 · 재료",compactButton))game.PreviewExchange(recipe.Id);
            }
            GUI.EndScrollView();
            var preview=new Rect(rect.x+330,rect.y,rect.width-330,rect.height);GUI.Box(preview,"",slotStyle);
            var choice=game.FindExchangeRecipe(game.PendingExchangeRecipe);
            if(choice==null)
            {GUI.Label(new Rect(preview.x+24,preview.y+24,preview.width-48,60),"교환할 유물을 고르면 필요한 재료와 효과를 비교할 수 있습니다.",compactStyle);return;}
            if(exchangeDetailRecipe!=choice.Id){exchangeDetailRecipe=choice.Id;exchangeDetailScroll=Vector2.zero;}
            var ingredients=game.ExchangeIngredients(choice.Id);var output=AccessoryLootTable.Find(choice.OutputId);
            if(output==null)return;
            var view=new Rect(preview.x+16,preview.y+16,preview.width-32,preview.height-108);
            float contentWidth=view.width-22,columnWidth=(contentWidth-20)*.5f;
            string effect=WrapPanelText(EquipmentUpgradeRules.AccessoryDescription(output,0,game.Inventory),contentWidth);
            float effectHeight=compactStyle.CalcHeight(new GUIContent(effect),contentWidth);
            float materialY=100+effectHeight;
            float materialHeight=0;
            for(int i=0;i<choice.InputIds.Count;i++)
            {
                var definition=AccessoryLootTable.Find(choice.InputIds[i]);if(definition==null)continue;
                string description=WrapPanelText(EquipmentUpgradeRules.AccessoryDescription(definition,0,game.Inventory),columnWidth);
                materialHeight=Mathf.Max(materialHeight,compactStyle.CalcHeight(new GUIContent(description),columnWidth));
            }
            exchangeDetailScroll=GUI.BeginScrollView(view,exchangeDetailScroll,new Rect(0,0,contentWidth,Mathf.Max(view.height,materialY+128+materialHeight)));
            DrawContentIcon(new Rect(0,0,72,72),choice.OutputId,"accessory");
            GUI.Label(new Rect(88,0,contentWidth-88,26),"획득할 유물",talentText);
            GUI.Label(new Rect(88,28,contentWidth-88,48),WrapPanelText(output.Name,contentWidth-88),compactStyle);
            GUI.Label(new Rect(0,84,contentWidth,effectHeight),effect,compactStyle);
            GUI.Label(new Rect(0,materialY,contentWidth,28),"소비할 장신구",compactStyle);
            for(int i=0;i<choice.InputIds.Count;i++)
            {
                var definition=AccessoryLootTable.Find(choice.InputIds[i]);if(definition==null)continue;
                float x=i*(columnWidth+20);int owned=0;
                foreach(var item in game.Inventory.OwnedAccessories)if(item.Definition.Id==definition.Id)owned++;
                DrawContentIcon(new Rect(x,materialY+38,48,48),definition.Id,"accessory");
                GUI.Label(new Rect(x+60,materialY+36,columnWidth-60,46),WrapPanelText(definition.Name,columnWidth-60),compactStyle);
                GUI.Label(new Rect(x,materialY+88,columnWidth,24),owned>0?"보유 "+owned+"개 · 1개 소비":"부족 · 1개 필요",talentText);
                string description=WrapPanelText(EquipmentUpgradeRules.AccessoryDescription(definition,0,game.Inventory),columnWidth);
                GUI.Label(new Rect(x,materialY+116,columnWidth,materialHeight),description,compactStyle);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(preview.x+16,preview.yMax-82,preview.width-32,30),"소비한 장신구의 효과와 세트 기여는 사라집니다.",talentText);
            bool old=GUI.enabled;GUI.enabled=old&&ingredients.Length==2&&!game.OpenService.Used;
            string action=game.OpenService.Used?"이 제단은 사용했습니다":ingredients.Length==2?"장신구 두 개 소비 · 교환":"재료 부족";
            if(GUI.Button(new Rect(preview.x+16,preview.yMax-46,preview.width-32,34),action,compactButton))game.ConfirmExchange();GUI.enabled=old;
        }
        string ContentName(string id,string kind)=>kind=="heal"?"회복 물약":kind=="weapon"?WeaponLootTable.Find(id)?.Name??id:AccessoryLootTable.Find(id)?.Name??id;
        void DrawContentIcon(Rect rect,string id,string kind)
        {
            if(kind=="heal"){ItemIconLibrary.DrawGUI(rect,InventoryIconFactory.Get("sapphire_flask",Color.white,InventoryIconKind.Accessory));return;}
            var weapon=kind=="weapon"?WeaponLootTable.Find(id):null;var accessory=kind=="accessory"?AccessoryLootTable.Find(id):null;
            if(weapon!=null||accessory!=null)ItemIconLibrary.DrawGUI(rect,InventoryIconFactory.Get(id,weapon!=null?weapon.Color:accessory.Color,weapon!=null?InventoryIconKind.Weapon:InventoryIconKind.Accessory));
        }
        static string ShortEffect(string description)
        {var lines=description.Split('\n');return lines.Length>1?lines[0]+"\n"+lines[1]:description;}
    }
}
