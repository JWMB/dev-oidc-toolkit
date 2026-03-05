namespace DevOidcToolkit.Pages;

using System.Text.Json;

using DevOidcToolkit.Infrastructure.Database;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

    [BindProperty]
    public required string? JsonInput { get; set; }

    public List<DevOidcToolkitUser> Users { get; set; } = new();

    public List<RenderOverride> GetRenderOverrides() 
        => [
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.UserName), ModifyInfo = pi => {
                pi.Required = true;
                pi.Pattern = @"\w+"; // probably min/max length?
                pi.Autocomplete = "off";
            } },
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.Email), ModifyInfo = pi => pi.Required = true },
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.NormalizedEmail), ModifyInfo = pi => pi.ReadOnly = true },
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.NormalizedUserName), ModifyInfo = pi => pi.ReadOnly = true }
        ];

    private List<PropertyWrapperBase> GetPropertyWrappers(DevOidcToolkitUser? target = null)
    {
        var overrides = GetRenderOverrides();
        var props = FormGenerator.AnalyzeProperties<DevOidcToolkitUser>(overrides?.Where(o => o.ModifyInfo != null)
            .Select(o => KeyValuePair.Create(o.PropertyName, o.ModifyInfo!)));

        // Replace PasswordHash in GUI with Password, which gets hashed before saving to PasswordHash
        var propPwdHash = props.Single(o => o.Name == nameof(DevOidcToolkitUser.PasswordHash));
        propPwdHash.Hidden = true;

        props.Insert(props.FindIndex(o => o.Name == "Email") + 1, new PropertyProxy(
            propPwdHash,
            transformSetValue: val => target != null && val is string str ? userManager.PasswordHasher.HashPassword(target, str) : val
        )
        {
            Name = propPwdHash.Name,
            DisplayName = "Password",
            Type = typeof(string),
            Secret = true,
            Autocomplete = "off"
        });
        return props;
    }

    public string GetFormHtml()
    {
        return FormGenerator.Render(GetPropertyWrappers(), Input, GetRenderOverrides());
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Users = await userManager.Users.ToListAsync();

        var qEmail = Request.Query["email"].FirstOrDefault();
        if (!string.IsNullOrEmpty(qEmail))
        {
            Input = Users.FirstOrDefault(o => string.Equals(o.Email, qEmail, StringComparison.OrdinalIgnoreCase));
            if (Input != null)
                JsonInput = JsonSerializer.Serialize(Input, new JsonSerializerOptions { WriteIndented = true });
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSubmitJson()
    {
        if (JsonInput?.Any() != true)
            return Page();

        try
        {
            var input = JsonSerializer.Deserialize<DevOidcToolkitUser>(JsonInput);
            if (input == null)
                throw new Exception("Not deserializable");
            await Upsert(input);
        }
        catch (Exception ex)
        {
            throw;
            //return 
        }

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (false == FormGenerator.IsModelValidSuperStrange(ModelState, Input)) { }
        else if (Input == null) { }
        else if (false == await IsModelValidActualValidationSincePropertiesAreNotProperlyDecorated(ModelState, Input)) { }
        else
            await Upsert(Input);

        return Page();
    }

    private async Task<bool> IsModelValidActualValidationSincePropertiesAreNotProperlyDecorated(ModelStateDictionary modelState, DevOidcToolkitUser input)
    {
        var validationTasks = userManager.UserValidators.Select(o => o.ValidateAsync(userManager, input));
        var validationResults = await Task.WhenAll(validationTasks);
        var errors = validationResults.Where(o => o.Succeeded == false).SelectMany(o => o.Errors).ToList();
        if (errors.Any())
        {
            foreach (var error in errors)
                modelState.AddModelError(error.Code, error.Description);
            return false;
        }
        return true;
    }

    // TODO: move to service
    public static void NormalizeUser(DevOidcToolkitUser input, bool autosetUsername = false, bool autosetId = false)
    {
        input.Email = input.Email?.Trim();
        input.NormalizedEmail = input.Email?.ToUpperInvariant();

        input.UserName = input.UserName?.Trim();
        if (autosetUsername && input.UserName?.Any() != false)
            input.UserName = input.Email?.Replace("@", "").Replace(".", "").Replace("-", "");
        input.NormalizedUserName = input.UserName?.ToUpperInvariant();

        input.Id = autosetId ? Guid.NewGuid().ToString().Replace("-", "") : input.Id.Trim();
    }

    public static async Task Upsert(DevOidcToolkitUser input, UserManager<DevOidcToolkitUser> userManager)
    {
        NormalizeUser(input);

        var existing = (input.Id.Any() && input.Id != "0" ? await userManager.FindByIdAsync(input.Id) : null)
            ?? (input.Email?.Any() == true ? await userManager.FindByEmailAsync(input.Email) : null)
            ?? await userManager.FindByNameAsync(input.UserName ?? "");

        if (existing != null)
        {
            input.Id = existing.Id;
            FormGenerator.UpdateModel(input, existing);
            var result = await userManager.UpdateAsync(existing);
            if (!result.Succeeded)
                throw new Exception($"{RenderErrors(result)}");  // TODO: update modelState (e.g. username already exists)
        }
        else
        {
            input.Id = Guid.NewGuid().ToString().Replace("-", "");
            var result = await userManager.CreateAsync(input);
            if (!result.Succeeded)
                throw new Exception($"{RenderErrors(result)}"); // TODO: update modelState (e.g. username already exists)
        }

        static string RenderErrors(IdentityResult r) => $"{string.Join(", ", r.Errors.Select(o => $"{o.Code}: {o.Description}"))}";
    }

    private async Task Upsert(DevOidcToolkitUser input)
    {
        foreach (var item in GetPropertyWrappers(input).OfType<PropertyProxy>())
            item.SetValue(input, item.GetValue(input));

        await Upsert(input, userManager);
        Users = await userManager.Users.ToListAsync();
    }
}
