namespace DevOidcToolkit
{
    public interface IElementRenderer
    {
        string Render(object? value);
    }

    public abstract class HtmlElementRendererBase : IElementRenderer
    {
        protected readonly PropertyWrapperBase p;

        public HtmlElementRendererBase(PropertyWrapperBase p)
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
                ["step"] = p.Step,
                ["autocomplete"] = p.Autocomplete
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
                ? $"""<{elementName} {attrsStr} />"""
                : $"""<{elementName} {attrsStr}>{innerHtml}</{elementName}>""";
        }

        public abstract string Render(object? value);

        public static IElementRenderer ResolveRenderer(PropertyWrapperBase p, object? value)
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

    public class HtmlUnhandledRenderer(PropertyWrapperBase p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value) =>
            $"""<div>{p.Type.Name} {string.Join(", ", p.Type.GenericTypeArguments.Select(o => o.Name))}</div> """;
    }

    public class HtmlInputStringRenderer(PropertyWrapperBase p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value)
        {
            if (p.Syntax == "Json") // TODO: maybe also if MaxLength is large enough?
                return RenderElement("textarea", [], value?.ToString() ?? "");
            else
                return RenderElement("input",
                    GetTuplesAsDict([("type", p.Secret ? "password" : "text"), ("value", value?.ToString())]));
        }
    }

    public class HtmlInputNumberRenderer(PropertyWrapperBase p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value) =>
            RenderElement("input",
                GetTuplesAsDict([("type", "number"), ("value", value?.ToString() ?? (p.Nullable ? null : p.DefaultValue?.ToString() ?? "0"))]));
    }
    public class HtmlInputDateRenderer(PropertyWrapperBase p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value) =>
            RenderElement("input",
                GetTuplesAsDict([("type", "date"), ("value", value?.ToString())]));
    }
    public class HtmlInputBoolRenderer(PropertyWrapperBase p) : HtmlElementRendererBase(p)
    {
        public override string Render(object? value) =>
            RenderElement("input",
                GetTuplesAsDict([("type", "checkbox"), ("checked", (bool?)value == true ? emptyAttr : null)]));
    }

}
