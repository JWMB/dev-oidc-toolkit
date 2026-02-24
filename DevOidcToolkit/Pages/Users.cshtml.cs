namespace DevOidcToolkit.Pages;

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

    public async Task<IActionResult> OnGetAsync()
    {
        Users = await userManager.Users.ToListAsync();

        var qEmail = Request.Query["email"].FirstOrDefault();
        if (!string.IsNullOrEmpty(qEmail))
            Input = Users.FirstOrDefault(o => string.Equals(o.Email, qEmail, StringComparison.OrdinalIgnoreCase));

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        if (!FormGenerator.IsModelValidSuperStrange(ModelState, Input))
            return Page();

        if (Input == null)
            return Page();

        Users = await userManager.Users.ToListAsync();
        var existing = Users.FirstOrDefault(o => string.Equals(o.Email, Input.Email, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            FormGenerator.UpdateModel(Input, existing);
            await userManager.UpdateAsync(existing); 
        }
        else
            await userManager.CreateAsync(Input);

        return Page();
    }
}