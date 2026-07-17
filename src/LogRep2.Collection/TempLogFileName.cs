namespace FfxiTempLogCollector.Core;

public sealed record TempLogFileName(
    string FileName,
    int WindowId,
    int RotationIndex);
