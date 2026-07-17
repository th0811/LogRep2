namespace FfxiTempLogCollector.Core;

public sealed class TempLogFileReader
{
    public TempLogFileReadResult Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return new TempLogFileReadResult();
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var content = memoryStream.ToArray();
            var fileInfo = new FileInfo(path);

            return new TempLogFileReadResult
            {
                Exists = true,
                Snapshot = new FileSnapshot
                {
                    Path = Path.GetFullPath(path),
                    FileName = Path.GetFileName(path),
                    LastWriteTime = fileInfo.LastWriteTimeUtc,
                    FileSize = content.LongLength,
                    FileHash = HashUtil.ComputeSha1(content),
                    Content = content,
                },
            };
        }
        catch (IOException exception)
        {
            return CreateErrorResult(path, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return CreateErrorResult(path, exception);
        }
    }

    private static TempLogFileReadResult CreateErrorResult(
        string path,
        Exception exception)
    {
        return new TempLogFileReadResult
        {
            Exists = true,
            Error = $"ログファイルを読み取れませんでした: {path} ({exception.Message})",
        };
    }
}
