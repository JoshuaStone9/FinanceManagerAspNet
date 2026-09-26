using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoveInPlanner.Data;

#nullable disable

namespace MoveInPlanner.Data.Migrations;

/// <summary>
/// Imports the populated rows from Book1.xlsx into the normalised database model.
/// This is intentionally a data-only migration: it does not change the schema.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260801170000_ImportExistingSpreadsheetItems")]
public partial class ImportExistingSpreadsheetItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""

DECLARE @Now datetime2 = '2026-08-01T16:30:00Z';
DECLARE @ItemId int;
DECLARE @PreferredChoiceId int;

-- Slow Cooker: premium-versus-budget comparison. The budget choice is preferred.
INSERT INTO HouseholdItems
    (Name, CategoryId, ChoiceType, Priority, Status, QuantityRequired, TargetBudget,
     IsEssentialForMoveIn, GeneralNotes, DecisionReason, FullComparisonResponse,
     CreatedAtUtc, UpdatedAtUtc)
VALUES
    (N'Slow Cooker', 8, 2, 3, 3, 1, 110.00, 1,
     N'Cheaper option is possibly better for us because it provides enough slow-cooking functionality for our likely use. The Ninja is more powerful and may heat, sear and brown more effectively, but the budget option is currently preferred.',
     N'Russell Hobbs is currently preferred because it covers the expected use at a much lower price.',
     N'Both are good multicookers, but they are aimed at slightly different buyers.

Feature	Ninja Foodi PossibleCooker MC1001UK	Russell Hobbs Good to Go 28270
Capacity	8L	6.5L
Power	1200W	750W
Cooking functions	8	8
Hob-safe searing	Better performance	Available
Oven-safe pot	Up to 240°C	Oven-safe for roasting and finishing
Keep warm	Up to 12 hours	Yes

The Ninja offers greater power, a larger capacity, stronger searing and more flexible oven integration. The Russell Hobbs still covers the everyday tasks most likely to matter: slow cooking, steaming, rice, sous vide, roasting, searing, boiling and keeping food warm.

Choose the Russell Hobbs when the main goal is affordable slow cooking for a couple or small family. Choose the Ninja when larger batches, frequent one-pot cooking, stronger searing and oven finishing justify the additional cost.', @Now, @Now);
SET @ItemId = CONVERT(int, SCOPE_IDENTITY());

INSERT INTO ProductChoices
    (HouseholdItemId, Name, Tier, Price, Quantity, ProductUrl, Retailer, Notes,
     IsPreferred, IsPurchased, PriceCheckedOn, CreatedAtUtc)
VALUES
    (@ItemId, N'Ninja Foodi PossibleCooker', 3, 110.00, 1, N'https://www.amazon.co.uk/Ninja-PossibleCooker-SlowCooker-IntegratedSpoon-MC1001UK/dp/B0CFYMZF81/ref=sr_1_2?crid=1VVRU9Y528XYX&dib=eyJ2IjoiMSJ9.pbvQC0ETFqDp7eVFK8rO1Ll2KTfcycnw-f0ujEPfem7hZ4oMb3eIDRX222Tbb6vDNMnWjxcef2SWPgcNfLmgXMvfz6Kl98ZVVte8y7I4SURm_9MTjdxLWcJFIBnwTUNFxzGNu_ggsuRU9nYaXcd5m5gKtnLvUZM32PlYv09saLxar-mLXoyq3y6OIpjnghWIPEAFasBzFlG9u9rYTb-FD08tOmD_B7hY1RwNy_wMP1A.VDXFCqRJnIqkBsNsf2WfTkQNGiAVVrp5gbaqiJYbDjE&dib_tag=se&keywords=ninja%2Bfoodi%2B8%2Bin%2B1&qid=1785597929&sprefix=ninja%2Bfoodi%2B8%2Bin%2B1%2Caps%2C115&sr=8-2&th=1', N'Amazon UK', N'Premium option from the spreadsheet.', 0, 0, @Now, @Now),
    (@ItemId, N'Russell Hobbs Good-to-Go 6.5L Electric Multicooker', 2, 39.00, 1, N'https://www.amazon.co.uk/Russell-Hobbs-28270-Good-Multicooker/dp/B096Y9XNLF/ref=sr_1_5?crid=ST260KMVE7FE&dib=eyJ2IjoiMSJ9.q0SNa7Z96cujVzxKhJwQzXYJnKzMo8l15RcwA4J6nK752wppCDWij4NHWzKHgOy707NLtBIE5VcRjU8iDeEFZDWyNugsfM_gdPh4JrKi6fDjDQOJkwzo-5UPH5XoTPdoPM4xOXpLBpdD0utGXwPSLmJZScDkqcZgfLHi7PSFM_mkFbdy3bqZd_xJvyGX5wbkh917cvgNrOUMcrG9NMBYoRK-hlKM1IaqZbzO392X4RI.7snZ71CuqlUrk-UvEJR3YB1aN8ZSR9NXUM1yBIL93NU&dib_tag=se&keywords=slow%2Bcooker&qid=1785597679&sprefix=slow%2Bcooke%2Caps%2C200&sr=8-5&th=1', N'Amazon UK', N'Cheaper option and current preferred choice.', 1, 0, @Now, @Now);
SET @PreferredChoiceId = CONVERT(int, SCOPE_IDENTITY());
UPDATE HouseholdItems SET SelectedProductChoiceId = @PreferredChoiceId WHERE Id = @ItemId;

-- Laundry Sorter: one selected product.
INSERT INTO HouseholdItems
    (Name, CategoryId, ChoiceType, Priority, Status, QuantityRequired, TargetBudget,
     IsEssentialForMoveIn, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (N'Laundry Sorter', 3, 1, 2, 3, 1, 25.49, 0, @Now, @Now);
SET @ItemId = CONVERT(int, SCOPE_IDENTITY());
INSERT INTO ProductChoices
    (HouseholdItemId, Name, Tier, Price, Quantity, ProductUrl, Retailer, IsPreferred,
     IsPurchased, PriceCheckedOn, CreatedAtUtc)
VALUES
    (@ItemId, N'SOLEDI Laundry Basket 3 Section', 1, 25.49, 1, N'https://www.amazon.co.uk/SOLEDI-Laundry-Collapsible-Foldable-Livingroom/dp/B0D2W6PPPP/ref=sr_1_6?crid=29NHZBMSF0F80&dib=eyJ2IjoiMSJ9.FuFZdPiPZMjm1nokDNSxRaO306FJtc9qGru_kKYq9vZkTfKAYPYpFu_HL3ke1KVkFUhwrvaZrmrJsU_xNM_6GCOWieItWdEy0DYeiFKBSZOm0NV7WSKcHVgjuZTm1Qfjpd1w0XymWud31huXQihRdvOzFJ87FOJo6QOw1xbutA1crUaqcVGJdhY8FzV3yj57cXeoAlmGt7u3ebfEg54K6BB0da1plvoipfMNxw-K7WTvMRjGDza76v3GvyzSqKoWYFeO2RERHZU63wFgsQid1fSb3IvSDpZt5FQEMv_ZLsE.fQyQLNvwD423f-AOyXqZqsrXE7nunIV1MXmKDGffKgM&dib_tag=se&keywords=laundry%2Bsorter&qid=1785600282&sprefix=laundry%2Bsorte%2Caps%2C192&sr=8-6&th=1', N'Amazon UK', 1, 0, @Now, @Now);
SET @PreferredChoiceId = CONVERT(int, SCOPE_IDENTITY());
UPDATE HouseholdItems SET SelectedProductChoiceId = @PreferredChoiceId WHERE Id = @ItemId;

-- Kitchen Scissors: one selected product.
INSERT INTO HouseholdItems
    (Name, CategoryId, ChoiceType, Priority, Status, QuantityRequired, TargetBudget,
     IsEssentialForMoveIn, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (N'Kitchen Scissors', 1, 1, 2, 3, 1, 6.99, 1, @Now, @Now);
SET @ItemId = CONVERT(int, SCOPE_IDENTITY());
INSERT INTO ProductChoices
    (HouseholdItemId, Name, Tier, Price, Quantity, ProductUrl, Retailer, IsPreferred,
     IsPurchased, PriceCheckedOn, CreatedAtUtc)
VALUES
    (@ItemId, N'Magnificent Kitchen Scissor', 1, 6.99, 1, N'https://www.amazon.co.uk/Magnificent-Kitchen-Scissor-Scissors-Multi-Functional/dp/B09CNFNZ3D/ref=sr_1_8?crid=3PA4AD2ORMMT7&dib=eyJ2IjoiMSJ9.TOox30TTSMX36Opzn7_YXlDsNo46xyDx-VAG4objH3yyH8Dg7WYJHtaBEta45mXzzl5Z4U-Bz2HlsbjXc4Kmnh5wpoguzS-rngksM6SRZNGI2ShORn6EtEY7o9TKgztjyKmzM6kqn5npdFHJJlprBlfvCBqF8Wccd-i4nW4w_nR1HQcg0kP8ufz8DF7vNaGlH5aTgxJCausm7UL8AeNVgjbFjoke_8zesZbVfVU1GRMtkATfd74aA2dL9rg1k2vcGuqVt5YQT1dPcUC4yxr1RTJPpXIzk3irpUDyPWbuLfk.8eF5-81dboYDqDyw_jfycCJ10o_ss1UuRQU2yFfMb3s&dib_tag=se&keywords=scissors&qid=1785600450&sprefix=scissors%2Caps%2C306&sr=8-8&th=1', N'Amazon UK', 1, 0, @Now, @Now);
SET @PreferredChoiceId = CONVERT(int, SCOPE_IDENTITY());
UPDATE HouseholdItems SET SelectedProductChoiceId = @PreferredChoiceId WHERE Id = @ItemId;

-- Ice Cream Scoop: premium-versus-budget comparison; no final choice recorded yet.
INSERT INTO HouseholdItems
    (Name, CategoryId, ChoiceType, Priority, Status, QuantityRequired, TargetBudget,
     IsEssentialForMoveIn, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (N'Ice Cream Scoop', 1, 2, 1, 2, 1, 20.99, 0, @Now, @Now);
SET @ItemId = CONVERT(int, SCOPE_IDENTITY());
INSERT INTO ProductChoices
    (HouseholdItemId, Name, Tier, Price, Quantity, ProductUrl, Retailer, Notes,
     IsPreferred, IsPurchased, PriceCheckedOn, CreatedAtUtc)
VALUES
    (@ItemId, N'Heated Ice Cream Scoop with LED Display', 3, 20.99, 1, N'https://www.amazon.co.uk/Display-Icecream-4-Level-Adjustable-Detachable/dp/B0GVDRG2Q4/ref=sr_1_14_sspa?crid=B2F7JF1E4K2V&dib=eyJ2IjoiMSJ9.p1YYPhGKcbn5HUV9aPTRAxwc9f7IsFFe4AbC5tCvrWCBVp6V5dniGvn5nffivCFT8FAUNGdxfRzOpOEK1L8odeTXAOW2pxqLjBdM-68OcZ57V8xCQ3aapXMkmEjNmEdk1UXDNSgr4DLMhm2ocNWdrhtFEJaIUp1z-rZKwHXfI6ytVdaHWw8VDsWk-jLw9bYRPk3DKeI8qNfzLtlg1Ug9K1_lIt2y4I8sOOWXTH3DChqJy1XfC-YuRTFgEBtoFl8WyLYofkDifYe4WhZc4WsfHykqxOVzW-4ntYFcN6SE2f4.kDIbEqaby8XeJ3o2tyZsMqvX0gmIFGdp20SeimdgBVI&dib_tag=se&keywords=ice+cream+scoop&qid=1785600686&sprefix=ice+cream%2Caps%2C133&sr=8-14-spons&aref=h4PXhxjxX7&sp_csd=d2lkZ2V0TmFtZT1zcF9tdGY&psc=1', N'Amazon UK', N'Premium option from the spreadsheet.', 0, 0, @Now, @Now),
    (@ItemId, N'One Piece Aluminium Scooper Spoon', 2, 5.99, 1, N'https://www.amazon.co.uk/Nonstick-Anti-Freeze-Aluminum-Scooper-Durable/dp/B095Z7MFKN/ref=sr_1_6?crid=B2F7JF1E4K2V&dib=eyJ2IjoiMSJ9.p1YYPhGKcbn5HUV9aPTRAxwc9f7IsFFe4AbC5tCvrWCBVp6V5dniGvn5nffivCFT8FAUNGdxfRzOpOEK1L8odeTXAOW2pxqLjBdM-68OcZ57V8xCQ3aapXMkmEjNmEdk1UXDNSgr4DLMhm2ocNWdrhtFEJaIUp1z-rZKwHXfI6ytVdaHWw8VDsWk-jLw9bYRPk3DKeI8qNfzLtlg1Ug9K1_lIt2y4I8sOOWXTH3DChqJy1XfC-YuRTFgEBtoFl8WyLYofkDifYe4WhZc4WsfHykqxOVzW-4ntYFcN6SE2f4.kDIbEqaby8XeJ3o2tyZsMqvX0gmIFGdp20SeimdgBVI&dib_tag=se&keywords=ice%2Bcream%2Bscoop&qid=1785600865&sprefix=ice%2Bcream%2Caps%2C133&sr=8-6&th=1', N'Amazon UK', N'Budget option from the spreadsheet.', 0, 0, @Now, @Now);

-- Oven mitts and heat-resistant surfaces appeared as an unnamed continuation row in Excel.
-- They are represented as one multi-buy requirement with two product choices.
INSERT INTO HouseholdItems
    (Name, CategoryId, ChoiceType, Priority, Status, QuantityRequired, TargetBudget,
     IsEssentialForMoveIn, GeneralNotes, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (N'Oven Mitts and Heat-Resistant Trivets', 1, 4, 3, 2, 2, 19.00, 1,
     N'The Excel sheet recorded a combined target of £19 for the two linked products. Individual prices were not supplied, so each choice is stored with a zero placeholder and the combined budget remains on the household item.',
     @Now, @Now);
SET @ItemId = CONVERT(int, SCOPE_IDENTITY());
INSERT INTO ProductChoices
    (HouseholdItemId, Name, Tier, Price, Quantity, ProductUrl, Retailer, Notes,
     IsPreferred, IsPurchased, PriceCheckedOn, CreatedAtUtc)
VALUES
    (@ItemId, N'Heat-Resistant Silicone Trivets', 1, 0.00, 1, N'https://www.amazon.co.uk/Resistant-Silicone-Non-Slip-Surfaces-Microwave/dp/B08QRVMLJ7/ref=sr_1_16?crid=1IHMAFOW7GYSJ&dib=eyJ2IjoiMSJ9.qmhorfbDQiRqeGCtehUXYXv3MpAlzsteV3_L0FlYOSAR18i1J6azEUw5l86rx6K5tBvTYZI8NnVTh-givBO9GTNDAQvZYtFP2xifVzrRQF4Mo8MwXxi4LWD4aKVtc9OXEajTDFa3yxV7muDieuPe4aoogf8USYRkYwjGE7az9OE6oFqHcNeiXt02Yy3YWJNF1uToLfwXS7NyaRge4-7mmOO-YZY2ycq8P-mLZr4uFxDduYIXGcgYFnX_iAPPtCE9PvZviaShbX5L_Y6-MO1x8C79NnN8taqHMEPfSO0PIzc.yxvM2YTpPC0NHBb1WiVIZj1TRs48eIxowfHO5W_9CjU&dib_tag=se&keywords=oven%2Bmitts&qid=1785601028&sprefix=oven%2Bmitts%2Caps%2C130&sr=8-16&th=1', N'Amazon UK', N'Part of the £19 combined budget; individual price was not recorded.', 0, 0, @Now, @Now),
    (@ItemId, N'AUAUY Heat-Resistant Oven Mitts', 1, 0.00, 1, N'https://www.amazon.co.uk/AUAUY-Resistant-Non-Slip-Cooking-Kitchen/dp/B0F3D1PM92/ref=sr_1_94?crid=1IHMAFOW7GYSJ&dib=eyJ2IjoiMSJ9.xeBdPMJjSYBLU71nA4cKxqIwzElvGd7hukCNgYNnZcpknFMcjyt7wVSpq7udUHOjiH99ucIWChjOKHoUOH08gLz-PcsTJHLwqe82BdR8x1wpBMus0njQBX3wdYj-q2wdFZ-aYBzqTXqEODrp3_q3NFDcA6Ae3TQb3YeKr1Hi8ujbHZEGRQ5BwAuaIr4-9Na4h3Xg1xxzYJibKZLHVhcVZVn0-ePsbMC9S-CT8jd23JKeygpD5POTYLcJlEH0oh97zLPOnykjuXAFgn4pfnIYrA64lUu3wR7441p5xHhgwec.X04AqWs4NshgJ9v8MFdVbaODjI08neV5qeI7sK31C_U&dib_tag=se&keywords=oven%2Bmitts&qid=1785601125&sprefix=oven%2Bmitts%2Caps%2C130&sr=8-94&xpid=TGR-qT5cwxc4b&th=1', N'Amazon UK', N'Part of the £19 combined budget; individual price was not recorded.', 0, 0, @Now, @Now);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DELETE FROM ProductChoices
WHERE HouseholdItemId IN
(
    SELECT Id
    FROM HouseholdItems
    WHERE Name IN
    (
        N'Slow Cooker',
        N'Laundry Sorter',
        N'Kitchen Scissors',
        N'Ice Cream Scoop',
        N'Oven Mitts and Heat-Resistant Trivets'
    )
);

DELETE FROM HouseholdItems
WHERE Name IN
(
    N'Slow Cooker',
    N'Laundry Sorter',
    N'Kitchen Scissors',
    N'Ice Cream Scoop',
    N'Oven Mitts and Heat-Resistant Trivets'
);
""");
    }
}
