namespace FFXI_LogAnalyzer.App;

public sealed class ActorFilterService
{
    public IReadOnlyList<ActorSummaryViewModel> FilterActorSummaries(
        IEnumerable<ActorSummaryViewModel> summaries,
        IEnumerable<ActorVisibilityViewModel> visibilities)
    {
        var visibleActors = CreateVisibleActorSet(visibilities);
        return summaries
            .Where(summary => visibleActors.Contains(summary.Actor))
            .ToArray();
    }

    public IReadOnlyList<ActionSummaryViewModel> FilterActionSummaries(
        IEnumerable<ActionSummaryViewModel> summaries,
        IEnumerable<ActorVisibilityViewModel> visibilities)
    {
        var visibleActors = CreateVisibleActorSet(visibilities);
        return summaries
            .Where(summary => visibleActors.Contains(summary.Actor))
            .ToArray();
    }

    private static HashSet<string> CreateVisibleActorSet(IEnumerable<ActorVisibilityViewModel> visibilities)
    {
        return visibilities
            .Where(visibility => visibility.IsVisible)
            .Select(visibility => visibility.Actor)
            .ToHashSet(StringComparer.Ordinal);
    }
}
