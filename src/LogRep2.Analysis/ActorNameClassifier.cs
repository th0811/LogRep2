using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class ActorNameClassifier
{
    public static string NormalizePcName(string name)
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

        var trimmed = actor.Trim();
        var normalized = NormalizePcName(actor);
        var pcNames = registeredPcNames.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        var npcNames = registeredNpcNames.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        if (npcNames.Contains(trimmed))
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
