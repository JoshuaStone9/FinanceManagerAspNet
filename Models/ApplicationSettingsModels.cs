using System.ComponentModel.DataAnnotations;

namespace FinanceManagerAspNet.Models;

public sealed class ApplicationSettingsViewModel
{
    public FinanceSettingsInput Finance { get; set; } = new();
    public AppearanceSettingsInput Appearance { get; set; } = new();
    public SecuritySettingsInput Security { get; set; } = new();
    public SettingsDiagnostics Diagnostics { get; set; } = new();
    public string AppVersion { get; set; } = "2.0";
    public string DatabaseName { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
}

public sealed class FinanceSettingsInput
{
    [Range(0, 10000000)] public decimal EmergencyFundBaseline { get; set; } = 12000m;
    [Range(0, 10000000)] public decimal DefaultMonthlyIncome { get; set; } = 3500m;
    public string ForecastMethod { get; set; } = "Last3Months";
    public string CurrencyCode { get; set; } = "GBP";
    public bool ShowPence { get; set; } = true;
    public bool PrepareRecurringEntriesByDefault { get; set; } = true;
    public bool PauseCompletedPotsAutomatically { get; set; }
}

public sealed class AppearanceSettingsInput
{
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "Purple";
    public bool CompactMode { get; set; }
    public bool SidebarCollapsedByDefault { get; set; }
}

public sealed class SecuritySettingsInput
{
    [Range(1, 365)] public int RememberDays { get; set; } = 365;
    [Range(5, 240)] public int SessionTimeoutMinutes { get; set; } = 60;
    public bool ReadOnlyWhenLoggedOut { get; set; } = true;
}

public sealed class ChangePasswordInput
{
    [Required, MinLength(8)] public string NewPassword { get; set; } = string.Empty;
    [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class SettingsDiagnostics
{
    public bool DatabaseConnected { get; set; }
    public int FinanceTableCount { get; set; }
    public int SettingsCount { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public string Status => DatabaseConnected ? "Healthy" : "Needs attention";
}
