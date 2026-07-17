namespace FfxiTempLogCollector.Core;

public sealed class StateStore
{
    public const string StateFileName = "state.json";

    public CollectorState Load(string sessionDirectory)
    {
        return JsonFileSerializer.Load<CollectorState>(
            GetStatePath(sessionDirectory));
    }

    public void Save(string sessionDirectory, CollectorState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        JsonFileSerializer.Save(GetStatePath(sessionDirectory), state);
    }

    private static string GetStatePath(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);

        return Path.Combine(sessionDirectory, StateFileName);
    }
}
