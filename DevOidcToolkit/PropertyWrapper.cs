using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.AspNetCore.Identity;

namespace DevOidcToolkit
{
    public abstract class PropertyWrapperBase
    {
        public required Type Type { get; set; }
        public required string Name { get; set; }
        public string? DisplayName { get; set; }

        public abstract object? GetValue(object obj);
        public abstract void SetValue(object obj, object? value);

        public bool Hidden { get; set; }
        public object? DefaultValue { get; set; }
        public bool Nullable { get; set; }
        public bool Required { get; set; }
        public bool Secret { get; set; }
        public bool ReadOnly { get; set; }
        public string? Autocomplete { get; set; }
        public string? Syntax { get; set; }

        [StringSyntax(StringSyntaxAttribute.Regex)]
        public string? Pattern { get; set; }
        public string? Min { get; set; }
        public string? Max { get; set; }
        public string? Step { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
    }

    public class PropertyProxy : PropertyWrapperBase
    {
        private readonly PropertyWrapperBase _other;
        private readonly Func<object?, object?>? _transformSetValue;
        private readonly Func<object?, object?>? _transformGetValue;

        public PropertyProxy(PropertyWrapperBase other, Func<object?, object?>? transformSetValue = null, Func<object?, object?>? transformGetValue = null)
        {
            _other = other;
            _transformSetValue = transformSetValue;
            _transformGetValue = transformGetValue;
        }
        public override object? GetValue(object obj) 
            => _transformGetValue == null ? _other.GetValue(obj) : _transformGetValue(_other.GetValue(obj));

        public override void SetValue(object obj, object? value)
            => _other.SetValue(obj, _transformSetValue == null ? value : _transformSetValue(value));
    }

    public class PropertyWrapper : PropertyWrapperBase
    {
        public required PropertyInfo PropertyInfo { get; set; }

        public Action<object, object>? SetUnderlying { get; set; } 

        public override string ToString() => FormGenerator.Render((object?)null, this);

        public static PropertyWrapper From(PropertyInfo p)
        {
            return new PropertyWrapper
            {
                Name = p.Name,
                PropertyInfo = p,
                Required = p.CustomAttributes.Where(o =>
                    (new[] { 
                        typeof(PersonalDataAttribute),
                        typeof(System.ComponentModel.DataAnnotations.RequiredAttribute),
                        typeof(System.Runtime.CompilerServices.RequiredMemberAttribute)
                    }).Contains(o.AttributeType)).Any(),
                Nullable = System.Nullable.GetUnderlyingType(p.PropertyType) != null,
                Type = System.Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType,
                DefaultValue = p.GetCustomAttribute<DefaultValueAttribute>()?.Value,
                Syntax = p.GetCustomAttribute<StringSyntaxAttribute>()?.Syntax,

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

        public override object? GetValue(object obj) => PropertyInfo.GetValue(obj, null);

        public override void SetValue(object obj, object? value) => PropertyInfo.SetValue(obj, value, null);
    }
}