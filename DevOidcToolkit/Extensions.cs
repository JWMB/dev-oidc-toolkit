using System.Text.Json;

using DevOidcToolkit.Infrastructure.Configuration;

using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace DevOidcToolkit
{
    public static class OpenIddictApplicationDescriptorExtensions
    {
        public static OpenIddictApplicationDescriptor Create(ClientConfiguration client)
        {
            var clientApp = Create(new OpenIddictEntityFrameworkCoreApplication
            {
                ClientId = client.Id,
                ClientSecret = client.Secret,
                ConsentType = ConsentTypes.Explicit,
                PostLogoutRedirectUris = JsonSerializer.Serialize(client.PostLogoutRedirectUris),
                RedirectUris = JsonSerializer.Serialize(client.RedirectUris)
            }, permissions: [
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,

                Permissions.GrantTypes.AuthorizationCode,

                Permissions.ResponseTypes.Code,

                Permissions.Scopes.Profile,
                Permissions.Scopes.Email
            ]);

            return clientApp;
        }

        public static List<string> DefaultPermissions => [
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.EndSession,

            Permissions.GrantTypes.AuthorizationCode,

            Permissions.ResponseTypes.Code,

            Permissions.Scopes.Profile,
            Permissions.Scopes.Email
            ];

        public static OpenIddictApplicationDescriptor Create(OpenIddictEntityFrameworkCoreApplication input, IEnumerable<string>? permissions = null)
        {
            var clientApp = new OpenIddictApplicationDescriptor()
            {
                ApplicationType = input.ApplicationType,
                ClientType = input.ClientType,
                DisplayName = input.DisplayName,
                ClientId = input.ClientId,
                ClientSecret = input.ClientSecret,

                ConsentType = ConsentTypes.Explicit,
            };

            // TODO: clientApp.JsonWebKeySet

            if (!string.IsNullOrWhiteSpace(input.DisplayNames))
                foreach (var item in JsonSerializer.Deserialize<IEnumerable<KeyValuePair<string, string>>>(input.DisplayNames) ?? [])
                    clientApp.DisplayNames.Add(System.Globalization.CultureInfo.CreateSpecificCulture(item.Key), item.Value);

            if (!string.IsNullOrWhiteSpace(input.Properties))
                foreach (var item in JsonSerializer.Deserialize<IEnumerable<KeyValuePair<string, JsonElement>>>(input.Properties) ?? [])
                    clientApp.Properties.Add(item.Key, item.Value);

            if (!string.IsNullOrWhiteSpace(input.Settings))
                foreach (var item in JsonSerializer.Deserialize<IEnumerable<KeyValuePair<string, string>>>(input.Settings) ?? [])
                    clientApp.Settings.Add(item.Key, item.Value);

            permissions = input.Permissions?.Any() == true
                ? GetStrings(input.Permissions)
                : permissions ?? [];

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
                return csvUris.StartsWith("[")
                    ? JsonSerializer.Deserialize<List<string>>(csvUris)
                    : csvUris.Split(',').Select(uri => uri.Trim()).Where(uri => !string.IsNullOrWhiteSpace(uri));
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
    }
}
