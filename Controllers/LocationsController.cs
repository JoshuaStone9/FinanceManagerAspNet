using Microsoft.AspNetCore.Mvc;
using FinanceManagerAspNet.Services;

namespace FinanceManagerAspNet.Controllers;

public class LocationsController(
    ILocationService locationService,
    ICategoryService categoryService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!CanEdit()) return LoginRedirect();
        ViewBag.SidebarCategories = await categoryService.GetAllAsync();
        ViewBag.Locations = await locationService.GetAllAsync();
        ViewBag.CanEdit = true;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? room, string? storageUnit, string? address)
    {
        if (!CanEdit()) return LoginRedirect();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Location name is required.";
            return RedirectToAction(nameof(Index));
        }

        await locationService.CreateAsync(name.Trim(), Clean(room), Clean(storageUnit), Clean(address));
        TempData["Success"] = $"Location '{name.Trim()}' added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        var deleted = await locationService.DeleteAsync(id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Location removed. Any items using it have been set to no location."
            : "Location could not be found.";

        return RedirectToAction(nameof(Index));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAll()
    {
        if (!CanEdit()) return LoginRedirect();
        await locationService.DeleteAllAsync();
        TempData["Success"] = "All locations removed. Any linked items have been set to no location.";
        return RedirectToAction(nameof(Index));
    }
    private bool CanEdit() => User.Identity?.IsAuthenticated == true;

    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
