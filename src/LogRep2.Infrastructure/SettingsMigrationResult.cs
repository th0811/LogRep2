namespace LogRep2.Infrastructure;

public sealed record SettingsMigrationResult(
    LogRep2Settings Settings,
    bool Created,
    bool MigratedCollectionSettings,
    bool MigratedAnalysisSettings,
    IReadOnlyList<string> Warnings);

