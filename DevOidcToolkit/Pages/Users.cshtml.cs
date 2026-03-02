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
            } },
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.Email), ModifyInfo = pi => pi.Required = true },
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.NormalizedEmail), ModifyInfo = pi => pi.ReadOnly = true },
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.NormalizedUserName), ModifyInfo = pi => pi.ReadOnly = true }
        ];
    public async Task<IActionResult> OnGetAsync()
    {
        new System.Text.RegularExpressions.Regex("");
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
        if (!FormGenerator.IsModelValidSuperStrange(ModelState, Input)) { }
        else if (Input == null) { }
        else if (await IsModelValidActualValidationSincePropertiesAreNotProperlyDecorated(ModelState, Input)) { }
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

    private async Task Upsert(DevOidcToolkitUser input)
    {
        input.NormalizedEmail = input.Email?.ToUpperInvariant();
        input.NormalizedUserName = input.UserName?.ToUpperInvariant();

        Users = await userManager.Users.ToListAsync();
        var existing = Users.FirstOrDefault(o => string.Equals(o.Email, input.Email, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
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
            Users.Add(input);
            var tmp = await userManager.FindByEmailAsync(input.Email!);
        }

        static string RenderErrors(IdentityResult r) => $"{string.Join(", ", r.Errors.Select(o => $"{o.Code}: {o.Description}"))}";
    }
}
