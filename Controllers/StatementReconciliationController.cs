using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class StatementReconciliationController(FinanceRepository repo, IWebHostEnvironment environment) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var selected = new DateTime(year ?? DateTime.Today.Year, month ?? DateTime.Today.Month, 1);
        return View(await repo.GetStatementReconciliationIndexAsync(selected.Year, selected.Month));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int accountId, int year, int month)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var id = await repo.GetOrCreateStatementAsync(accountId, year, month);
        return RedirectToAction(nameof(Workspace), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Workspace(long id)
    {
        var model = await repo.GetStatementWorkspaceAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(long statementId, IFormFile? statementFile)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (statementFile is null || statementFile.Length == 0 || !string.Equals(Path.GetExtension(statementFile.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Choose a PDF statement up to 10 MB.";
            return RedirectToAction(nameof(Workspace), new { id = statementId });
        }
        var folder = Path.Combine(environment.ContentRootPath, "App_Data", "statements");
        Directory.CreateDirectory(folder);
        var storedName = $"{statementId}-{Guid.NewGuid():N}.pdf";
        await using (var stream = System.IO.File.Create(Path.Combine(folder, storedName))) await statementFile.CopyToAsync(stream);
        await repo.AttachStatementFileAsync(statementId, statementFile.FileName, storedName);
        TempData["Success"] = "Statement stored. PDF extraction will be added in the next phase; transactions can be entered manually now.";
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTransaction(long statementId, DateTime transactionDate, string description, decimal amount, string direction)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        if (string.IsNullOrWhiteSpace(description) || amount <= 0 || direction is not ("In" or "Out"))
            TempData["Error"] = "Enter a description, positive amount and transaction direction.";
        else
        {
            await repo.AddStatementTransactionAsync(statementId, transactionDate, description.Trim(), amount, direction);
            TempData["Success"] = "Statement transaction added.";
        }
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Match(long statementId, long transactionId, string candidateKey)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        await repo.MatchStatementTransactionAsync(transactionId, candidateKey);
        TempData["Success"] = "Transaction matched.";
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ignore(long statementId, long transactionId, string? notes)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        await repo.SetStatementTransactionStatusAsync(transactionId, "Ignored", notes);
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(long statementId, long transactionId)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        await repo.SetStatementTransactionStatusAsync(transactionId, "Unmatched", null);
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(long statementId)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var completed = await repo.CompleteStatementAsync(statementId);
        TempData[completed ? "Success" : "Error"] = completed ? "Statement reconciliation completed." : "Resolve or ignore every transaction before completing the statement.";
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }
}
