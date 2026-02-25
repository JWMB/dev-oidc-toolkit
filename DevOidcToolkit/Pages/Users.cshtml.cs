namespace DevOidcToolkit.Pages;

using System.Text.Json;

using DevOidcToolkit.Infrastructure.Database;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

//[Authorize]
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
            //new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.FirstName), ModifyInfo = pi => pi.MinLength = 1 },
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.NormalizedEmail), ModifyInfo = pi => pi.ReadOnly = true },
            new RenderOverride { PropertyName = nameof(DevOidcToolkitUser.NormalizedUserName), ModifyInfo = pi => pi.ReadOnly = true }
        ];
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
        if (!FormGenerator.IsModelValidSuperStrange(ModelState, Input)) { }
        else if (Input == null) { }
        else
            await Upsert(Input);

        return Page();
    }

    private async Task Upsert(DevOidcToolkitUser input)
    {
        Users = await userManager.Users.ToListAsync();
        var existing = Users.FirstOrDefault(o => string.Equals(o.Email, input.Email, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            FormGenerator.UpdateModel(input, existing);
            await userManager.UpdateAsync(existing);
        }
        else
        {
            input.Id = Guid.NewGuid().ToString().Replace("-", "");
            await userManager.CreateAsync(input);
            Users.Add(input);
        }
    }
}