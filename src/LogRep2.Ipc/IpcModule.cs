namespace FfxiTempLogCollector.Ipc;

public static class IpcModule
{
    public const string MutexName =
        @"Local\LogRep2.Gui.Singleton";

    public const string PipeName =
        "LogRep2.CommandPipe";
}
