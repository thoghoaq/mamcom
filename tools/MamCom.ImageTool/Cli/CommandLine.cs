using MamCom.ImageTool.Running;

namespace MamCom.ImageTool.Cli;

/// <summary><c>process</c> (CLAUDE.md, "Lệnh thường dùng"). Exit code 0: done; 1: something failed; 2: the command line is wrong.</summary>
public static class CommandLine
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int UsageError = 2;

    private sealed record Options(string Data, RunOptions Run);

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0)
        {
            error.WriteLine(Messages.Usage);
            return UsageError;
        }

        if (args[0] is "help" or "--help" or "-h")
        {
            output.WriteLine(Messages.Usage);
            return Success;
        }

        if (args[0] != "process")
        {
            error.WriteLine($"Lệnh '{args[0]}' không có.");
            error.WriteLine(Messages.Usage);
            return UsageError;
        }

        if (!TryParse(args.Skip(1).ToArray(), out var options, out var problem))
        {
            error.WriteLine(problem);
            error.WriteLine(Messages.Usage);
            return UsageError;
        }

        var result = new ImageRunner(options.Data).Run(
            options.Run,
            outcome =>
            {
                (outcome.Kind == OutcomeKind.Failed ? error : output).WriteLine(outcome.Message);
                foreach (var warning in outcome.Warnings)
                {
                    output.WriteLine($"  Lưu ý, {outcome.Id}: {warning}");
                }
            });

        if (result.Problem is not null)
        {
            error.WriteLine(result.Problem);
            return Failure;
        }

        foreach (var line in result.Ignored)
        {
            output.WriteLine(line);
        }

        var count = (OutcomeKind kind) => result.Outcomes.Count(o => o.Kind == kind);
        output.WriteLine(Messages.Summary(count(OutcomeKind.Written), count(OutcomeKind.JsonOnly), count(OutcomeKind.UpToDate), count(OutcomeKind.Failed)));

        return count(OutcomeKind.Failed) == 0 ? Success : Failure;
    }

    private static bool TryParse(string[] args, out Options options, out string problem)
    {
        var data = "data";
        IReadOnlyList<string>? only = null;
        string? source = null, license = null;
        bool? illustration = null;
        var force = false;
        options = new Options(data, new RunOptions(only, source, license, illustration, force));
        problem = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            switch (name)
            {
                case "--force":
                    force = true;
                    continue;
                case "--illustration" or "--photo":
                    var value = name == "--illustration";
                    if (illustration is { } already && already != value)
                    {
                        problem = "Tuỳ chọn '--illustration' và '--photo' không đi cùng nhau.";
                        return false;
                    }

                    illustration = value;
                    continue;
            }

            if (name is not ("--data" or "--only" or "--source" or "--license"))
            {
                problem = $"Tuỳ chọn '{name}' không có.";
                return false;
            }

            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                problem = $"Tuỳ chọn '{name}' cần một giá trị.";
                return false;
            }

            var text = args[++i];
            switch (name)
            {
                case "--data":
                    data = text;
                    break;
                case "--only":
                    only = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (only.Count == 0)
                    {
                        problem = "Tuỳ chọn '--only' cần ít nhất một mã.";
                        return false;
                    }

                    break;
                case "--source":
                    source = text.Trim();
                    break;
                default:
                    license = text.Trim();
                    break;
            }
        }

        options = new Options(data, new RunOptions(only, source, license, illustration, force));
        return true;
    }
}
