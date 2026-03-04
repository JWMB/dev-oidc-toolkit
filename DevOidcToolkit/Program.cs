using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

using DevOidcToolkit;
using DevOidcToolkit.Infrastructure.Configuration;
using DevOidcToolkit.Infrastructure.Database;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

using OpenIddict.Abstractions;

using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.Sources.Clear();

builder.Configuration
    .AddJsonFile("config.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"config.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

var configSection = builder.Configuration.GetSection(DevOidcToolkitConfiguration.Position);
var config = configSection.Get<DevOidcToolkitConfiguration>() ?? new DevOidcToolkitConfiguration();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Logging.SetMinimumLevel(LogEventLevelMapping.LogLevelType(config.Logging.MinimumLevel));

builder.Services.AddDbContext<DevOidcToolkitContext>(options =>
{
    if (config.Database.SqliteFile is not null)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = config.Database.SqliteFile
        }.ToString();
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseInMemoryDatabase("dev-auth");
    }
    options.UseOpenIddict();
});

builder.Services.AddIdentity<DevOidcToolkitUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 0;
    options.Password.RequiredUniqueChars = 0;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<DevOidcToolkitContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    // TODO: cookie doesn't appear in response when behind proxy
    //options.Cookie.Name = "ThisIsMyNewName";
    //options.Cookie.SameSite = SameSiteMode.None;
    //options.Cookie.Domain = "test.bngaged.io:8088";
    //if (options.CookieManager != null) { }
    options.LoginPath = "/login";

    options.Events.OnSignedIn = context =>
    {
        Log(context.HttpContext, context.Principal, "OnSignedIn");
        return Task.CompletedTask;
    };
    options.Events.OnSigningIn = context =>
    {
        Log(context.HttpContext, context.Principal, "OnSigningIn");
        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = context =>
    {
        Log(context.HttpContext, context.Principal, "OnValidatePrincipal");
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToLogin = context =>
    {
        var redirectUri = context.RedirectUri;
        var loginHint = context.Request.Query[Parameters.LoginHint].ToString();
        if (loginHint.Any())
            redirectUri = QueryHelpers.AddQueryString(redirectUri, Parameters.LoginHint, loginHint);

        context.Response.Redirect(redirectUri);
        return Task.CompletedTask;
    };

    static void Log(HttpContext ctx, ClaimsPrincipal? principal, string eventName)
        => GetLogger(ctx)?.LogInformation($"Event={eventName} Authenticated={principal?.Identity?.IsAuthenticated} Type={principal?.Identity?.AuthenticationType} Name={principal?.Identity?.Name}");
    static ILogger? GetLogger(HttpContext ctx) => ctx.RequestServices.GetRequiredService<ILoggerFactory>()?.CreateLogger("Cookies");
});

// Add session support with secure defaults
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".DevOidcToolkit.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.IsEssential = true;
});

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<DevOidcToolkitContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("connect/authorize");
        options.SetTokenEndpointUris("connect/token");
        options.SetUserInfoEndpointUris("connect/user-info");
        options.SetEndSessionEndpointUris("connect/logout");
        options.SetIntrospectionEndpointUris("connect/introspection");

        options.AllowAuthorizationCodeFlow();
        options.AllowClientCredentialsFlow();

        options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile);
        options.RegisterClaims(Claims.Email, Claims.GivenName, Claims.FamilyName, Claims.Role);

        if (config.Issuer is not null)
        {
            options.SetIssuer(new Uri(config.Issuer));
        }

        // Register the signing and encryption credentials.
        options.AddEphemeralEncryptionKey()
               .AddEphemeralSigningKey();

        // Register the ASP.NET Core host and configure the ASP.NET Core options.
        var oidBuilder = options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .EnableStatusCodePagesIntegration()
               .EnableEndSessionEndpointPassthrough();

        if (builder.Environment.IsDevelopment()) //builder.Environment.EnvironmentName == "Development")
            oidBuilder.DisableTransportSecurityRequirement();
    });

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();

Console.WriteLine($"Setting up Kestrel: {config.Address} {config.Port}");

builder.WebHost.ConfigureKestrel(options =>
{
    bool fallbackToDefault = false; // builder.Environment.EnvironmentName != "Development";
    if (config.Address != null)
        options.Listen(IPAddress.Parse(config.Address), config.Port, listenOptions => ConfigureHttps(config.Https, listenOptions, fallbackToDefault));
    else
        options.ListenLocalhost(config.Port, listenOptions => ConfigureHttps(config.Https, listenOptions, fallbackToDefault));
    
    static void ConfigureHttps(HttpsConfiguration? httpsConfig, ListenOptions listenOptions, bool fallbackToDefault)
    {
        if (httpsConfig?.Inline != null)
        {
            var certPem = httpsConfig.Inline.Certificate;
            var keyPem = httpsConfig.Inline.PrivateKey;
            var x509 = X509Certificate2.CreateFromPem(certPem, keyPem);
            listenOptions.UseHttps(x509);
            Console.WriteLine($"Using inline cert");
        }
        else if (httpsConfig?.File != null)
        {
            var certPem = File.ReadAllText(httpsConfig.File.CertificatePath);
            var keyPem = File.ReadAllText(httpsConfig.File.PrivateKeyPath);
            var x509 = X509Certificate2.CreateFromPem(certPem, keyPem);
            listenOptions.UseHttps(x509);
            Console.WriteLine($"Using file cert");
        }
        else if (fallbackToDefault)
        {
            listenOptions.UseHttps();
            Console.WriteLine($"Using default cert");
        }
    }
});


builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = config.Clients
            .SelectMany(client => client.RedirectUris.Concat(client.PostLogoutRedirectUris))
            .Select(uri => new Uri(uri).GetLeftPart(UriPartial.Authority))
            .Concat(string.IsNullOrEmpty(config.PublicAuthority) ? [] : [config.PublicAuthority])
            .Distinct()
            .ToArray();

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto);

if (!builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(config.DataProtectionDirectory))
    builder.Services.AddDataProtection() // Still getting "The antiforgery token could not be decrypted"...
        .PersistKeysToFileSystem(new DirectoryInfo(config.DataProtectionDirectory));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<DevOidcToolkitContext>();

    if (config.Database.SqliteFile is not null)
    {
        try
        {
            db.Database.EnsureCreated();
        }
        catch (SqliteException ex) when (ex.Message.Contains("SQLite Error 14")) // unable to open database file
        {
            throw new Exception($"{nameof(config.Database.SqliteFile)}={config.Database.SqliteFile} ({new FileInfo(config.Database.SqliteFile).FullName})", ex);
        }
    }

    // Set up users and clients in the DB
    var userManager = services.GetRequiredService<UserManager<DevOidcToolkitUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    for (var i = 0; i < config.Users.Count; i++)
    {
        var user = config.Users[i];

        if (await userManager.FindByEmailAsync(user.Email) is not null)
        {
            continue;
        }

        var userEntity = new DevOidcToolkitUser()
        {
            Id = i.ToString(),
            Email = user.Email,
            UserName = user.Username ?? user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            EmailConfirmed = true,
        };
        if (user.Password?.Any() == true)
            userEntity.PasswordHash = userManager.PasswordHasher.HashPassword(userEntity, user.Password);

        var result = await userManager.CreateAsync(userEntity);

        if (!result.Succeeded)
        {
            throw new Exception($"Failed to set up user: ${string.Join(", ", result.Errors.Select(error => error.Description))}");
        }

        foreach (var role in user.Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
            await userManager.AddToRoleAsync(userEntity, role);
        }
    }

    // Set up OIDC clients
    var openIddictManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    foreach (var client in config.Clients)
    {
        if (await openIddictManager.FindByClientIdAsync(client.Id) is not null)
            continue;

        await openIddictManager.CreateAsync(OpenIddictApplicationDescriptorExtensions.Create(client));
    }
}

app.UseDeveloperExceptionPage();
app.UseForwardedHeaders(); // similar to? https://learn.microsoft.com/en-us/answers/questions/1329133/issues-with-openidconnect-and-ms-identity-web-behi

if (!app.Environment.IsDevelopment())
{
    Console.WriteLine("Configure HSTS and HTTPS redirection");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseSession();

app.UseCors();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsDevelopment())
{
    try
    {
        var documentationFileProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "/Documentation");
        app.Map("/documentation", documentationApp =>
        {
            documentationApp.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = documentationFileProvider,
                DefaultFileNames = ["index.html"],
            });

            documentationApp.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = documentationFileProvider,
            });
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Documentation error: {ex.Message}");
    }
}

app.MapControllers();
app.MapRazorPages();

app.Run();
