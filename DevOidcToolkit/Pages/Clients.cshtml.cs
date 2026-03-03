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
            var descriptor = OpenIddictApplicationDescriptorExtensions.Create(input);
            await _applicationManager.PopulateAsync(existingApp, descriptor);
            await _applicationManager.UpdateAsync(existingApp);
            dbContext.Update(existingApp);
        }
        else
        {
            input.Id = Guid.NewGuid().ToString().Replace("-", "");

            var clientApp = OpenIddictApplicationDescriptorExtensions.Create(input);
            await _applicationManager.CreateAsync(clientApp);
            await dbContext.AddAsync(input);
        }

        Clients = (await dbContext.Set<OpenIddictEntityFrameworkCoreApplication>().ToListAsync()) ?? [];
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
