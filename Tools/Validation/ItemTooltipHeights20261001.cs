using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using RuinRunner;
using Newtonsoft.Json.Linq;
public static class ItemTooltipHeights20261001
{
    public static string Main()
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
        var style = (GUIStyle)typeof(PrototypeHud).GetField("detailStyle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
        if (style == null) throw new Exception("Allow actual HUD to initialize fonts first");
        var rows = new JArray();
        foreach (var item in AccessoryLootTable.All)
        {
            string set = ItemSetCatalog.GetName(item.SetType) + "  3/3";
            foreach (var bonus in ItemSetCatalog.Bonuses) if (bonus.SetType == item.SetType) set += "\n● " + bonus.RequiredPieces + "세트: " + bonus.Description;
            float description = style.CalcHeight(new GUIContent(item.Description), 392);
            float height = 150 + description + style.CalcHeight(new GUIContent(set), 392);
            rows.Add(new JObject { { "id", item.Id }, { "height", height }, { "fits720", height <= 704 }, { "descriptionHeight", description } });
        }
        File.WriteAllText("ArtDirection/Review-20261001/tooltip-height-qa.json", rows.ToString()); return rows.ToString();
    }
}
