using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Task4.Data;
using Task4.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AppDb>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<EmailService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDb>().InitAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Account/Login");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && !IsPublicPath(context.Request.Path))
    {
        var idText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var db = context.RequestServices.GetRequiredService<AppDb>();

        if (!int.TryParse(idText, out var id))
        {
            await context.SignOutAsync();
            context.Response.Redirect("/Account/Login?message=session");
            return;
        }

        var user = await db.GetUserByIdAsync(id);

        if (user == null || user.Status == "Blocked")
        {
            await context.SignOutAsync();
            context.Response.Redirect("/Account/Login?message=session");
            return;
        }
    }

    await next();
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Users}/{action=Index}/{id?}");

app.Run();

static bool IsPublicPath(PathString path)
{
    var value = path.Value ?? "";

    return value.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/Account/Register", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/Account/Confirm", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase);
}
