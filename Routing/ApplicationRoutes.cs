namespace PersonalHub.Routing;

public static class ApplicationRoutes
{
    public static void MapApplicationRoutes(this IEndpointRouteBuilder app)
    {
        // Accept Personal Vault links that were generated through the Move In Planner route.
        app.MapControllerRoute(
            name: "personal-vault-detail-compatibility",
            pattern: "MoveInPlanner/Items/Detail/{id?}",
            defaults: new { area = "", controller = "Items", action = "Detail" })
            .WithMetadata(new Microsoft.AspNetCore.Routing.SuppressLinkGenerationMetadata());
        
        // Accept payment links that were generated with the Move In Planner prefix.
        // This must appear before the general Move In Planner area route.
        app.MapControllerRoute(
            name: "finance-edit-payment-compatibility",
            pattern: "MoveInPlanner/Dashboard/EditPayment/{id?}",
            defaults: new { area = "", controller = "Dashboard", action = "EditPayment" })
            .WithMetadata(new Microsoft.AspNetCore.Routing.SuppressLinkGenerationMetadata());
        
        app.MapAreaControllerRoute(
            name: "move-in-planner",
            areaName: "MoveInPlanner",
            pattern: "MoveInPlanner/{controller=Dashboard}/{action=Index}/{id?}");
        
        app.MapControllerRoute(
            name: "vault",
            pattern: "PersonalVault/{action=Index}/{id?}",
            defaults: new { area = "", controller = "Items" },
            constraints: new { area = "" })
            .WithMetadata(new Microsoft.AspNetCore.Routing.SuppressLinkGenerationMetadata());
        
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Auth}/{action=Start}/{year?}/{month?}");
    }
}
