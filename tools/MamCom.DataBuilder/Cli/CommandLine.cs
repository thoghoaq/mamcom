using System.Text;
using MamCom.DataBuilder.Bundling;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Reporting;
using MamCom.DataBuilder.Validation;

namespace MamCom.DataBuilder.Cli;

/// <summary>
/// <c>validate</c> and <c>build</c> (CLAUDE.md, "Lệnh thường dùng"). Exit code 0: nothing wrong; 1: the data has errors;
/// 2: the command line is wrong.
/// </summary>
public static class CommandLine
{
    public const int Success = 0;
    public const int DataInvalid = 1;
    public const int UsageError = 2;

    public const string DefaultDataFolder = "data";
    public const string DefaultOutFolder = "dist";

    public const string BundleFileName = "data-bundle.json";
    public const string ReportFileName = "data-report.md";

    private const string Usage = """
        Cách dùng:
          MamCom.DataBuilder validate [--data <thư mục>] [--release]
          MamCom.DataBuilder build    [--data <thư mục>] [--out <thư mục>] [--release]

        validate  kiểm dữ liệu (luật SPEC §7.3) và in lỗi, cảnh báo.
        build     kiểm rồi ghi data-bundle.json và data-report.md vào thư mục --out.
                  Còn lỗi thì chỉ ghi báo cáo, không ghi bundle.

        --data     thư mục dữ liệu, mặc định "data".
        --out      thư mục đầu ra của build, mặc định "dist".
        --release  bản phát hành: chỉ nhận món từ reviewed, không còn dinh dưỡng placeholder,
                   cảnh báo thành lỗi.
        """;

    private sealed record Options(string Data, string Out, bool Release);

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Length == 0)
        {
            error.WriteLine(Usage);
            return UsageError;
        }

        if (args[0] is "help" or "--help" or "-h")
        {
            output.WriteLine(Usage);
            return Success;
        }

        var command = args[0];
        if (command is not ("validate" or "build"))
        {
            error.WriteLine($"Lệnh '{command}' không có.");
            error.WriteLine(Usage);
            return UsageError;
        }

        if (!TryParseOptions(command, args.Skip(1).ToArray(), out var options, out var problem))
        {
            error.WriteLine(problem);
            error.WriteLine(Usage);
            return UsageError;
        }

        var mode = options.Release ? ValidationMode.Release : ValidationMode.Dev;
        var report = DataValidator.Validate(DataLoader.Load(options.Data), mode);
        Print(report, output, error);

        return command == "build" ? Build(report, options, output) : Finish(report, output);
    }

    private static bool TryParseOptions(string command, string[] args, out Options options, out string problem)
    {
        string data = DefaultDataFolder, outFolder = DefaultOutFolder;
        var release = false;
        options = new Options(data, outFolder, release);
        problem = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--release":
                    release = true;
                    break;
                case "--data" when i + 1 < args.Length:
                    data = args[++i];
                    break;
                case "--out" when command == "build" && i + 1 < args.Length:
                    outFolder = args[++i];
                    break;
                case "--data" or "--out":
                    problem = args[i] == "--out" && command != "build"
                        ? "Tuỳ chọn '--out' chỉ dùng với lệnh build."
                        : $"Tuỳ chọn '{args[i]}' cần một giá trị.";
                    return false;
                default:
                    problem = $"Tuỳ chọn '{args[i]}' không có.";
                    return false;
            }
        }

        options = new Options(data, outFolder, release);
        return true;
    }

    private static int Build(ValidationReport report, Options options, TextWriter output)
    {
        Directory.CreateDirectory(options.Out);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var bundlePath = Path.Combine(options.Out, BundleFileName);
        Core.Data.DataBundle? bundle = null;
        if (report.HasErrors)
        {
            // A bundle left from an earlier build would look like the result of this one to anything that ignores the exit code.
            File.Delete(bundlePath);
        }
        else
        {
            bundle = BundleBuilder.Build(report.Data, report.Mode);
            File.WriteAllText(bundlePath, bundle.ToJson(), utf8);
        }

        File.WriteAllText(Path.Combine(options.Out, ReportFileName), ReportWriter.Render(report, bundle), utf8);

        if (bundle is null)
        {
            output.WriteLine($"Không ghi {BundleFileName} vì còn lỗi. Báo cáo: {Path.Combine(options.Out, ReportFileName)}");
            return DataInvalid;
        }

        output.WriteLine($"Đã ghi {Path.Combine(options.Out, BundleFileName)} (phiên bản {bundle.Version}) và {Path.Combine(options.Out, ReportFileName)}.");
        return Success;
    }

    private static int Finish(ValidationReport report, TextWriter output)
    {
        output.WriteLine(report.HasErrors ? "Dữ liệu còn lỗi." : "Dữ liệu hợp lệ.");
        return report.HasErrors ? DataInvalid : Success;
    }

    private static void Print(ValidationReport report, TextWriter output, TextWriter error)
    {
        foreach (var diagnostic in report.Diagnostics)
        {
            var where = diagnostic.File.Length == 0 ? string.Empty : $" {diagnostic.File}{(diagnostic.Location.Length == 0 ? string.Empty : ":" + diagnostic.Location)}";
            var line = $"{(diagnostic.Severity == Severity.Error ? "error" : "warning")}:{where} [{diagnostic.Code}] {diagnostic.Message}";
            (diagnostic.Severity == Severity.Error ? error : output).WriteLine(line);
        }

        var mode = report.Mode == ValidationMode.Release ? "release" : "dev";
        output.WriteLine($"Đã kiểm {report.Data.Ingredients.Count} nguyên liệu và {report.Data.Dishes.Count} món ({mode}): {report.Errors.Count()} lỗi, {report.Warnings.Count()} cảnh báo.");
    }
}
