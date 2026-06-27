using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace FinanceManagerAspNet.Services;

public sealed class AppAuthService(FinanceRepository repo, IConfiguration config)
{
    private readonly PasswordHasher<object> _passwordHasher = new();

    public async Task<bool> HasPasswordAsync() => await repo.GetLoginPasswordHashAsync() is not null;

    public async Task SetPasswordAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));

        var hash = _passwordHasher.HashPassword(new object(), password);
        await repo.SaveLoginPasswordHashAsync(hash);
    }

    public async Task<bool> CheckPasswordAsync(string password)
    {
        var storedHash = await repo.GetLoginPasswordHashAsync();
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        var result = _passwordHasher.VerifyHashedPassword(new object(), storedHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public async Task SignInAsync(HttpContext httpContext)
    {
        var rememberDays = int.TryParse(config["AppSecurity:RememberDays"], out var days) ? days : 365;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Owner"),
            new("CanEdit", "true")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var props = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(rememberDays)
        };

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }

    public async Task SignOutAsync(HttpContext httpContext) =>
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
