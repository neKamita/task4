using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task4.Data;
using Task4.Models;
using Task4.Services;

namespace Task4.Controllers;

public class AccountController : Controller
{
    private readonly AppDb _db;
    private readonly EmailService _emailService;
    private readonly IConfiguration _config;

    public AccountController(AppDb db, EmailService emailService, IConfiguration config)
    {
        _db = db;
        _emailService = emailService;
        _config = config;
    }

    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Users");
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _db.LoginAsync(model);

        if (user == null)
        {
            ModelState.AddModelError("", "Invalid email, password, or blocked account.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Name)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return RedirectToAction("Index", "Users");
    }

    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Users");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = await _db.RegisterAsync(model);
            var baseUrl = _config["App:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            _ = _emailService.SendConfirmationAsync(user, baseUrl);
            TempData["Success"] = "Registration completed. You can login now. Confirmation link was sent asynchronously.";
            return RedirectToAction("Login");
        }
        catch (DuplicateEmailException)
        {
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
            return View(model);
        }
    }

    [AllowAnonymous]
    public async Task<IActionResult> Confirm(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Confirmation link is invalid.";
            return RedirectToAction("Login");
        }

        var confirmed = await _db.ConfirmAsync(token);
        TempData[confirmed ? "Success" : "Error"] = confirmed ? "Email confirmed." : "Confirmation link is invalid.";

        return RedirectToAction("Login");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Login");
    }
}
