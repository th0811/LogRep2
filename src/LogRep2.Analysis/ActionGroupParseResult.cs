namespace FFXI_LogAnalyzer.Core;

public sealed class ActionGroupParseResult
{
    private ActionGroupParseResult(
        ParsedActionGroup? parsed,
        UnparsedActionGroup? unparsed)
    {
        Parsed = parsed;
        Unparsed = unparsed;
    }

    public ParsedActionGroup? Parsed { get; }

    public UnparsedActionGroup? Unparsed { get; }

    public bool IsParsed => Parsed is not null;

    public static ActionGroupParseResult FromParsed(ParsedActionGroup parsed)
    {
        return new ActionGroupParseResult(parsed, null);
    }

    public static ActionGroupParseResult FromUnparsed(UnparsedActionGroup unparsed)
    {
        return new ActionGroupParseResult(null, unparsed);
    }
}
