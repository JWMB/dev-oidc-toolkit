using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.AspNetCore.Identity;

namespace DevOidcToolkit
{
    public class PropertyInfoWrapper
    {
        public required string Name { get; set; }
        public required Type Type { get; set; }
        public required PropertyInfo PropertyInfo { get; set; }

        public object? DefaultValue { get; set; }
        public bool Nullable { get; set; }
        public bool Required { get; set; }
        public bool Secret { get; set; }
        public bool ReadOnly { get; set; }
        public string? Syntax { get; set; }

        [StringSyntax(StringSyntaxAttribute.Regex)]
        public string? Pattern { get; set; }
        public string? Min { get; set; }
        public string? Max { get; set; }
        public string? Step { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }

        public override string ToString() => FormGenerator.Render(null, this);

        public static PropertyInfoWrapper From(PropertyInfo p)
        {
            return new PropertyInfoWrapper
            {
                Name = p.Name,
                PropertyInfo = p,
                Required = p.CustomAttributes.Where(o =>
                    (new[] { typeof(PersonalDataAttribute), typeof(System.ComponentModel.DataAnnotations.RequiredAttribute)
                    }).Contains(o.AttributeType)).Any(),
                Nullable = System.Nullable.GetUnderlyingType(p.PropertyType) != null,
                Type = System.Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType,
                DefaultValue = p.GetCustomAttribute<DefaultValueAttribute>()?.Value,
                Syntax = p.GetCustomAttribute<System.Diagnostics.CodeAnalysis.StringSyntaxAttribute>()?.Syntax,

                Step = null, // TODO: no commonly agreed-on attribute, right?
                Secret = false, // TODO: no commonly agreed-on attribute, right?
                ReadOnly = !p.CanWrite, // TODO: also some attribute, no?

                // Validation:
                Pattern = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.RegularExpressionAttribute>()?.Pattern,
                MinLength = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.MinLengthAttribute>()?.Length,
                MaxLength = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.MaxLengthAttribute>()?.Length,
                // TODO: inclusive or not
                Min = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>()?.Minimum?.ToString(), // TODO: globalization etc
                Max = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>()?.Maximum?.ToString(), // TODO: globalization etc
            };
        }
    }
}