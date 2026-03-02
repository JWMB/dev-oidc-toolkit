namespace DevOidcToolkit.Pages;

using System.ComponentModel.DataAnnotations;

using DevOidcToolkit.Infrastructure.Database;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

public class LoginPageModel(SignInManager<DevOidcToolkitUser> signInManager, UserManager<DevOidcToolkitUser> userManager) : PageModel
{
    private readonly SignInManager<DevOidcToolkitUser> _signInManager = signInManager;
    private readonly UserManager<DevOidcToolkitUser> _userManager = userManager;

    [BindProperty]
    public required InputModel Input { get; set; }

    [TempData]
    public required string ErrorMessage { get; set; }

    public required List<SelectListItem> UserEmails { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Display(Name = "Remember me?")]
        public required bool RememberMe { get; set; }

        public string EmailOrUsername { get; set; } = "";
        public string? Password { get; set; }
    }

    public bool IsDevelopment => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public void OnGet()
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }
        PopulateEmails();
    }

    private void PopulateEmails()
    {
        UserEmails = IsDevelopment ? [.. _userManager.Users.ToList().Select(u =>
            new SelectListItem
            {
                Value = u.Email,
                Text = u.Email
            })]
            : [];
    }

    public async Task<IActionResult> OnPostPasswordAsync(string? returnUrl = null)
    {
        var user = await _userManager.FindByEmailAsync(Input.EmailOrUsername) ?? await _userManager.FindByNameAsync(Input.EmailOrUsername);
        if (user != null && Input.Password?.Any() == true)
        {
            await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, user.AccessFailedCount > 3);
            return LocalRedirect(returnUrl ?? Url.Content("/user"));
        }

        // If we got this far, something failed, redisplay form
        ModelState.AddModelError(string.Empty, "Invalid login attempt");
        PopulateEmails();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (ModelState.IsValid && IsDevelopment)
        {
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user != null)
            {
                await _signInManager.SignInAsync(user, Input.RememberMe);
                return LocalRedirect(returnUrl ?? Url.Content("/user"));
            }
        }

        // If we got this far, something failed, redisplay form
        ModelState.AddModelError(string.Empty, "Invalid login attempt");
        PopulateEmails();
        return Page();
    }
}