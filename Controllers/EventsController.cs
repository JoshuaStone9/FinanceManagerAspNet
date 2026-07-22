using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class EventsController(FinanceRepository repo, FinanceEventExportService exportService) : Controller
{
    public async Task<IActionResult> Index(int? potId, string? eventType, string? area, bool includeDetailedAudit, DateTime? from, DateTime? to)
    {
        return View(new FinanceEventsViewModel
        {
            PotId = potId,
            EventType = eventType,
            Area = area,
            IncludeDetailedAudit = includeDetailedAudit,
            From = from,
            To = to,
            Pots = await repo.GetReservePotsAsync(),
            Events = await repo.GetFinanceEventsAsync(potId, eventType, area, includeDetailedAudit, from, to)
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(int? potId, string? eventType, string? area, bool includeDetailedAudit, DateTime? from, DateTime? to)
    {
        var events = await repo.GetFinanceEventsAsync(potId, eventType, area, includeDetailedAudit, from, to);
        return File(exportService.BuildCsv(events), "text/csv; charset=utf-8", $"finance-events-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(int? potId, string? eventType, string? area, bool includeDetailedAudit, DateTime? from, DateTime? to)
    {
        var events = await repo.GetFinanceEventsAsync(potId, eventType, area, includeDetailedAudit, from, to);
        var filters = $"Filters: allocation {(potId?.ToString() ?? "all")}; area {area ?? "all"}; type {eventType ?? "all"}; date {from?.ToString("dd MMM yyyy") ?? "start"} to {to?.ToString("dd MMM yyyy") ?? "today"}.";
        return File(exportService.BuildPdf(events, filters), "application/pdf", $"finance-events-{DateTime.Now:yyyyMMdd-HHmm}.pdf");
    }
}
