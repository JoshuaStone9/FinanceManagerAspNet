using FinanceManagerAspNet.Models;
using FinanceManagerAspNet.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManagerAspNet.Controllers;

public sealed class EventsController(FinanceRepository repo) : Controller
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
}
