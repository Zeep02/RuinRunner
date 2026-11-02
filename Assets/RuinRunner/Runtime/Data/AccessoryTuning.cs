using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using RuinRunner.Backend;

namespace RuinRunner
{
    /// <summary>Immutable values owned by a definition/run, never a mutable current-catalog lookup.</summary>
    public sealed class AccessoryTuning
    {
        readonly Dictionary<string,float> values;
        static readonly Dictionary<string,AccessoryTuning> legacy=new Dictionary<string,AccessoryTuning>(StringComparer.Ordinal);
        static readonly Regex placeholder=new Regex(@"\{([A-Za-z0-9_.]+)(:p)?\}");
        AccessoryTuning(Dictionary<string,float> source) { values=new Dictionary<string,float>(source,StringComparer.Ordinal); }
        public float Number(string key) => values.TryGetValue(key,out var value) ? value : throw new ArgumentException("Unknown item tuning field: "+key);
        public int Integer(string key) => checked((int)Number(key));
        public float Effect(string key) => Number("effect."+key);
        public int EffectCount(string key) => Integer("effect."+key);
        public static AccessoryTuning Legacy(string id)
        {
            if(legacy.TryGetValue(id,out var existing))return existing;
            var values=new Dictionary<string,float>(StringComparer.Ordinal);
            foreach(var rule in AccessoryTuningSchema.Rules(id))values.Add(rule.Key,rule.Value.Legacy);
            var result=new AccessoryTuning(values); legacy.Add(id,result); return result;
        }
        public static AccessoryTuning Parse(string id,JObject stats,int version)
        {
            if(version==0)return Legacy(id); // Published v1 never consults values left from a newer binding.
            if(version!=AccessoryTuningSchema.Version||stats==null)throw new ArgumentException("Unsupported item tuning schema");
            var rules=AccessoryTuningSchema.Rules(id); var values=new Dictionary<string,float>(StringComparer.Ordinal);
            foreach(var property in stats.Properties())
            {
                if(property.Name=="effect")
                {
                    if(!(property.Value is JObject effect))throw new ArgumentException("Item effect tuning must be an object");
                    foreach(var field in effect.Properties())Read("effect."+field.Name,field.Value);
                }
                else Read(property.Name,property.Value);
            }
            if(values.Count!=rules.Count)throw new ArgumentException("Incomplete item tuning: "+id);
            if(id=="trail_spindle"&&values["effect.requiredMarkCount"]>values["effect.markCount"])throw new ArgumentException("Impossible trail mark requirement");
            return new AccessoryTuning(values);
            void Read(string key,JToken token)
            {
                if(!rules.TryGetValue(key,out var rule)||token.Type!=JTokenType.Integer&&token.Type!=JTokenType.Float)
                    throw new ArgumentException("Unknown or non-numeric item tuning: "+id+"."+key);
                double number=token.Value<double>();
                if(double.IsNaN(number)||double.IsInfinity(number)||number<rule.Minimum||number>rule.Maximum||rule.Integer&&number!=Math.Truncate(number))
                    throw new ArgumentException("Out-of-range item tuning: "+id+"."+key);
                values.Add(key,(float)number);
            }
        }
        public string Format(string template)
        {
            var description=placeholder.Replace(template??"",match=>
            {
                float value=Number(match.Groups[1].Value);
                if(match.Groups[2].Success)value*=100;
                return value.ToString("0.###",CultureInfo.InvariantCulture);
            });
            if(description.IndexOf('{')>=0||description.IndexOf('}')>=0)throw new ArgumentException("Invalid item description placeholder");
            return description;
        }
    }
}
