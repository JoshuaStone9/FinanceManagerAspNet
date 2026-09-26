# Move-in Planner area

Move-in Planner is integrated into the main application as an MVC Area at `/MoveInPlanner`.

- The welcome screen displays **Move-in Planner** beneath Personal Vault.
- Existing data remains in the separate `MoveInPlannerDb` database.
- Anonymous users can browse in read-only mode. Add, edit, delete and product-choice actions are hidden and protected by the shared login.
- Planner CSS is isolated under `wwwroot/move-in-planner`.
- Set `MIP_CONNECTION_STRING` to override the configured planner database connection.

The imported controllers, entity model, EF Core configuration, views, product metadata services and migration history remain under this directory.
