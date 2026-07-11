using FinanceManagerAspNet.Data;
using FinanceManagerAspNet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceManagerAspNet.Controllers;

public class VaultSettingsController(AppDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!CanEdit()) return LoginRedirect();
        ViewBag.Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.ItemTypes = await db.ItemTypes.OrderBy(t => t.Name).ToListAsync();
        ViewBag.Platforms = await db.Platforms.Include(p => p.ItemType).OrderBy(p => p.Name).ToListAsync();
        ViewBag.DropdownOptions = await db.DropdownOptions.OrderBy(o => o.ListKey).ThenBy(o => o.SortOrder).ThenBy(o => o.Value).ToListAsync();
        ViewBag.SidebarCategories = await db.Categories.Select(c => new { c.Id, c.Name, ItemCount = c.Items.Count }).OrderBy(c => c.Name).ToListAsync();
        ViewBag.CanEdit = true;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(string name, string? description, string? icon, string? colour)
    {
        if (!CanEdit()) return LoginRedirect();
        if (!string.IsNullOrWhiteSpace(name) && !await db.Categories.AnyAsync(c => c.Name == name.Trim()))
        {
            db.Categories.Add(new Category { Name = name.Trim(), Description = description, Icon = icon, Colour = colour });
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItemType(string name, string? description)
    {
        if (!CanEdit()) return LoginRedirect();
        if (!string.IsNullOrWhiteSpace(name) && !await db.ItemTypes.AnyAsync(t => t.Name == name.Trim()))
        {
            db.ItemTypes.Add(new ItemType { Name = name.Trim(), Description = description });
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPlatform(string name, string? description, int? itemTypeId)
    {
        if (!CanEdit()) return LoginRedirect();
        if (!string.IsNullOrWhiteSpace(name) && !await db.Platforms.AnyAsync(p => p.Name == name.Trim() && p.ItemTypeId == itemTypeId))
        {
            db.Platforms.Add(new Platform { Name = name.Trim(), Description = description, ItemTypeId = itemTypeId });
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDropdownOption(string listKey, string value)
    {
        if (!CanEdit()) return LoginRedirect();
        if (!string.IsNullOrWhiteSpace(listKey) && !string.IsNullOrWhiteSpace(value) && !await db.DropdownOptions.AnyAsync(o => o.ListKey == listKey.Trim() && o.Value == value.Trim()))
        {
            db.DropdownOptions.Add(new DropdownOption { ListKey = listKey.Trim(), Value = value.Trim() });
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDropdownOption(int id)
    {
        if (!CanEdit()) return LoginRedirect();
        var option = await db.DropdownOptions.FindAsync(id);
        if (option is not null) { db.DropdownOptions.Remove(option); await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    private bool CanEdit() => User.Identity?.IsAuthenticated == true;
    private IActionResult LoginRedirect() => RedirectToAction("Login", "Auth", new { returnUrl = Request.Path.ToString() + Request.QueryString.ToString() });
}
