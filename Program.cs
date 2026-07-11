using FinanceManagerAspNet.Services;
using FinanceManagerAspNet.Data;
using FinanceManagerAspNet.Repositories;
using FinanceManagerAspNet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FinanceManager")));

builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<FinanceRepository>();
builder.Services.AddScoped<FinanceCalculator>();
builder.Services.AddHttpClient<MarketPriceService>();
builder.Services.AddScoped<AppAuthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "vault",
    pattern: "PersonalVault/{action=Index}/{id?}",
    defaults: new { controller = "Items" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Start}/{year?}/{month?}");

using (var scope = app.Services.CreateScope())
{
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var connectionString = Environment.GetEnvironmentVariable("FM_CONNECTION_STRING")
        ?? configuration.GetConnectionString("FinanceManager")
        ?? throw new InvalidOperationException("Missing FinanceManager connection string.");

    EnsureSqlDatabaseExists(connectionString);

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    await EnsurePersonalVaultSchemaAsync(db);

    var financeRepository = scope.ServiceProvider.GetRequiredService<FinanceRepository>();
    await financeRepository.EnsureModernTablesAsync();
}

app.Run();

static void EnsureSqlDatabaseExists(string connectionString)
{
    var builder = new SqlConnectionStringBuilder(connectionString);
    var databaseName = builder.InitialCatalog;

    if (string.IsNullOrWhiteSpace(databaseName))
    {
        return;
    }

    builder.InitialCatalog = "master";

    using var connection = new SqlConnection(builder.ConnectionString);
    connection.Open();

    using var command = connection.CreateCommand();
    command.CommandText = @"
IF DB_ID(@databaseName) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
    EXEC(@sql);
END";
    command.Parameters.AddWithValue("@databaseName", databaseName);
    command.ExecuteNonQuery();
}


static async Task EnsurePersonalVaultSchemaAsync(AppDbContext db)
{
    await db.Database.ExecuteSqlRawAsync(@"
SET NOCOUNT ON;

------------------------------------------------------------
-- Core lookup tables
------------------------------------------------------------

IF OBJECT_ID('dbo.ItemTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemTypes
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ItemTypes PRIMARY KEY,
        Name nvarchar(450) NOT NULL,
        Description nvarchar(max) NULL
    );
END;

IF OBJECT_ID('dbo.Platforms', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Platforms
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Platforms PRIMARY KEY,
        Name nvarchar(450) NOT NULL,
        Description nvarchar(max) NULL,
        ItemTypeId int NULL
    );
END;

IF OBJECT_ID('dbo.Locations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Locations
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Locations PRIMARY KEY,
        Name nvarchar(450) NOT NULL,
        Description nvarchar(max) NULL,
        LegacyLocationId int NULL
    );
END;

IF OBJECT_ID('dbo.Tags', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tags
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Tags PRIMARY KEY,
        Name nvarchar(150) NOT NULL,
        Colour nvarchar(50) NULL
    );
END;

IF COL_LENGTH('dbo.Tags', 'Colour') IS NULL
BEGIN
    ALTER TABLE dbo.Tags
    ADD Colour nvarchar(50) NULL;
END;

------------------------------------------------------------
-- Dropdown options
------------------------------------------------------------

IF OBJECT_ID('dbo.DropdownOptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DropdownOptions
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_DropdownOptions PRIMARY KEY,
        ListKey nvarchar(80) NOT NULL,
        Value nvarchar(200) NOT NULL,
        SortOrder int NOT NULL
            CONSTRAINT DF_DropdownOptions_SortOrder DEFAULT 0,
        CreatedAt datetime2 NOT NULL
            CONSTRAINT DF_DropdownOptions_CreatedAt
            DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID('dbo.DropdownOptions', 'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_DropdownOptions_ListKey_Value'
         AND object_id = OBJECT_ID('dbo.DropdownOptions')
   )
BEGIN
    CREATE UNIQUE INDEX IX_DropdownOptions_ListKey_Value
        ON dbo.DropdownOptions(ListKey, Value);
END;

------------------------------------------------------------
-- ItemTags junction table
------------------------------------------------------------

IF OBJECT_ID('dbo.ItemTags', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemTags
    (
        ItemId int NOT NULL,
        TagId int NOT NULL,
        CONSTRAINT PK_ItemTags PRIMARY KEY (ItemId, TagId)
    );
END;

-- Rename old TagsId column before attempting to add TagId
IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTags', 'TagsId') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTags', 'TagId') IS NULL
BEGIN
    EXEC sp_rename
        'dbo.ItemTags.TagsId',
        'TagId',
        'COLUMN';
END;

IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTags', 'ItemId') IS NULL
BEGIN
    ALTER TABLE dbo.ItemTags
    ADD ItemId int NULL;
END;

IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTags', 'TagId') IS NULL
BEGIN
    ALTER TABLE dbo.ItemTags
    ADD TagId int NULL;
END;

IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
BEGIN
    DELETE FROM dbo.ItemTags
    WHERE ItemId IS NULL
       OR TagId IS NULL;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ItemTags')
      AND name = 'ItemId'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.ItemTags
    ALTER COLUMN ItemId int NOT NULL;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.ItemTags')
      AND name = 'TagId'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.ItemTags
    ALTER COLUMN TagId int NOT NULL;
END;

IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.key_constraints
       WHERE name = 'PK_ItemTags'
         AND parent_object_id = OBJECT_ID('dbo.ItemTags')
   )
BEGIN
    ALTER TABLE dbo.ItemTags
    ADD CONSTRAINT PK_ItemTags
        PRIMARY KEY (ItemId, TagId);
END;

------------------------------------------------------------
-- Legacy lookup columns
------------------------------------------------------------

IF OBJECT_ID('dbo.ItemTypes', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTypes', 'LegacyTypeId') IS NULL
BEGIN
    ALTER TABLE dbo.ItemTypes
    ADD LegacyTypeId int NULL;
END;

IF OBJECT_ID('dbo.Platforms', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Platforms', 'LegacyPlatformId') IS NULL
BEGIN
    ALTER TABLE dbo.Platforms
    ADD LegacyPlatformId int NULL;
END;

IF OBJECT_ID('dbo.Locations', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Locations', 'LegacyLocationId') IS NULL
BEGIN
    ALTER TABLE dbo.Locations
    ADD LegacyLocationId int NULL;
END;

------------------------------------------------------------
-- Items columns
------------------------------------------------------------

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Items', 'ItemTypeId') IS NULL
        ALTER TABLE dbo.Items ADD ItemTypeId int NULL;

    IF COL_LENGTH('dbo.Items', 'PlatformId') IS NULL
        ALTER TABLE dbo.Items ADD PlatformId int NULL;

    IF COL_LENGTH('dbo.Items', 'LocationId') IS NULL
        ALTER TABLE dbo.Items ADD LocationId int NULL;

    IF COL_LENGTH('dbo.Items', 'LegacyInventoryId') IS NULL
        ALTER TABLE dbo.Items ADD LegacyInventoryId int NULL;

    IF COL_LENGTH('dbo.Items', 'LegacyPlatformId') IS NULL
        ALTER TABLE dbo.Items ADD LegacyPlatformId int NULL;

    IF COL_LENGTH('dbo.Items', 'LegacyTypeId') IS NULL
        ALTER TABLE dbo.Items ADD LegacyTypeId int NULL;

    IF COL_LENGTH('dbo.Items', 'LegacyManufacturerId') IS NULL
        ALTER TABLE dbo.Items ADD LegacyManufacturerId int NULL;

    IF COL_LENGTH('dbo.Items', 'LegacyCaseTypeId') IS NULL
        ALTER TABLE dbo.Items ADD LegacyCaseTypeId int NULL;

    IF COL_LENGTH('dbo.Items', 'LegacyFormatId') IS NULL
        ALTER TABLE dbo.Items ADD LegacyFormatId int NULL;

    IF COL_LENGTH('dbo.Items', 'LegacyInstructionId') IS NULL
        ALTER TABLE dbo.Items ADD LegacyInstructionId int NULL;

    IF COL_LENGTH('dbo.Items', 'LegacyLocationId') IS NULL
        ALTER TABLE dbo.Items ADD LegacyLocationId int NULL;

    IF COL_LENGTH('dbo.Items', 'OldInventoryId') IS NULL
        ALTER TABLE dbo.Items ADD OldInventoryId int NULL;

    IF COL_LENGTH('dbo.Items', 'Manufacturer') IS NULL
        ALTER TABLE dbo.Items ADD Manufacturer nvarchar(max) NULL;

    IF COL_LENGTH('dbo.Items', 'CaseType') IS NULL
        ALTER TABLE dbo.Items ADD CaseType nvarchar(max) NULL;

    IF COL_LENGTH('dbo.Items', 'MediaFormat') IS NULL
        ALTER TABLE dbo.Items ADD MediaFormat nvarchar(max) NULL;

    IF COL_LENGTH('dbo.Items', 'Instruction') IS NULL
        ALTER TABLE dbo.Items ADD Instruction nvarchar(max) NULL;

    IF COL_LENGTH('dbo.Items', 'Memory') IS NULL
        ALTER TABLE dbo.Items ADD Memory nvarchar(max) NULL;

    IF COL_LENGTH('dbo.Items', 'Owner') IS NULL
        ALTER TABLE dbo.Items ADD [Owner] nvarchar(max) NULL;

    IF COL_LENGTH('dbo.Items', 'ReleaseYear') IS NULL
        ALTER TABLE dbo.Items ADD ReleaseYear int NULL;

    IF COL_LENGTH('dbo.Items', 'Boxed') IS NULL
        ALTER TABLE dbo.Items ADD Boxed bit NULL;

    IF COL_LENGTH('dbo.Items', 'Sell') IS NULL
        ALTER TABLE dbo.Items ADD Sell bit NULL;

    IF COL_LENGTH('dbo.Items', 'Tested') IS NULL
        ALTER TABLE dbo.Items ADD Tested nvarchar(max) NULL;

    IF COL_LENGTH('dbo.Items', 'CustomStatus') IS NULL
        ALTER TABLE dbo.Items ADD CustomStatus nvarchar(120) NULL;
END;

------------------------------------------------------------
-- Item photos
------------------------------------------------------------

IF OBJECT_ID('dbo.ItemPhotos', 'U') IS NULL
   AND OBJECT_ID('dbo.Items', 'U') IS NOT NULL
BEGIN
    CREATE TABLE dbo.ItemPhotos
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ItemPhotos PRIMARY KEY,
        ItemId int NOT NULL,
        ImagePath nvarchar(max) NOT NULL,
        Caption nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL
            CONSTRAINT DF_ItemPhotos_CreatedAt
            DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_ItemPhotos_Items_ItemId
            FOREIGN KEY (ItemId)
            REFERENCES dbo.Items(Id)
            ON DELETE CASCADE
    );
END;

------------------------------------------------------------
-- Indexes
------------------------------------------------------------

IF OBJECT_ID('dbo.ItemPhotos', 'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_ItemPhotos_ItemId'
         AND object_id = OBJECT_ID('dbo.ItemPhotos')
   )
BEGIN
    CREATE INDEX IX_ItemPhotos_ItemId
        ON dbo.ItemPhotos(ItemId);
END;

IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTags', 'ItemId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_ItemTags_ItemId'
         AND object_id = OBJECT_ID('dbo.ItemTags')
   )
BEGIN
    CREATE INDEX IX_ItemTags_ItemId
        ON dbo.ItemTags(ItemId);
END;

IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTags', 'TagId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_ItemTags_TagId'
         AND object_id = OBJECT_ID('dbo.ItemTags')
   )
BEGIN
    CREATE INDEX IX_ItemTags_TagId
        ON dbo.ItemTags(TagId);
END;

IF OBJECT_ID('dbo.ItemTypes', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTypes', 'LegacyTypeId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_ItemTypes_LegacyTypeId'
         AND object_id = OBJECT_ID('dbo.ItemTypes')
   )
BEGIN
    CREATE INDEX IX_ItemTypes_LegacyTypeId
        ON dbo.ItemTypes(LegacyTypeId);
END;

IF OBJECT_ID('dbo.Platforms', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Platforms', 'LegacyPlatformId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_Platforms_LegacyPlatformId'
         AND object_id = OBJECT_ID('dbo.Platforms')
   )
BEGIN
    CREATE INDEX IX_Platforms_LegacyPlatformId
        ON dbo.Platforms(LegacyPlatformId);
END;

IF OBJECT_ID('dbo.Locations', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Locations', 'LegacyLocationId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_Locations_LegacyLocationId'
         AND object_id = OBJECT_ID('dbo.Locations')
   )
BEGIN
    CREATE INDEX IX_Locations_LegacyLocationId
        ON dbo.Locations(LegacyLocationId);
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'ItemTypeId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_Items_ItemTypeId'
         AND object_id = OBJECT_ID('dbo.Items')
   )
BEGIN
    CREATE INDEX IX_Items_ItemTypeId
        ON dbo.Items(ItemTypeId);
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'PlatformId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_Items_PlatformId'
         AND object_id = OBJECT_ID('dbo.Items')
   )
BEGIN
    CREATE INDEX IX_Items_PlatformId
        ON dbo.Items(PlatformId);
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'LocationId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_Items_LocationId'
         AND object_id = OBJECT_ID('dbo.Items')
   )
BEGIN
    CREATE INDEX IX_Items_LocationId
        ON dbo.Items(LocationId);
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'LegacyInventoryId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_Items_LegacyInventoryId'
         AND object_id = OBJECT_ID('dbo.Items')
   )
BEGIN
    CREATE INDEX IX_Items_LegacyInventoryId
        ON dbo.Items(LegacyInventoryId);
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'LegacyPlatformId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_Items_LegacyPlatformId'
         AND object_id = OBJECT_ID('dbo.Items')
   )
BEGIN
    CREATE INDEX IX_Items_LegacyPlatformId
        ON dbo.Items(LegacyPlatformId);
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'LegacyTypeId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_Items_LegacyTypeId'
         AND object_id = OBJECT_ID('dbo.Items')
   )
BEGIN
    CREATE INDEX IX_Items_LegacyTypeId
        ON dbo.Items(LegacyTypeId);
END;

------------------------------------------------------------
-- Foreign keys
------------------------------------------------------------

IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTags', 'ItemId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = 'FK_ItemTags_Items_ItemId'
   )
BEGIN
    ALTER TABLE dbo.ItemTags
    ADD CONSTRAINT FK_ItemTags_Items_ItemId
        FOREIGN KEY (ItemId)
        REFERENCES dbo.Items(Id)
        ON DELETE CASCADE;
END;

IF OBJECT_ID('dbo.ItemTags', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Tags', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ItemTags', 'TagId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = 'FK_ItemTags_Tags_TagId'
   )
BEGIN
    ALTER TABLE dbo.ItemTags
    ADD CONSTRAINT FK_ItemTags_Tags_TagId
        FOREIGN KEY (TagId)
        REFERENCES dbo.Tags(Id)
        ON DELETE CASCADE;
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.ItemTypes', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'ItemTypeId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = 'FK_Items_ItemTypes_ItemTypeId'
   )
BEGIN
    ALTER TABLE dbo.Items
    ADD CONSTRAINT FK_Items_ItemTypes_ItemTypeId
        FOREIGN KEY (ItemTypeId)
        REFERENCES dbo.ItemTypes(Id)
        ON DELETE SET NULL;
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Platforms', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'PlatformId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = 'FK_Items_Platforms_PlatformId'
   )
BEGIN
    ALTER TABLE dbo.Items
    ADD CONSTRAINT FK_Items_Platforms_PlatformId
        FOREIGN KEY (PlatformId)
        REFERENCES dbo.Platforms(Id)
        ON DELETE SET NULL;
END;

IF OBJECT_ID('dbo.Items', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Locations', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Items', 'LocationId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = 'FK_Items_Locations_LocationId'
   )
BEGIN
    ALTER TABLE dbo.Items
    ADD CONSTRAINT FK_Items_Locations_LocationId
        FOREIGN KEY (LocationId)
        REFERENCES dbo.Locations(Id)
        ON DELETE SET NULL;
END;

IF OBJECT_ID('dbo.Platforms', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.ItemTypes', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.Platforms', 'ItemTypeId') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.foreign_keys
       WHERE name = 'FK_Platforms_ItemTypes_ItemTypeId'
   )
BEGIN
    ALTER TABLE dbo.Platforms
    ADD CONSTRAINT FK_Platforms_ItemTypes_ItemTypeId
        FOREIGN KEY (ItemTypeId)
        REFERENCES dbo.ItemTypes(Id)
        ON DELETE SET NULL;
END;
");

    await SeedPersonalVaultLookupsAsync(db);
}

static async Task SeedPersonalVaultLookupsAsync(AppDbContext db)
{
    // Safety check: ensure the lookup tables exist before querying them.
    // The main schema method should normally create these first.
    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID('dbo.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Categories PRIMARY KEY,
        Name nvarchar(450) NOT NULL,
        Description nvarchar(max) NULL,
        Icon nvarchar(max) NULL,
        Colour nvarchar(max) NULL,
        ParentCategoryId int NULL
    );
END;

IF OBJECT_ID('dbo.ItemTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemTypes
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ItemTypes PRIMARY KEY,
        Name nvarchar(450) NOT NULL,
        Description nvarchar(max) NULL,
        LegacyTypeId int NULL
    );
END;

IF OBJECT_ID('dbo.Platforms', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Platforms
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Platforms PRIMARY KEY,
        Name nvarchar(450) NOT NULL,
        Description nvarchar(max) NULL,
        ItemTypeId int NULL,
        LegacyPlatformId int NULL
    );
END;

IF OBJECT_ID('dbo.DropdownOptions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DropdownOptions
    (
        Id int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_DropdownOptions PRIMARY KEY,
        ListKey nvarchar(80) NOT NULL,
        Value nvarchar(200) NOT NULL,
        SortOrder int NOT NULL
            CONSTRAINT DF_DropdownOptions_SortOrder DEFAULT 0,
        CreatedAt datetime2 NOT NULL
            CONSTRAINT DF_DropdownOptions_CreatedAt
            DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID('dbo.DropdownOptions', 'U') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = 'IX_DropdownOptions_ListKey_Value'
         AND object_id = OBJECT_ID('dbo.DropdownOptions')
   )
BEGIN
    CREATE UNIQUE INDEX IX_DropdownOptions_ListKey_Value
        ON dbo.DropdownOptions(ListKey, Value);
END;
");

    var categories = new (string Name, string Icon, string Colour)[]
    {
        ("Games", "gamepad-2", "#22c55e"),
        ("Games Accessories", "joystick", "#16a34a"),
        ("Consoles", "monitor-dot", "#10b981"),

        ("Computers & Laptops", "laptop", "#6366f1"),
        ("Computer Components", "cpu", "#818cf8"),
        ("Storage & NAS", "hard-drive", "#64748b"),

        ("Phones & Tablets", "smartphone", "#06b6d4"),
        ("Smart Watches", "watch", "#14b8a6"),

        ("Cameras & Photography", "camera", "#8b5cf6"),
        ("Lenses", "aperture", "#7c3aed"),
        ("Drones", "plane", "#0ea5e9"),

        ("Audio & Headphones", "headphones", "#f59e0b"),
        ("TV & Home Cinema", "tv", "#f97316"),

        ("Coins & Bullion", "coins", "#eab308"),
        ("Jewellery & Watches", "gem", "#ec4899"),
        ("Collectables", "archive", "#a855f7"),

        ("Books, Music & Film", "book-open", "#f43f5e"),
        ("Documents", "file-text", "#94a3b8"),

        ("Tools & DIY", "hammer", "#a16207"),
        ("Garden Equipment", "shovel", "#65a30d"),

        ("Kitchen Appliances", "utensils", "#ef4444"),
        ("White Goods", "washing-machine", "#0f766e"),

        ("Furniture", "armchair", "#92400e"),
        ("Home Decor", "lamp", "#d946ef"),

        ("Clothing", "shirt", "#db2777"),
        ("Bags", "briefcase", "#be185d"),

        ("Sports & Outdoor", "tent", "#65a30d"),
        ("Office Equipment", "briefcase", "#3b82f6"),

        ("Vehicles & Accessories", "car", "#f97316"),
        ("Household", "home", "#475569"),
        ("Miscellaneous", "package", "#6b7280")
    };

    var existingCategoryNames = await db.Categories
        .AsNoTracking()
        .Select(x => x.Name)
        .ToListAsync();

    var categoryNameSet = existingCategoryNames
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var category in categories)
    {
        if (categoryNameSet.Contains(category.Name))
            continue;

        db.Categories.Add(new Category
        {
            Name = category.Name,
            Icon = category.Icon,
            Colour = category.Colour,
            Description = "Personal Vault item category"
        });

        categoryNameSet.Add(category.Name);
    }

    await db.SaveChangesAsync();

    var types = new[]
    {
        "Console",
        "Game",
        "Accessory",
        "Peripheral",
        "Camera body",
        "Lens",
        "Drone",
        "Computer",
        "Laptop",
        "Phone",
        "Tablet",
        "Watch",
        "Storage",
        "NAS",
        "Appliance",
        "Tool",
        "Furniture",
        "Document",
        "Collectable",
        "Coin",
        "Jewellery",
        "Book",
        "Vinyl",
        "CD",
        "DVD",
        "Blu-ray",
        "Clothing",
        "Coat",
        "Jacket",
        "T-Shirt",
        "Shirt",
        "Jumper",
        "Hoodie",
        "Jeans",
        "Trousers",
        "Dress",
        "Suit",
        "Shoes",
        "Trainers",
        "Boots",
        "Handbag",
        "Backpack",
        "Suitcase",
        "Wallet",
        "Bag",
        "Vehicle",
        "Other"
    };

    var existingTypeNames = await db.ItemTypes
        .AsNoTracking()
        .Select(x => x.Name)
        .ToListAsync();

    var typeNameSet = existingTypeNames
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    foreach (var type in types)
    {
        if (typeNameSet.Contains(type))
            continue;

        db.ItemTypes.Add(new ItemType
        {
            Name = type,
            Description = "Personal Vault item type"
        });

        typeNameSet.Add(type);
    }

    await db.SaveChangesAsync();

    var itemTypeIds = await db.ItemTypes
        .AsNoTracking()
        .ToDictionaryAsync(
            x => x.Name,
            x => x.Id,
            StringComparer.OrdinalIgnoreCase);

    var platforms = new (string Name, string Type)[]
    {
        ("Sony E-mount", "Lens"),
        ("Canon RF", "Lens"),
        ("Nikon Z", "Lens"),
        ("DJI", "Drone"),

        ("Windows", "Computer"),
        ("macOS", "Computer"),

        ("iOS", "Phone"),
        ("Android", "Phone"),

        ("PlayStation", "Console"),
        ("Xbox", "Console"),
        ("Nintendo Switch", "Console"),

        ("PC", "Game"),

        ("USB-C", "Peripheral"),
        ("Thunderbolt", "Peripheral"),
        ("Network", "Peripheral"),

        ("Not applicable", "Other"),

        ("Wardrobe", "Clothing"),
        ("Shoe Rack", "Shoes"),
        ("Bag Storage", "Bag"),
        ("Everyday Carry", "Bag"),

        ("Home", "Other"),
        ("Work", "Other")
    };

    var existingPlatforms = await db.Platforms
        .AsNoTracking()
        .Select(x => new
        {
            x.Name,
            x.ItemTypeId
        })
        .ToListAsync();

    var platformKeys = existingPlatforms
        .Select(x => $"{x.Name.Trim().ToUpperInvariant()}|{x.ItemTypeId}")
        .ToHashSet();

    foreach (var platform in platforms)
    {
        if (!itemTypeIds.TryGetValue(platform.Type, out var itemTypeId))
            continue;

        var platformKey =
            $"{platform.Name.Trim().ToUpperInvariant()}|{itemTypeId}";

        if (platformKeys.Contains(platformKey))
            continue;

        db.Platforms.Add(new Platform
        {
            Name = platform.Name,
            ItemTypeId = itemTypeId,
            Description = "Personal Vault platform"
        });

        platformKeys.Add(platformKey);
    }

    await db.SaveChangesAsync();

    var existingDropdownOptions = await db.DropdownOptions
        .AsNoTracking()
        .Select(x => new
        {
            x.ListKey,
            x.Value
        })
        .ToListAsync();

    var dropdownKeys = existingDropdownOptions
        .Select(x =>
            $"{x.ListKey.Trim().ToUpperInvariant()}|" +
            $"{x.Value.Trim().ToUpperInvariant()}")
        .ToHashSet();

    void SeedDropdown(string listKey, params string[] values)
    {
        foreach (var value in values)
        {
            var dropdownKey =
                $"{listKey.Trim().ToUpperInvariant()}|" +
                $"{value.Trim().ToUpperInvariant()}";

            if (dropdownKeys.Contains(dropdownKey))
                continue;

            db.DropdownOptions.Add(new DropdownOption
            {
                ListKey = listKey,
                Value = value
            });

            dropdownKeys.Add(dropdownKey);
        }
    }

    SeedDropdown(
        "PurchasedFrom",
        "Amazon",
        "eBay",
        "Royal Mint",
        "Trading 212",
        "Currys",
        "Apple",
        "CEX",
        "Facebook Marketplace",
        "Vinted",
        "Other");

    SeedDropdown(
        "InsuranceProvider",
        "Home Insurance",
        "Contents Insurance",
        "Gadget Insurance",
        "AppleCare",
        "Royal Mint",
        "Other");

    SeedDropdown(
        "Manufacturer",
        "Apple",
        "Sony",
        "Nintendo",
        "Microsoft",
        "Canon",
        "Nikon",
        "DJI",
        "Royal Mint",
        "Nike",
        "Adidas",
        "Samsung",
        "Other");

    SeedDropdown(
        "Brand",
        "Apple",
        "PlayStation",
        "Xbox",
        "Nintendo",
        "Royal Mint",
        "Nike",
        "Adidas",
        "The North Face",
        "Other");

    SeedDropdown(
        "CaseType",
        "Boxed",
        "Loose",
        "Original Box",
        "Protective Case",
        "Dust Bag",
        "Clothes Hanger",
        "Other");

    SeedDropdown(
        "MediaFormat",
        "Disc",
        "Cartridge",
        "Digital",
        "Paper",
        "Fabric",
        "Leather",
        "Metal",
        "Other");

    SeedDropdown(
        "Instruction",
        "Included",
        "Missing",
        "Digital Manual",
        "Certificate Included",
        "Not applicable");

    await db.SaveChangesAsync();
}
