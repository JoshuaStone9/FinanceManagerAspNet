using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace FinanceManagerAspNet.Controllers;

public sealed class StatementReconciliationController(FinanceRepository repo, IWebHostEnvironment environment, IStatementImportService importer) : Controller
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
        var workspace = await repo.GetStatementWorkspaceAsync(statementId);
        if (workspace is null) return NotFound();
        var folder = Path.Combine(environment.ContentRootPath, "App_Data", "statements");
        Directory.CreateDirectory(folder);
        var storedName = $"{statementId}-{Guid.NewGuid():N}.pdf";
        var storedPath = Path.Combine(folder, storedName);
        await using (var stream = System.IO.File.Create(storedPath)) await statementFile.CopyToAsync(stream);
        await using (var signatureStream = System.IO.File.OpenRead(storedPath))
        {
            var signature = new byte[5];
            if (await signatureStream.ReadAsync(signature) != signature.Length || Encoding.ASCII.GetString(signature) != "%PDF-")
            {
                System.IO.File.Delete(storedPath);
                TempData["Error"] = "The selected file is not a valid PDF document.";
                return RedirectToAction(nameof(Workspace), new { id = statementId });
            }
        }
        await repo.AttachStatementFileAsync(statementId, statementFile.FileName, storedName);
        try
        {
            var rows = await importer.ExtractAsync(storedPath, workspace.StatementParser, workspace.Year, workspace.Month, HttpContext.RequestAborted);
            await repo.ReplaceStatementImportPreviewAsync(statementId, rows);
            TempData[rows.Count > 0 ? "Success" : "Error"] = rows.Count > 0
                ? $"Statement stored and {rows.Count} transaction line{(rows.Count == 1 ? "" : "s")} extracted for review."
                : $"Statement stored, but no transaction lines were found for {new DateTime(workspace.Year, workspace.Month, 1):MMMM yyyy}. Check that the statement month matches this workspace, or add its lines manually.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            TempData["Error"] = "Statement stored, but its text could not be extracted. The PDF may be scanned or password protected.";
        }
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(long statementId, long[]? selectedRows)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var imported = await repo.ImportStatementPreviewAsync(statementId, selectedRows ?? []);
        TempData[imported > 0 ? "Success" : "Error"] = imported > 0
            ? $"Imported {imported} statement transaction{(imported == 1 ? "" : "s")}."
            : "Select at least one new transaction to import.";
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
    public async Task<IActionResult> CreateEntry(long statementId, long transactionId, string source, string title)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        try
        {
            await repo.CreateEntryFromStatementAsync(statementId, transactionId, source, title);
            TempData["Success"] = $"Created “{title.Trim()}” and matched the statement transaction.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTransaction(long statementId, long transactionId)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        await repo.DeleteStatementTransactionAsync(statementId, transactionId);
        TempData["Success"] = "Statement transaction removed.";
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenStatement(long statementId)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        await repo.ReopenStatementAsync(statementId);
        TempData["Success"] = "Statement reopened for review.";
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(long statementId)
    {
        if (User.Identity?.IsAuthenticated != true) return Unauthorized();
        var completed = await repo.CompleteStatementAsync(statementId);
        TempData[completed ? "Success" : "Error"] = completed ? "Statement reconciliation completed." : "Add at least one transaction and resolve or ignore every line before completing the statement.";
        return RedirectToAction(nameof(Workspace), new { id = statementId });
    }
}
