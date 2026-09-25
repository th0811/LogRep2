namespace LogRep2.Infrastructure;

public sealed class SessionSelectionState
{
    public string FolderPath { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool Matches(string folderPath, string sessionId)
    {
        return string.Equals(
                FolderPath,
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath)),
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(SessionId, sessionId, StringComparison.Ordinal);
    }
}
