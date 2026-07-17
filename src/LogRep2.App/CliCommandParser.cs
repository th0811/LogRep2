namespace FfxiTempLogCollector.App;

public sealed class CliCommandParser
{
    public CliParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return Failure("CLIコマンドを指定してください。");
        }

        var tokens = args.ToList();
        string? configPath = null;

        for (var index = 0; index < tokens.Count; index++)
        {
            if (!tokens[index].Equals(
                    "--config",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= tokens.Count)
            {
                return Failure(
                    "--config の後に設定ファイルパスを指定してください。");
            }

            configPath = tokens[index + 1];
            tokens.RemoveRange(index, 2);
            break;
        }

        if (tokens.Count == 0)
        {
            return Failure("CLIコマンドを指定してください。");
        }

        var commandName = tokens[0].ToLowerInvariant();

        return commandName switch
        {
            "help" or "--help" or "-h" => ParseSimple(
                CliCommandKind.Help,
                tokens,
                configPath),
            "start" => ParseCollection(
                CliCommandKind.Start,
                tokens,
                configPath,
                allowMinimized: true),
            "stop" => ParseSimple(
                CliCommandKind.Stop,
                tokens,
                configPath),
            "status" => ParseSimple(
                CliCommandKind.Status,
                tokens,
                configPath),
            "once" => ParseCollection(
                CliCommandKind.Once,
                tokens,
                configPath,
                allowMinimized: false),
            "config" => ParseConfig(tokens, configPath),
            _ => Failure($"不明なコマンドです: {tokens[0]}"),
        };
    }

    private static CliParseResult ParseSimple(
        CliCommandKind kind,
        IReadOnlyList<string> tokens,
        string? configPath)
    {
        if (tokens.Count != 1)
        {
            return Failure("このコマンドに追加の引数は指定できません。");
        }

        return Success(
            new CliCommand
            {
                Kind = kind,
                ConfigPath = configPath,
            });
    }

    private static CliParseResult ParseCollection(
        CliCommandKind kind,
        IReadOnlyList<string> tokens,
        string? configPath,
        bool allowMinimized)
    {
        string? tempDirectory = null;
        string? outputDirectory = null;
        var minimized = false;

        for (var index = 1; index < tokens.Count; index++)
        {
            var option = tokens[index].ToLowerInvariant();

            if (option == "--minimized" && allowMinimized)
            {
                minimized = true;
                continue;
            }

            if (option is "--temp-dir" or "--output-dir")
            {
                if (index + 1 >= tokens.Count)
                {
                    return Failure(
                        $"{tokens[index]} の値を指定してください。");
                }

                var value = tokens[++index];

                if (option == "--temp-dir")
                {
                    tempDirectory = value;
                }
                else
                {
                    outputDirectory = value;
                }

                continue;
            }

            return Failure($"不明なオプションです: {tokens[index]}");
        }

        return Success(
            new CliCommand
            {
                Kind = kind,
                ConfigPath = configPath,
                TempDirectory = tempDirectory,
                OutputDirectory = outputDirectory,
                Minimized = minimized,
            });
    }

    private static CliParseResult ParseConfig(
        IReadOnlyList<string> tokens,
        string? configPath)
    {
        if (tokens.Count < 2)
        {
            return Failure(
                "config get、config set、config path のいずれかを指定してください。");
        }

        var operation = tokens[1].ToLowerInvariant();

        if (operation == "path" && tokens.Count == 2)
        {
            return Success(
                new CliCommand
                {
                    Kind = CliCommandKind.ConfigPath,
                    ConfigPath = configPath,
                });
        }

        if (operation == "get" && tokens.Count == 3)
        {
            return Success(
                new CliCommand
                {
                    Kind = CliCommandKind.ConfigGet,
                    ConfigPath = configPath,
                    ConfigKey = tokens[2],
                });
        }

        if (operation == "set" && tokens.Count == 4)
        {
            return Success(
                new CliCommand
                {
                    Kind = CliCommandKind.ConfigSet,
                    ConfigPath = configPath,
                    ConfigKey = tokens[2],
                    ConfigValue = tokens[3],
                });
        }

        return Failure("configコマンドの引数が不正です。");
    }

    private static CliParseResult Success(CliCommand command)
    {
        return new CliParseResult
        {
            Success = true,
            Command = command,
        };
    }

    private static CliParseResult Failure(string error)
    {
        return new CliParseResult { Error = error };
    }
}
