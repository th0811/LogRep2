namespace FfxiTempLogCollector.App;

public enum CliExitCode
{
    Success = 0,
    InvalidArguments = 2,
    ConfigError = 3,
    CollectionError = 4,
    NotRunning = 5,
    UnexpectedError = 10,
}
