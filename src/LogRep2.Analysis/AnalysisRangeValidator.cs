namespace FFXI_LogAnalyzer.Core;

public sealed class AnalysisRangeValidator
{
    public IReadOnlyList<string> Validate(AnalysisRangeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var errors = new List<string>();

        ValidateStart(selection.Start, errors);
        ValidateEnd(selection.End, errors);
        ValidateMarkerOrder(selection, errors);

        return errors;
    }

    public bool IsValid(AnalysisRangeSelection selection)
    {
        return Validate(selection).Count == 0;
    }

    private static void ValidateStart(AnalysisEndpoint endpoint, ICollection<string> errors)
    {
        if (endpoint.Type is not (AnalysisEndpointType.LogStart or AnalysisEndpointType.Marker))
        {
            errors.Add("開始ポイントにはログ先頭またはmarkerを指定してください。");
        }

        ValidateMarkerEndpoint(endpoint, errors);
    }

    private static void ValidateEnd(AnalysisEndpoint endpoint, ICollection<string> errors)
    {
        if (endpoint.Type is not (AnalysisEndpointType.Marker or AnalysisEndpointType.LogEnd))
        {
            errors.Add("終了ポイントにはmarkerまたはログ最後尾を指定してください。");
        }

        ValidateMarkerEndpoint(endpoint, errors);
    }

    private static void ValidateMarkerEndpoint(AnalysisEndpoint endpoint, ICollection<string> errors)
    {
        if (endpoint.Type != AnalysisEndpointType.Marker)
        {
            return;
        }

        if (endpoint.Marker is null)
        {
            errors.Add("markerポイントにはmarker情報が必要です。");
            return;
        }

        if (endpoint.Marker.Order is null)
        {
            errors.Add("markerポイントにはorderが必要です。");
        }
    }

    private static void ValidateMarkerOrder(AnalysisRangeSelection selection, ICollection<string> errors)
    {
        if (selection.Start.Type != AnalysisEndpointType.Marker ||
            selection.End.Type != AnalysisEndpointType.Marker ||
            selection.Start.Marker?.Order is not { } startOrder ||
            selection.End.Marker?.Order is not { } endOrder)
        {
            return;
        }

        if (endOrder <= startOrder)
        {
            errors.Add("終了markerは開始markerより後ろのmarkerを指定してください。");
        }
    }
}
