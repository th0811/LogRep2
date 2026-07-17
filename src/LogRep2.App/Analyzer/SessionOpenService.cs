using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class SessionOpenService
{
    private readonly SessionFolderLoader _loader = new();

    public LoadSessionResult Open(string folderPath)
    {
        return _loader.Load(folderPath);
    }
}
