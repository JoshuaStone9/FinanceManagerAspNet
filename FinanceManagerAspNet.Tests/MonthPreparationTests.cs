using FinanceManagerAspNet.Models;
using Xunit;

namespace FinanceManagerAspNet.Tests;

public sealed class MonthPreparationTests
{
    [Fact]
    public void ExistingDestinationNamesAreExcludedIgnoringCaseAndWhitespace()
    {
        var entries = new[] { Item(1, " Rent ", true), Item(2, "Food", false) };
        var result = MonthPreparationItem.Missing(entries, ["rent"]);
        Assert.Equal("Food", Assert.Single(result).Name);
    }

    [Fact]
    public void LatestRecordWinsWithoutTurningOneOffIntoRecurring()
    {
        var entries = new[] { Item(1, "Rent", true), Item(2, " rent ", false) with { Amount = 900 } };
        var item = Assert.Single(MonthPreparationItem.Missing(entries, []));
        Assert.Equal(900, item.Amount);
        Assert.False(item.IsRecurring);
    }

    [Fact]
    public void DefaultsSeparateIncomeFromAllocationsAndLeaveOneOffsUnselected()
    {
        var model = new PrepareNextMonthViewModel { Sections = [
            new() { Source = "income", Items = [Item(1, "Salary", true) with { Source = "income", Amount = 2000 }] },
            new() { Source = "bills", Items = [Item(2, "Rent", true) with { Amount = 800 }, Item(3, "Repair", false)] },
            new() { Source = "savings", Items = [Item(4, "Holiday", true) with { Source = "savings", Amount = 100 }] }
        ] };
        Assert.Equal(4, model.EntryCount);
        Assert.Equal(3, model.RecurringEntryCount);
        Assert.Equal(2000, model.IncomeTotal);
        Assert.Equal(900, model.AllocationTotal);
    }

    [Theory]
    [InlineData(2027, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2027, 1, 31)]
    public void CopyDateRetainsDayAndCapsShortMonths(int year, int month, int day)
    {
        var item = Item(1, "Rent", true) with { Date = new DateTime(2026, 12, 31) };
        Assert.Equal(new DateTime(year, month, day), item.DateIn(new DateTime(year, month, 1)));
    }

    [Fact]
    public void SelectionKeysDistinguishIncomeAndPaymentIds()
    {
        Assert.NotEqual(Item(1, "Rent", true).Key, (Item(1, "Salary", true) with { Source = "income" }).Key);
    }

    private static MonthPreparationItem Item(int id, string name, bool recurring) =>
        new("bills", id, name, 10, new DateTime(2026, 9, 20), recurring);
}
