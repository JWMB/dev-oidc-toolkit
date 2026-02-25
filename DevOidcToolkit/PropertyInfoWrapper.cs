using System.ComponentModel;
using System.Reflection;

using Microsoft.AspNetCore.Identity;

namespace DevOidcToolkit
{
    //public interface IPropertyInfoAccessor
    //{
    //    string Name { get; }
    //    PropertyInfo PropertyInfo { get; }
    //    Type Type { get; }

    //    object? DefaultValue { get; }
    //    bool IsNullable { get; }
    //    bool IsRequired { get; }
    //    string? Syntax { get; }
    //    bool IsSecret { get; }
    //}

    //public class PropertyInfoAccessor(PropertyInfo p) //: IPropertyInfoAccessor
    //{
    //    public string Name => p.Name;
    //    public PropertyInfo PropertyInfo => p;
    //    public bool IsRequired => p.CustomAttributes.Where(o =>
    //                new[] { typeof(PersonalDataAttribute), typeof(System.ComponentModel.DataAnnotations.RequiredAttribute)
    //                }.Contains(o.AttributeType)).Any();
    //    public bool IsNullable => Nullable.GetUnderlyingType(p.PropertyType) != null;
    //    public Type Type => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
    //    public object? DefaultValue => p.GetCustomAttribute<DefaultValueAttribute>()?.Value;
    //    public string? Syntax => p.GetCustomAttribute<System.Diagnostics.CodeAnalysis.StringSyntaxAttribute>()?.Syntax;

    //    public bool IsSecret => false;
    //}

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
            };
        }

        //public static PropertyInfoWrapper From(PropertyInfoWrapper source)
        //{
        //    return new PropertyInfoWrapper
        //    {
        //        Name = source.Name,
        //        Type = source.Type,
        //        PropertyInfo = source.PropertyInfo,
        //        DefaultValue = source.DefaultValue,
        //        IsNullable = source.IsNullable,
        //        IsRequired = source.IsRequired,
        //        IsSecret = source.IsSecret,
        //        Syntax = source.Syntax,
        //    };
        //}
    }
}