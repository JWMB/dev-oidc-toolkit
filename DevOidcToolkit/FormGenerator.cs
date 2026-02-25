using System.ComponentModel;
using System.Reflection;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DevOidcToolkit
{
    public class FormGenerator
    {
        public static void UpdateModel<T>(T from, T to)
        {
            // TODO: how can we update the existing without manually setting all properties?
            foreach (var p in AnalyzeProperties<T>())
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

        public static List<PropertyInfoWrapper> AnalyzeProperties<T>(IEnumerable<KeyValuePair<string, Action<PropertyInfoWrapper>>>? overrides = null)
        {
            return typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .Select((p, i) => new
                {
                    OriginalIndex = i,
                    PEx = PropertyInfoWrapper.From(p),
                })
                .Select(o =>
                {
                    var px = o.PEx;
                    var found = overrides?.SingleOrDefault(p => p.Key == o.PEx.Name);
                    if (found.HasValue && found.Value.Value != null)
                        found.Value.Value(o.PEx);
                    return new { o.OriginalIndex, o.PEx };
                })
                .OrderByDescending(o => o.PEx.Required)
                .ThenBy(o => o.OriginalIndex)
                .Select(o => o.PEx).Cast<PropertyInfoWrapper>().ToList();
        }

        public static bool IsModelValidSuperStrange<T>(ModelStateDictionary modelState, T model)
        {
            if (modelState.IsValid)
                return true;

            // TODO: this can't be right, all this because checkboxes yield "on" instead of booleans?
            var invalids = modelState.Where(o => o.Value != null && o.Value.ValidationState != ModelValidationState.Valid).ToList();

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

        public static string Render(object? value, PropertyInfoWrapper p, RenderOverride? renderOverride = null)
        {
            if (renderOverride?.Renderer != null)
                return renderOverride.Renderer(p, value);
            return new HtmlElementRendererBase(p).Render(value);
        }

        public static string Render<T>(T? model = default, IEnumerable<RenderOverride>? overrides = null)
        {
            var strs = new List<string>();
            var props = AnalyzeProperties<T>(overrides?.Where(o => o.ModifyInfo != null).Select(o => KeyValuePair.Create(o.PropertyName, o.ModifyInfo!)));
            foreach (var p in props)
            {
                object? value = null;
                if (model != null)
                    value = p.PropertyInfo.GetValue(model); //?.ToString();
                if (value == null && p.Required && p.DefaultValue != null)
                    value = p.DefaultValue;

                strs.Add($"""<label for="{p.Name}">{(p.Required ? "* " : "")}{p.Name}</label> """);
                strs.Add(Render(value, p, overrides?.SingleOrDefault(o => o.PropertyName == p.Name)));
            }

            return string.Join("\n", strs);

        }
    }

    public class RenderOverride
    {
        public required string PropertyName { get; set; }
        public Func<PropertyInfoWrapper, object?, string>? Renderer { get; set; }
        public Action<PropertyInfoWrapper>? ModifyInfo { get; set; }
    }

    public interface IElementRenderer
    {
        string Render(object? value);
    }

    public class HtmlElementRendererBase : IElementRenderer
    {
        private readonly PropertyInfoWrapper p;

        public HtmlElementRendererBase(PropertyInfoWrapper p)
        {
            this.p = p;
        }

        private readonly string emptyAttr = "_EMPTY_ATTR_";

        protected Dictionary<string, string?> GetAttributes()
        {
            return new Dictionary<string, string?>
            {
                ["name"] = p.Name,
                ["required"] = p.Required ? emptyAttr : null,
                ["readonly"] = p.ReadOnly ? emptyAttr : null,
                ["min"] = p.Min,
                ["max"] = p.Max,
                ["minlength"] = p.MinLength.HasValue ? $"{p.MinLength}" : null,
                ["maxlength"] = p.MaxLength.HasValue ? $"{p.MaxLength}" : null,
                ["pattern"] = p.Pattern,
                ["step"] = p.Step
            }.Where(o => o.Value != null)
            .ToDictionary();
        }

        protected string RenderElement(string elementName, IEnumerable<KeyValuePair<string, string?>> attrs, string? innerHtml)
        {
            var attrsStr = string.Join(" ", attrs.Select(o => o.Value == emptyAttr ? o.Key : $"{o.Key}=\"{o.Value}\""));
            return innerHtml == null
                ? $"""<{elementName} {attrsStr}/>"""
                : $"""<{elementName} {attrsStr}>{innerHtml}</{elementName}>""";
        }

        public string Render(object? value)
        {
            var strValue = value?.ToString();
            var element = "input";
            string? innerHtml = null;
            var attrs = new List<(string, string?)>();

            if (p.Type == typeof(bool))
                attrs = [("type", "checkbox"), ("checked", (bool?)value == true ? emptyAttr : null)];
            else if (p.Type == typeof(string))
            {
                if (p.Syntax == "Json")
                {
                    element = "textarea";
                    innerHtml = strValue;
                }
                else
                    attrs = [("type", p.Secret ? "password" : "text"), ("value", strValue)];
            }
            else if (new[] { typeof(long), typeof(ulong), typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(byte) }.Contains(p.Type))
                attrs = [("type", "number"), ("value", strValue)];
            else if (new[] { typeof(DateTimeOffset), typeof(DateTime) }.Contains(p.Type))
                attrs = [("type", "date"), ("value", strValue)];
            else
                return $"""<div>{p.PropertyInfo.PropertyType.Name} {string.Join(", ", p.PropertyInfo.PropertyType.GenericTypeArguments.Select(o => o.Name))}</div> """;

            var mergedAttributes = GetAttributes().Concat(attrs.Select(o => KeyValuePair.Create(o.Item1, o.Item2))).ToDictionary(o => o.Key, o => o.Value);
            return RenderElement(element, mergedAttributes, innerHtml);
        }
    }
}
