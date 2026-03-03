namespace DevOidcToolkit.Pages;

using System.Text.Json;

using DevOidcToolkit.Infrastructure.Database;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

[Authorize]
public partial class ClientsPageModel : PageModel
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly DevOidcToolkitContext dbContext;

    public ClientsPageModel(IOpenIddictApplicationManager applicationManager, DevOidcToolkitContext dbContext)
    {
        _applicationManager = applicationManager;
        this.dbContext = dbContext;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Clients = (await dbContext.Set<OpenIddictEntityFrameworkCoreApplication>().ToListAsync()) ?? [];

        var qId = Request.Query["clientid"].FirstOrDefault();
        if (!string.IsNullOrEmpty(qId))
        {
            Input = Clients.FirstOrDefault(o => string.Equals(o.ClientId, qId, StringComparison.OrdinalIgnoreCase));
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
            var input = JsonSerializer.Deserialize<OpenIddictEntityFrameworkCoreApplication>(JsonInput);
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

    private async Task Upsert(OpenIddictEntityFrameworkCoreApplication input)
    {
        var existing = string.IsNullOrEmpty(input.ClientId) ? null : await _applicationManager.FindByClientIdAsync(input.ClientId);

        if (existing is OpenIddictEntityFrameworkCoreApplication existingApp)
        {
            //FormGenerator.UpdateModel(input, existingApp);
            var descriptor = CreateDescriptor(input);
            await _applicationManager.PopulateAsync(existingApp, descriptor);
            await _applicationManager.UpdateAsync(existingApp);
            dbContext.Update(existingApp);
        }
        else
        {
            input.Id = Guid.NewGuid().ToString().Replace("-", "");

            var clientApp = CreateDescriptor(input);
            await _applicationManager.CreateAsync(clientApp);
            await dbContext.AddAsync(input);
        }

        Clients = (await dbContext.Set<OpenIddictEntityFrameworkCoreApplication>().ToListAsync()) ?? [];
    }

    // TODO: move to a service
    public static OpenIddictApplicationDescriptor CreateDescriptor(OpenIddictEntityFrameworkCoreApplication input, IEnumerable<string>? permissions = null,
        bool useDefaultPermissions = true)
    {
        var clientApp = new OpenIddictApplicationDescriptor()
        {
            ApplicationType = input.ApplicationType,
            ClientType = input.ClientType,
            DisplayName = input.DisplayName,
            ClientId = input.ClientId,
            ClientSecret = input.ClientSecret,

            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
        };

        // TODO: lots to parse here
        //foreach (var item in GetStrings(input.DisplayNames) ?? [])
        //    clientApp.DisplayNames.Add(item);

        //foreach (var item in input.Properties ?? [])
        //    clientApp.Properties.Add(item);

        //clientApp.Settings.Add("");

        //clientApp.JsonWebKeySet
        //input.Tokens

        permissions = input.Permissions?.Any() == true
            ? GetStrings(input.Permissions)
            : (useDefaultPermissions
            ? [
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,

                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,

                OpenIddictConstants.Permissions.ResponseTypes.Code,

                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Email
                ] : permissions ?? []);

        foreach (var item in permissions ?? [])
            clientApp.Permissions.Add(item);

        foreach (var item in GetStrings(input.Requirements) ?? [])
            clientApp.Requirements.Add(item);

        foreach (var uri in GetValidUris(input.RedirectUris, nameof(input.RedirectUris)))
            clientApp.RedirectUris.Add(uri);

        foreach (var uri in GetValidUris(input.PostLogoutRedirectUris, nameof(input.PostLogoutRedirectUris)))
            clientApp.PostLogoutRedirectUris.Add(uri);

        return clientApp;

        static IEnumerable<string>? GetStrings(string? csvUris)
        {
            csvUris = csvUris?.Trim();
            if (string.IsNullOrEmpty(csvUris))
                return null;
            return (csvUris.StartsWith("[")
                ? JsonSerializer.Deserialize<List<string>>(csvUris)
                : csvUris.Split(',').Select(uri => uri.Trim()).Where(uri => !string.IsNullOrWhiteSpace(uri))
                );
        }

        static IEnumerable<Uri> GetValidUris(string? csvUris, string name)
        {
            var uris = GetStrings(csvUris);
            if (uris == null)
                return [];
            var invalid = uris.Where(o => Uri.IsWellFormedUriString(o, UriKind.Absolute) == false);
            if (invalid.Any())
                throw new InvalidOperationException($"Invalid {name}: {string.Join(", ", invalid.Select(o => $"'{o}'"))}");
            return uris.Select(o => new Uri(o));
        }
    }

    [BindProperty]
    public OpenIddictEntityFrameworkCoreApplication? Input { get; set; }

    [BindProperty]
    public required string? JsonInput { get; set; }

    public List<OpenIddictEntityFrameworkCoreApplication> Clients { get; private set; } = [];

    public List<RenderOverride> GetRenderOverrides() =>
        [
            new RenderOverride { PropertyName = nameof(OpenIddictEntityFrameworkCoreApplication.ClientSecret), ModifyInfo = pi => pi.Secret = true }
        ];
}
