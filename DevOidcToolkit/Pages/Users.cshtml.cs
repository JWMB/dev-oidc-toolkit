namespace DevOidcToolkit.Pages;

using System.ComponentModel;
using System.Reflection;

using DevOidcToolkit.Infrastructure.Database;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

[Authorize]
public class UsersPageModel : PageModel
{
    private readonly UserManager<DevOidcToolkitUser> userManager;

    public UsersPageModel(UserManager<DevOidcToolkitUser> userManager)
    {
        this.userManager = userManager;
    }

    [BindProperty]
    public required DevOidcToolkitUser? Input { get; set; }

    public List<DevOidcToolkitUser> Users { get; set; } = new();

    public record PropertyInfoEx(PropertyInfo PropertyInfo, bool IsRequired, bool IsNullable, Type Type, object? DefaultValue)
    {
        public static PropertyInfoEx From(PropertyInfo p)
        {
            return new PropertyInfoEx(
                p,
                p.CustomAttributes.Where(o =>
                    new[] { typeof(PersonalDataAttribute), typeof(System.ComponentModel.DataAnnotations.RequiredAttribute)
                    }.Contains(o.AttributeType)).Any(),
                Nullable.GetUnderlyingType(p.PropertyType) != null,
                Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType,
                p.GetCustomAttribute<DefaultValueAttribute>()?.Value
                );
        }
    }
    public List<PropertyInfoEx> UserProperties => typeof(DevOidcToolkitUser).GetProperties()
		.Where(p => p.CanRead && p.CanWrite)
		.Select((p, i) => new
        {
            OriginalIndex = i,
            PEx = PropertyInfoEx.From(p),
        })
		.OrderByDescending(o => o.PEx.IsRequired)
        .ThenBy(o => o.OriginalIndex)
        .Select(o => o.PEx).ToList();

    public async Task<IActionResult> OnGetAsync()
    {
        Users = await userManager.Users.ToListAsync();

        var qEmail = Request.Query["email"].FirstOrDefault();
        if (!string.IsNullOrEmpty(qEmail))
        {
            Input = Users.FirstOrDefault(o => string.Equals(o.Email, qEmail, StringComparison.OrdinalIgnoreCase));
        }

        return Page();
    }

    private bool IsModelValidSuperStrange()
    {
        if (ModelState.IsValid)
            return true;

        // TODO: this can't be right, all this because checkboxes yield "on" instead of booleans?
        var invalids = ModelState.Where(o => o.Value != null && o.Value.ValidationState != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Valid).ToList();

        foreach (var item in invalids)
        {
            if (item.Value == null)
                continue;
            var errors = item.Value!.Errors;
            var p = UserProperties.SingleOrDefault(o => o.PropertyInfo.Name == item.Key);
            if (p != null)
            {
                if (p.Type == typeof(bool))
                {
                    var onErr = errors.SingleOrDefault(o => o.ErrorMessage.Contains($"The value 'on' is not valid for"));
                    if (onErr != null)
                    {
                        var bVal = item.Value?.AttemptedValue == "on";
                        p.PropertyInfo.SetValue(Input, bVal);
                        ModelState.SetModelValue(item.Key, bVal, $"{bVal}".ToLower());
                        errors.Remove(onErr);
                    }
                }
            }
            //if (errors.Any() == false) { }
        }

        return invalids.Any(o => o.Value == null || o.Value.Errors.Any()) == false; // !ModelState.IsValid
    }

    public async Task<IActionResult> OnPost()
    {
        if (!IsModelValidSuperStrange())
            return Page();

        if (Input == null)
            return Page();

        Users = await userManager.Users.ToListAsync();
        var existing = Users.FirstOrDefault(o => string.Equals(o.Email, Input.Email, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            // TODO: how can we update the existing without manually setting all properties?
            foreach (var p in UserProperties)
            {
                var prev = p.PropertyInfo.GetValue(existing);
                var next = p.PropertyInfo.GetValue(Input);
                var update = false;
                if (prev == null)
                    update = next != null;
                else if (next == null)
                    update = true;
                else
                    update = prev.Equals(next) == false;

                if (update)
                    p.PropertyInfo.SetValue(existing, next);
            } 
            
            await userManager.UpdateAsync(existing); 
        }
        else
            await userManager.CreateAsync(Input);

        return Page();
    }
}