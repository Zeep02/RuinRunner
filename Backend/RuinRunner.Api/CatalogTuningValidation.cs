using System.Text.Json;
using System.Text.RegularExpressions;
using RuinRunner.Backend;

namespace RuinRunner.Api;

public static class CatalogTuningValidation
{
    public static void Validate(Catalog catalog)
    {
        if (catalog.AccessoryTuningVersion == 0)
            return; // Historical published catalogs remain byte-for-byte compatible.
        Wire.Require(catalog.AccessoryTuningVersion == AccessoryTuningSchema.Version, "catalog_invalid", "지원하지 않는 장신구 수치 버전입니다.");
        var accessories = (catalog.Items ?? []).Where(x => x.Kind == "accessory").ToArray();
        Wire.Require(accessories.Length >= 30, "catalog_invalid", "장신구 수치가 완전하지 않습니다.");
        foreach (var item in accessories)
            ValidateValues(item.Id, item.Stats, item.Description);
        var sets = catalog.ItemSets ?? [];
        Wire.Require(sets.Length == 8 && sets.Select(x => x.Id + ":" + x.Pieces).Distinct().Count() == 8, "catalog_invalid", "세트 수치가 완전하지 않습니다.");
        foreach (var set in sets)
            ValidateValues("set." + set.Id + "." + set.Pieces, set.Stats, set.Description);
    }
    static void ValidateValues(string id, JsonElement stats, string description)
    {
        Dictionary<string, AccessoryValueRule> rules;
        try
        {
            rules = AccessoryTuningSchema.Rules(id);
        }
        catch (ArgumentException)
        {
            throw new ApiFailure(400, "catalog_invalid", "알 수 없는 장신구·세트 식별자입니다.");
        }
        Wire.Require(stats.ValueKind == JsonValueKind.Object, "catalog_invalid", "장신구 수치 형식을 확인하세요.");
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var property in stats.EnumerateObject())
        {
            if (property.Name == "effect")
            {
                Wire.Require(property.Value.ValueKind == JsonValueKind.Object, "catalog_invalid", "발동 수치 형식을 확인하세요.");
                foreach (var field in property.Value.EnumerateObject())
                    Read("effect." + field.Name, field.Value);
            }
            else
                Read(property.Name, property.Value);
        }
        Wire.Require(values.Count == rules.Count, "catalog_invalid", "장신구 수치가 빠져 있습니다.");
        if (id == "trail_spindle")
            Wire.Require(values["effect.requiredMarkCount"] <= values["effect.markCount"], "catalog_invalid", "실현 불가능한 표식 조건입니다.");
        var remaining = Regex.Replace(description ?? "", @"\{([A-Za-z0-9_.]+)(:p)?\}", match =>
        {
            Wire.Require(values.ContainsKey(match.Groups[1].Value), "catalog_invalid", "설명이 알 수 없는 수치를 참조합니다.");
            return "";
        });
        Wire.Require(!remaining.Contains('{') && !remaining.Contains('}'), "catalog_invalid", "설명 수치 표기를 확인하세요.");
        void Read(string key, JsonElement field)
        {
            Wire.Require(rules.TryGetValue(key, out var rule) && field.ValueKind == JsonValueKind.Number, "catalog_invalid", "알 수 없거나 숫자가 아닌 수치가 있습니다.");
            Wire.Require(field.TryGetDouble(out var value) && double.IsFinite(value) && value >= rule.Minimum && value <= rule.Maximum && (!rule.Integer || value == Math.Truncate(value)), "catalog_invalid", "장신구 수치 범위를 확인하세요.");
            Wire.Require(values.TryAdd(key, value), "catalog_invalid", "중복 수치가 있습니다.");
        }
    }
}
