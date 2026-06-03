using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using TrainingPlatform.Reports.Auth;
using TrainingPlatform.Reports.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddHttpContextAccessor();

// This name has to match the MVC app's cookie scheme, or the shared cookie won't work here.
builder.Services
    .AddAuthentication(SharedCookie.Scheme)
    .AddCookie(SharedCookie.Scheme, options =>
    {
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = SharedCookie.Name;
        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Login lives in the MVC app now — send unauthenticated visitors there.
        options.Events.OnRedirectToLogin = context =>
        {
            var navLinks = context.HttpContext.RequestServices.GetRequiredService<INavLinks>();
            context.Response.Redirect(navLinks.Mvc("/Account/Login"));
            return Task.CompletedTask;
        };
    });

var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "TrainingPlatform", "keys");
Directory.CreateDirectory(keyRingPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
    .SetApplicationName("TrainingPlatform");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TrainingCoordinator", policy =>
        policy.RequireAuthenticatedUser().RequireRole("TrainingCoordinator"));

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("TrainingCoordinator")
        .Build();
});

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl is not configured.");

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<INavLinks, NavLinks>();

var app = builder.Build();

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
