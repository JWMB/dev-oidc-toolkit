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
            return HtmlElementRendererBase.ResolveRenderer(p, value).Render(value);
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
        public Action<Dictionary<string, string?>>? ModifyAttributes { get; set; } // TODO: HTML-specific, should be in a separate implementation
    }

    public interface IElementRenderer
    {
        string Render(object? value);
    }

    public class HtmlUnhandledRenderer(PropertyInfoWrapper p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value) =>
            $"""<div>{p.PropertyInfo.PropertyType.Name} {string.Join(", ", p.PropertyInfo.PropertyType.GenericTypeArguments.Select(o => o.Name))}</div> """;
    }

    public class HtmlInputStringRenderer(PropertyInfoWrapper p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value)
        {
            if (p.Syntax == "Json") // TODO: maybe also if MaxLength is large enough?
                return RenderElement("textarea", [], value?.ToString());
            else
                return RenderElement("input",
                    GetTuplesAsDict([("type", p.Secret ? "password" : "text"), ("value", value?.ToString())]));
            // return RenderWithX("input", [("type", p.Secret ? "password" : "text"), ("value", value?.ToString())]);
        }
    }

    public class HtmlInputNumberRenderer(PropertyInfoWrapper p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value) =>
            RenderElement("input",
                GetTuplesAsDict([("type", "number"), ("value", value?.ToString())]));
        //[("type", "number"), ("value", value?.ToString())]);
    }
    public class HtmlInputDateRenderer(PropertyInfoWrapper p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value) =>
            RenderElement("input",
                GetTuplesAsDict([("type", "date"), ("value", value?.ToString())]));
        // RenderWithX("input", [("type", "date"), ("value", value?.ToString())]);
    }
    public class HtmlInputBoolRenderer(PropertyInfoWrapper p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value) =>
            RenderElement("input",
                GetTuplesAsDict([("type", "checkbox"), ("checked", (bool?)value == true ? emptyAttr : null)]));
        // RenderWithX("input", [("type", "checkbox"), ("checked", (bool?)value == true ? emptyAttr : null)]);
    }

    public abstract class HtmlElementRendererBase : IElementRenderer
    {
        private readonly PropertyInfoWrapper p;

        public HtmlElementRendererBase(PropertyInfoWrapper p)
        {
            this.p = p;
        }

        protected const string emptyAttr = "_EMPTY_ATTR_";

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

        protected string RenderAttributes(IEnumerable<KeyValuePair<string, string?>> attrs)
            => string.Join(" ", attrs.Select(o => o.Value == emptyAttr ? o.Key : $"{o.Key}=\"{o.Value}\""));
        protected IEnumerable<KeyValuePair<string, string?>> GetTuplesAsDict(IEnumerable<(string, string?)> items)
            => items.Select(o => KeyValuePair.Create(o.Item1, o.Item2));

        protected string RenderElement(string elementName, IEnumerable<KeyValuePair<string, string?>> attrs, string? innerHtml = null, bool addAutoAttributes = true)
        {
            var attrsStr = RenderAttributes(addAutoAttributes
                ? GetAttributes().Concat(attrs)
                : attrs);
            return innerHtml == null
                ? $"""<{elementName} {attrsStr}/>"""
                : $"""<{elementName} {attrsStr}>{innerHtml}</{elementName}>""";
        }

        public abstract string Render(object? value);

        public static IElementRenderer ResolveRenderer(PropertyInfoWrapper p, object? value)
        {
            if (p.Type == typeof(bool))
                return new HtmlInputBoolRenderer(p);
            else if (p.Type == typeof(string))
                return new HtmlInputStringRenderer(p);
            else if (new[] { typeof(long), typeof(ulong), typeof(int), typeof(uint), typeof(short), typeof(ushort), typeof(byte) }.Contains(p.Type))
                return new HtmlInputNumberRenderer(p);
            else if (new[] { typeof(DateTimeOffset), typeof(DateTime) }.Contains(p.Type))
                return new HtmlInputDateRenderer(p);
            else
                return new HtmlUnhandledRenderer(p);
        }
    }
}
