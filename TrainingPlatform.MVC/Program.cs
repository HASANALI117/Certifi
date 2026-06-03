using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;
using TrainingPlatform.API.Models;
using TrainingPlatform.MVC.Hubs;
using TrainingPlatform.MVC.Services;

// Set the whole app to Bahraini Dinar so money is formatted correctly everywhere.
var bhdCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
bhdCulture.NumberFormat.CurrencySymbol = "BD";
bhdCulture.NumberFormat.CurrencyDecimalDigits = 3;
bhdCulture.NumberFormat.CurrencyDecimalSeparator = ".";
bhdCulture.NumberFormat.CurrencyGroupSeparator = ",";
bhdCulture.NumberFormat.CurrencyPositivePattern = 2; // "$ n" → "BD 1.000"
bhdCulture.NumberFormat.CurrencyNegativePattern = 12; // "$ -n"
CultureInfo.DefaultThreadCurrentCulture = bhdCulture;
CultureInfo.DefaultThreadCurrentUICulture = bhdCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Use the same cookie settings as the Reports app so the login cookie works in both.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "TrainingPlatform.Auth";
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "TrainingPlatform", "keys");
Directory.CreateDirectory(keyRingPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
    .SetApplicationName("TrainingPlatform");

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
    ?? throw new InvalidOperationException("ApiSettings:BaseUrl is not configured.");

// Typed HttpClient for the public certification lookup
builder.Services.AddHttpClient<ICertificationLookupService, CertificationLookupService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// Gets an API token when the user logs in and saves it in the cookie, so the Reports app can use it too.
builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton<INavLinks, NavLinks>();

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddSignalR();

// Stripe API key from config. The real keys are kept out of source control.
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();

// Set up roles and sample data so the app works even if you only run this project. It's safe to run again.
using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbSeeder.SeedAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error while seeding the database from the MVC host.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Don't cache the AJAX fragment responses, or the browser might show a page with no layout.
app.Use(async (ctx, next) =>
{
    var isAjax = string.Equals(
        ctx.Request.Headers["X-Requested-With"],
        "XMLHttpRequest",
        StringComparison.OrdinalIgnoreCase);

    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers["Vary"] = "X-Requested-With";
        if (isAjax)
        {
            ctx.Response.Headers["Cache-Control"] = "no-store";
        }
        return Task.CompletedTask;
    });

    await next();
});

app.MapStaticAssets();
app.MapHub<EnrollmentHub>("/hubs/enrollment");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
