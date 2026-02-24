using System.ComponentModel;
using System.Reflection;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DevOidcToolkit
{
    public class FormGenerator
    {
        public class PropertyInfoAccessor(PropertyInfo p)
        {
            public string Name => p.Name;
            public PropertyInfo PropertyInfo => p;
            public bool IsRequired => p.CustomAttributes.Where(o =>
                        new[] { typeof(PersonalDataAttribute), typeof(System.ComponentModel.DataAnnotations.RequiredAttribute)
                        }.Contains(o.AttributeType)).Any();
            public bool IsNullable => Nullable.GetUnderlyingType(p.PropertyType) != null;
            public Type Type => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            public object? DefaultValue => p.GetCustomAttribute<DefaultValueAttribute>()?.Value;
            public string? Syntax => p.GetCustomAttribute<System.Diagnostics.CodeAnalysis.StringSyntaxAttribute>()?.Syntax;
        }

        public static void UpdateModel<T>(T from, T to)
        {
            // TODO: how can we update the existing without manually setting all properties?
            foreach (var p in FormGenerator.AnalyzeProperties<T>())
            {
                var prev = p.PropertyInfo.GetValue(to);
                var next = p.PropertyInfo.GetValue(from);
                var update = false;
                if (prev == null)
                    update = next != null;
                else if (next == null)
                    update = true;
                else
                    update = prev.Equals(next) == false;

                if (update)
                    p.PropertyInfo.SetValue(to, next);
            }
        }

        public static List<PropertyInfoAccessor> AnalyzeProperties<T>()
        {
            return typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .Select((p, i) => new
                {
                    OriginalIndex = i,
                    PEx = new PropertyInfoAccessor(p),
                })
                .OrderByDescending(o => o.PEx.IsRequired)
                .ThenBy(o => o.OriginalIndex)
                .Select(o => o.PEx).ToList();
        }

        public static bool IsModelValidSuperStrange<T>(ModelStateDictionary modelState, T model)
        {
            if (modelState.IsValid)
                return true;

            // TODO: this can't be right, all this because checkboxes yield "on" instead of booleans?
            var invalids = modelState.Where(o => o.Value != null && o.Value.ValidationState != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Valid).ToList();

            foreach (var item in invalids)
            {
                if (item.Value == null)
                    continue;
                var errors = item.Value!.Errors;
                var p = AnalyzeProperties<T>().SingleOrDefault(o => o.Name == item.Key);
                if (p != null)
                {
                    if (p.Type == typeof(bool))
                    {
                        var onErr = errors.SingleOrDefault(o => o.ErrorMessage.Contains($"The value 'on' is not valid for"));
                        if (onErr != null)
                        {
                            var bVal = item.Value?.AttemptedValue == "on";
                            p.PropertyInfo.SetValue(model, bVal);
                            modelState.SetModelValue(item.Key, bVal, $"{bVal}".ToLower());
                            errors.Remove(onErr);
                        }
                    }
                }
                //if (errors.Any() == false) { }
            }

            return invalids.Any(o => o.Value == null || o.Value.Errors.Any()) == false; // !ModelState.IsValid
        }

        public static string Render<T>(T? model = default)
        {
            var strs = new List<string>();
            //var sb = new StringBuilder();
            foreach (var p in AnalyzeProperties<T>())
            {
                object? value = null;
                if (model != null)
                    value = p.PropertyInfo.GetValue(model); //?.ToString();
                if (value == null && p.IsRequired && p.DefaultValue != null)
                    value = p.DefaultValue;

                var isSecret = p.Name.ToLower().Contains("secret"); // hm, expected an attribute on these properties...

                var strValue = value?.ToString();
                strs.Add($"""<label for="{p.Name}">{(p.IsRequired ? "* " : "")}{p.Name}</label>""");

                if (p.Type == typeof(bool))
                    strs.Add($"""<input type="checkbox" name="{p.Name}" {((bool?)value == true ? "checked" : "")} />""");
                else if (p.Type == typeof(string))
                {
                    if (p.Syntax == "Json")
                        strs.Add($"""<textarea name="{p.Name}">{strValue}</textarea>""");
                    else
                        strs.Add($"""<input type="{(isSecret ? "password" : "text")}" name="{p.Name}" value="{strValue}" />""");
                }
                else if (new[] { typeof(long), typeof(ulong), typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(byte) }.Contains(p.Type))
                    strs.Add($"""<input type="number" name="{p.Name}" value="{strValue}" />""");
                else if (new[] { typeof(DateTimeOffset), typeof(DateTime) }.Contains(p.Type))
                    strs.Add($"""<input type="date" name="{p.Name}" value="{strValue}" />""");
                else
                    strs.Add($"""<div>{p.PropertyInfo.PropertyType.Name} {string.Join(", ", p.PropertyInfo.PropertyType.GenericTypeArguments.Select(o => o.Name))}</div>""");
            }

            //p.PropertyType switch
            //{
            //_ => <input type= "text" name= @p.Name />
            //}

            return string.Join("\n", strs);
        }
    }
}
