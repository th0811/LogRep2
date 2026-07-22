using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class ActorNameClassifier
{
    public static string NormalizePcName(string name)
    {
        return NormalizeRegisteredName(name);
    }

    public static string NormalizeRegisteredName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(trimmed[0])
            + trimmed[1..].ToLowerInvariant();
    }

    public static bool IsPcNameCandidate(string name)
    {
        return PcNameRegex().IsMatch(name);
    }

    public ActorNameKind Classify(
        string actor,
        IEnumerable<string> registeredPcNames,
        IEnumerable<string> registeredNpcNames)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(registeredPcNames);
        ArgumentNullException.ThrowIfNull(registeredNpcNames);

        var normalized = NormalizeRegisteredName(actor);
        var pcNames = registeredPcNames
            .Select(NormalizeRegisteredName)
            .ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        var npcNames = registeredNpcNames
            .Select(NormalizeRegisteredName)
            .ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        if (npcNames.Contains(normalized))
        {
            return ActorNameKind.RegisteredNpc;
        }

        if (pcNames.Contains(normalized))
        {
            return ActorNameKind.RegisteredPc;
        }

        return IsPcNameCandidate(actor)
            ? ActorNameKind.PcCandidate
            : ActorNameKind.Unknown;
    }

    [GeneratedRegex("^[A-Za-z]{1,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex PcNameRegex();
}
