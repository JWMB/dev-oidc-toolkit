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

            return HtmlElementRendererBase.ResolveRenderer(p, value)
                .Render(value);
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
        //public Action<Dictionary<string, string?>>? ModifyAttributes { get; set; } // TODO: HTML-specific, should be in a separate implementation
    }
}
