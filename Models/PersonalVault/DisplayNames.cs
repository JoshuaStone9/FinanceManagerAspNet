namespace FinanceManagerAspNet.Models;

public static class DisplayNames
{
    public static string ToDisplayName(this ItemCondition condition) => condition switch
    {
        ItemCondition.None => "None",
        ItemCondition.New => "New",
        ItemCondition.LikeNew => "Like new",
        ItemCondition.Good => "Good",
        ItemCondition.Fair => "Fair",
        ItemCondition.Poor => "Poor",
        _ => condition.ToString()
    };

    public static string ToDisplayName(this ItemStatus status) => status switch
    {
        ItemStatus.EasilyAccessible => "Easily accessible",
        ItemStatus.AccessibleWithNotice => "Accessible with notice",
        ItemStatus.StoredSafely => "Stored safely",
        ItemStatus.HardToAccess => "Hard to access",
        ItemStatus.InUse => "In use",
        ItemStatus.OnLoan => "On loan",
        ItemStatus.NeedsRepair => "Needs repair",
        ItemStatus.Sold => "Sold",
        ItemStatus.Disposed => "Disposed",
        ItemStatus.Missing => "Missing",
        ItemStatus.BeingDelivered => "Being delivered",
        _ => status.ToString()
    };
}
