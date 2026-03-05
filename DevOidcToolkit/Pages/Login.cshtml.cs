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

    [BindProperty]
    public required CredentialsModel Credentials { get; set; }

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
    }

    public class CredentialsModel
    {
        public string EmailOrUsername { get; set; } = "";
        public string? Password { get; set; }

        public string? ReturnUrl { get; set; } // strange, why needed? https://stackoverflow.com/questions/62431279/how-to-preserve-returnurl-query-parameter-when-submiting-form
    }

    public bool IsDevelopment => false; // Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    private bool autoLogin = false;

    public void OnGet()
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }
        PopulateEmails();
        Credentials ??= new CredentialsModel { EmailOrUsername = "" };
        Credentials.ReturnUrl = Request.Query.TryGetValue("ReturnUrl", out var v) ? $"{v}" : null;
        (var loginHint, autoLogin) = GetEmailAndAutologinFromQuery(Request.QueryString.Value);
        if (loginHint?.Any() == true)
        {
            Credentials.EmailOrUsername = loginHint; // $"{loginHints}";
            Input ??= new InputModel { Email = "", RememberMe = false };
            Input.Email = loginHint; // $"{loginHints}";
        }

        // TODO: use some other query parameter
        //if (Request.Query.TryGetValue(OpenIddict.Abstractions.OpenIddictConstants.Parameters.LoginHint, out _))
        //{
        //    autoLogin = true;
        //}
    }

    public (string, bool) GetEmailAndAutologinFromQuery(string? url)
    {
        //TryGetValue(OpenIddict.Abstractions.OpenIddictConstants.Parameters.LoginHint, out var loginHints)
        if (url?.Any() == true && Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            var queryString = uri.IsAbsoluteUri ? uri.Query.ToString() : uri.OriginalString.TrimStart("?").ToString(); // stupid Uri!
            if (Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString)
                .TryGetValue(OpenIddict.Abstractions.OpenIddictConstants.Parameters.LoginHint, out var v))
            {
                var split = $"{v}".Split(';');
                return (split[0], split.Length > 1 && split[1] == "autologin");
            }
        }
        return ("", false);
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
        DevOidcToolkitUser? user;
        returnUrl ??= Credentials.ReturnUrl;

        var (email, autologin) = GetEmailAndAutologinFromQuery(Request.Headers["Referer"].ToString());
        autoLogin = true; // TODO: no referer?! looks like it in network tab?
        if (autoLogin)
        {
            user = new DevOidcToolkitUser { FirstName = "Otto", LastName = "Auto", Email = Credentials.EmailOrUsername };
            UsersPageModel.NormalizeUser(user, true, true);
            await UsersPageModel.Upsert(user, userManager);

            await _signInManager.SignInAsync(user, false);
            return LocalRedirect(returnUrl ?? Url.Content("/user"));
        }

        user = await _userManager.FindByEmailAsync(Credentials.EmailOrUsername) ?? await _userManager.FindByNameAsync(Credentials.EmailOrUsername);
        if (user != null && Credentials.Password?.Any() == true)
        {
            if (await _userManager.CheckPasswordAsync(user, Credentials.Password))
            {
                await _signInManager.SignInAsync(user, Input.RememberMe);
            }
            else
            {
                var result = await _signInManager.PasswordSignInAsync(user, Credentials.Password, Input.RememberMe, user.AccessFailedCount > 3);
                if (!result.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, $"RF:{result.RequiresTwoFactor} L:{result.IsLockedOut} NA:{result.IsNotAllowed}");
                    PopulateEmails();
                    return Page();
                }
            }
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