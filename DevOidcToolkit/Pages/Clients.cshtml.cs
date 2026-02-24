using DevOidcToolkit.Infrastructure.Database;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

using OpenIddict.EntityFrameworkCore.Models;

namespace DevOidcToolkit.Pages
{
    public partial class ClientsPageModel : PageModel
    {
        private readonly DevOidcToolkitContext dbContext;

        public ClientsPageModel(DevOidcToolkitContext dbContext)
        {
            this.dbContext = dbContext;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            Clients = (await dbContext.Set<OpenIddictEntityFrameworkCoreApplication>().ToListAsync()) ?? [];

            var qId = Request.Query["clientid"].FirstOrDefault();
            if (!string.IsNullOrEmpty(qId))
                Input = Clients.FirstOrDefault(o => string.Equals(o.ClientId, qId, StringComparison.OrdinalIgnoreCase));

            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            if (!FormGenerator.IsModelValidSuperStrange(ModelState, Input))
                return Page();

            if (Input == null)
                return Page();

            Clients = (await dbContext.Set<OpenIddictEntityFrameworkCoreApplication>().ToListAsync()) ?? [];
            var existing = Clients.FirstOrDefault(o => string.Equals(o.ClientId, Input.ClientId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                //existing.Permissions
                FormGenerator.UpdateModel(Input, existing);
                dbContext.Update(existing);
            }
            else
                await dbContext.AddAsync(Input);

            return Page();
        }
        public OpenIddictEntityFrameworkCoreApplication? Input { get; set; }
        public List<OpenIddictEntityFrameworkCoreApplication> Clients { get; private set; } = [];
    }
}
