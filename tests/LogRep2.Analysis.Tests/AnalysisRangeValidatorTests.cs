using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class AnalysisRangeValidatorTests
{
    [Fact]
    public void Validate_RejectsEndMarkerBeforeStartMarker()
    {
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.FromMarker(CreateMarker(20)),
            AnalysisEndpoint.FromMarker(CreateMarker(10)));

        var errors = new AnalysisRangeValidator().Validate(selection);

        Assert.Single(errors);
        Assert.Contains("終了markerは開始markerより後ろ", errors[0]);
    }

    [Fact]
    public void Validate_AllowsEndMarkerAfterStartMarker()
    {
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.FromMarker(CreateMarker(10)),
            AnalysisEndpoint.FromMarker(CreateMarker(20)));

        var errors = new AnalysisRangeValidator().Validate(selection);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AllowsLogStartAndLogEnd()
    {
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.LogStart,
            AnalysisEndpoint.LogEnd);

        var errors = new AnalysisRangeValidator().Validate(selection);

        Assert.Empty(errors);
    }

    private static MarkerRecord CreateMarker(long order)
    {
        var record = new CanonicalRecord
        {
            Order = order,
            IsMarker = true,
            MarkerKeyword = "#marker"
        };

        return new MarkerRecord(order, "#marker", "#marker", "[12:00]", null, record);
    }
}
