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
        ItemStatus.EasyAccess => "Easy access",
        ItemStatus.NearbyStorage => "Nearby storage",
        ItemStatus.StoredAway => "Stored away",
        ItemStatus.HardToReach => "Hard to reach",
        ItemStatus.LoanedOut => "Loaned out",
        ItemStatus.Sold => "Sold",
        ItemStatus.Disposed => "Disposed",
        ItemStatus.Missing => "Missing",
        _ => status.ToString()
    };
}
