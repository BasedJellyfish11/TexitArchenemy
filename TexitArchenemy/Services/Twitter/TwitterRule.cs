#nullable enable
using System;
using System.Text.Json.Serialization;

namespace TexitArchenemy.Services.Twitter
{
    public struct TwitterRule
    {
        [JsonInclude]
        public string value;
        [JsonInclude]
        public string tag;

        public TwitterRule(string value, string tag)
        {
            this.value = value;
            this.tag = tag;
        }
        
        public override bool Equals(object? o)
        {
            if (o is not TwitterRule rule)
                return false;

            return value == rule.value;
        }

       
    }
    
}