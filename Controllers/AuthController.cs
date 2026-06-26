using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class AuthController(AppAuthService auth) : Controller
{
    [HttpGet]
    public IActionResult Start()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (!await auth.HasPasswordAsync())
            return RedirectToAction(nameof(CreatePassword), new { returnUrl });

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string password, string? returnUrl = null)
    {
        if (!await auth.HasPasswordAsync())
            return RedirectToAction(nameof(CreatePassword), new { returnUrl });

        if (!await auth.CheckPasswordAsync(password))
        {
            ViewBag.ReturnUrl = returnUrl;
            ModelState.AddModelError(string.Empty, "Incorrect password.");
            return View();
        }

        await auth.SignInAsync(HttpContext);
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpGet]
    public async Task<IActionResult> CreatePassword(string? returnUrl = null)
    {
        if (await auth.HasPasswordAsync() && User.Identity?.IsAuthenticated != true)
            return RedirectToAction(nameof(Login), new { returnUrl });

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePassword(string password, string confirmPassword, string? returnUrl = null)
    {
        if (await auth.HasPasswordAsync() && User.Identity?.IsAuthenticated != true)
            return RedirectToAction(nameof(Login), new { returnUrl });

        if (password != confirmPassword)
            ModelState.AddModelError(string.Empty, "Passwords do not match.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            ModelState.AddModelError(string.Empty, "Password must be at least 4 characters.");

        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        await auth.SetPasswordAsync(password);
        await auth.SignInAsync(HttpContext);
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await auth.SignOutAsync(HttpContext);
        return RedirectToAction(nameof(Start));
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action("Index", "Dashboard") ?? "/";
}
