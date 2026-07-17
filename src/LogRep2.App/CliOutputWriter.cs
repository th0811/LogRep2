using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FfxiTempLogCollector.App;

public sealed class CliOutputWriter
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public CliOutputWriter(TextWriter output, TextWriter error)
    {
        _output = output
            ?? throw new ArgumentNullException(nameof(output));
        _error = error
            ?? throw new ArgumentNullException(nameof(error));
    }

    public static CliOutputWriter CreateConsole()
    {
        if (OperatingSystem.IsWindows())
        {
            AttachConsole(AttachParentProcess);
        }

        Console.OutputEncoding = Encoding.UTF8;
        var output = new StreamWriter(
            Console.OpenStandardOutput(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        var error = new StreamWriter(
            Console.OpenStandardError(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        Console.SetOut(output);
        Console.SetError(error);
        return new CliOutputWriter(Console.Out, Console.Error);
    }

    public void WriteLine(string message)
    {
        _output.WriteLine(message);
        _output.Flush();
    }

    public void WriteError(string message)
    {
        _error.WriteLine(message);
        _error.Flush();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);
}
