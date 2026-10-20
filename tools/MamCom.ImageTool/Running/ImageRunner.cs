using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MamCom.Core.Models;
using MamCom.DataBuilder.Loading;
using MamCom.DataBuilder.Schemas;
using MamCom.DataBuilder.Validation;
using MamCom.ImageTool.Imaging;

namespace MamCom.ImageTool.Running;

public enum OutcomeKind
{
    /// <summary>The two files were made and the data file names them.</summary>
    Written,

    /// <summary>The files were already up to date; only the data file changed (new source, license or illustration flag).</summary>
    JsonOnly,

    /// <summary>Nothing to do: the files are newer than the original and the data file already says it all.</summary>
    UpToDate,

    Failed,
}

/// <param name="Id">The id of the ingredient or dish, or the file name when it could not be one.</param>
/// <param name="Warnings">Things worth a look that did not stop the picture: a size the original is too small for, a shape other than the one SPEC §7.5 asks for.</param>
public sealed record Outcome(string Id, OutcomeKind Kind, string Message, IReadOnlyList<string> Warnings);

/// <param name="Only">Only these ids; null means every original in the folder.</param>
/// <param name="Source">Where the pictures come from; null keeps what the data file already says.</param>
/// <param name="IsIllustration">Sets the illustration flag; null keeps what the data file already says.</param>
/// <param name="Force">Make the files again even when they are newer than the original.</param>
public sealed record RunOptions(IReadOnlyList<string>? Only, string? Source, string? License, bool? IsIllustration, bool Force);

/// <param name="Problem">Why nothing could be done at all (no folder, data unusable); null when the run went through every original.</param>
/// <param name="Ignored">Files in the originals folder that are not pictures of a supported kind, with the reason.</param>
public sealed record RunResult(string? Problem, IReadOnlyList<Outcome> Outcomes, IReadOnlyList<string> Ignored);

/// <summary>
/// Goes through <c>data/images/raw</c>: each original is named after the id of an ingredient or a dish, becomes
/// <c>data/images/out/&lt;id&gt;-200.webp</c> and <c>&lt;id&gt;-800.webp</c>, and the data file of that ingredient or dish is
/// updated to name them, written exactly as Studio writes it so Git shows only the image lines. A picture needs a source and
/// a license (the schema says so); they are never invented: they come from the options or from what the data file already has.
/// Each original is its own unit: one that cannot be done leaves its files and data file as they were, and the rest go on.
/// </summary>
public sealed partial class ImageRunner(string dataFolder)
{
    private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp"];

    private sealed record Target(string Id, bool IsIngredient, string File, string Text, ImageRef Image);

    public static string ThumbName(string id) => $"{id}-{ImageProcessor.ThumbPixels}.webp";

    public static string FullName(string id) => $"{id}-{ImageProcessor.FullPixels}.webp";

    public RunResult Run(RunOptions options, Action<Outcome>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rawFolder = Path.Combine(dataFolder, "images", "raw");
        if (!Directory.Exists(rawFolder))
        {
            return new RunResult(Messages.RawFolderMissing(rawFolder), [], []);
        }

        var data = DataLoader.Load(dataFolder);
        var schemaFolder = Path.Combine(dataFolder, "schemas");
        SchemaSet? schemas = null;
        if (Directory.Exists(schemaFolder))
        {
            try
            {
                schemas = SchemaSet.Load(schemaFolder);
            }
            catch (InvalidOperationException)
            {
                // DataLoader has reported it; the data is not usable below.
            }
        }

        if (schemas is null || data.Config is null || data.Validation is null)
        {
            return new RunResult(Messages.DataNotUsable, [], []);
        }

        var ignored = new List<string>();
        var byId = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(rawFolder).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith('.'))
            {
                continue;   // .gitkeep and the like
            }

            if (!Extensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            {
                ignored.Add(Messages.UnsupportedFile(name));
                continue;
            }

            var id = Path.GetFileNameWithoutExtension(name);
            if (options.Only is { } only && !only.Contains(id, StringComparer.Ordinal))
            {
                continue;
            }

            if (!byId.TryGetValue(id, out var files))
            {
                byId[id] = files = [];
            }

            files.Add(path);
        }

        var outcomes = new List<Outcome>();
        void Report(Outcome outcome)
        {
            outcomes.Add(outcome);
            progress?.Invoke(outcome);
        }

        if (options.Only is { } wanted)
        {
            foreach (var missing in wanted.Where(id => !byId.ContainsKey(id)).Order(StringComparer.Ordinal))
            {
                Report(Failed(missing, Messages.NoOriginal(missing)));
            }
        }

        foreach (var (id, files) in byId)
        {
            if (files.Count > 1)
            {
                Report(Failed(id, Messages.TwoOriginals(id, files.Select(Path.GetFileName).OfType<string>())));
                continue;
            }

            try
            {
                Report(Do(id, files[0], options, data, schemas));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A disk that is full, a file another program holds: this picture fails, the others still go on.
                Report(Failed(id, Messages.WriteFailed(id, ex.Message)));
            }
        }

        return new RunResult(null, outcomes, ignored);
    }

    private Outcome Do(string id, string rawPath, RunOptions options, LoadedData data, SchemaSet schemas)
    {
        if (!IdPattern().IsMatch(id))
        {
            return Failed(id, Messages.BadId(Path.GetFileName(rawPath)));
        }

        var found = Find(id, out var problem);
        if (found is null)
        {
            return Failed(id, problem!);
        }

        var wanted = found.Image with
        {
            Thumb = ThumbName(id),
            Full = FullName(id),
            Source = options.Source ?? found.Image.Source,
            License = options.License ?? found.Image.License,
            IsIllustration = options.IsIllustration ?? found.Image.IsIllustration,
        };

        if (string.IsNullOrWhiteSpace(wanted.Source) || string.IsNullOrWhiteSpace(wanted.License))
        {
            return Failed(id, Messages.SourceAndLicenseNeeded(id));
        }

        var outFolder = Path.Combine(dataFolder, "images", "out");
        var thumbPath = Path.Combine(outFolder, ThumbName(id));
        var fullPath = Path.Combine(outFolder, FullName(id));
        var rawTime = File.GetLastWriteTimeUtc(rawPath);
        var fresh = !options.Force
            && File.Exists(thumbPath) && File.Exists(fullPath)
            && File.GetLastWriteTimeUtc(thumbPath) >= rawTime && File.GetLastWriteTimeUtc(fullPath) >= rawTime;

        var warnings = new List<string>();
        ProcessedImage? made = null;
        if (!fresh)
        {
            try
            {
                made = ImageProcessor.Process(File.ReadAllBytes(rawPath));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                return Failed(id, Messages.Unreadable(Path.GetFileName(rawPath), ex.Message));
            }

            warnings.AddRange(Warnings(found.IsIngredient, made));
            Directory.CreateDirectory(outFolder);
            WriteAtomic(thumbPath, made.Thumb.Webp);
            WriteAtomic(fullPath, made.Full.Webp);
        }

        var newText = found.IsIngredient
            ? IngredientFile.Serialize(IngredientFile.Parse(found.Text) with { Image = wanted })
            : DishFile.Serialize(DishFile.Parse(found.Text) with { Image = wanted });

        var findings = found.IsIngredient
            ? IngredientCheck.Check(found.File, newText, IngredientFile.Parse(newText), data, schemas)
            : DishCheck.Check(found.File, newText, DishFile.Parse(newText), data, schemas);
        var errors = findings.Where(f => f.Severity == Severity.Error).ToArray();
        if (errors.Length > 0)
        {
            return Failed(id, Messages.CheckFailed(found.File, errors.Select(e => $"{e.Location} {e.Message}".Trim())));
        }

        var changed = newText != found.Text;
        if (changed)
        {
            WriteAtomic(Path.Combine(dataFolder, found.File.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8.GetBytes(newText));
        }

        if (made is not null)
        {
            return new Outcome(id, OutcomeKind.Written, Messages.Written(id, Describe(made.Thumb, ThumbName(id)), Describe(made.Full, FullName(id)), found.File, changed), warnings);
        }

        return changed
            ? new Outcome(id, OutcomeKind.JsonOnly, Messages.JsonOnly(id, found.File), warnings)
            : new Outcome(id, OutcomeKind.UpToDate, Messages.UpToDate(id), warnings);
    }

    /// <summary>The ingredient or dish this id names, as it is on disk; null (and a reason) when there is none, or two.</summary>
    private Target? Find(string id, out string? problem)
    {
        var ingredientFile = $"ingredients/{id}.json";
        var dishFile = $"dishes/{id}.json";
        var ingredientPath = Path.Combine(dataFolder, "ingredients", id + ".json");
        var dishPath = Path.Combine(dataFolder, "dishes", id + ".json");
        var isIngredient = File.Exists(ingredientPath);
        var isDish = File.Exists(dishPath);

        if (isIngredient && isDish)
        {
            problem = Messages.IngredientAndDish(id);
            return null;
        }

        if (!isIngredient && !isDish)
        {
            problem = Messages.NoSuchTarget(id, File.Exists(Path.Combine(dataFolder, "dishes", "_drafts", id + ".json")));
            return null;
        }

        var file = isIngredient ? ingredientFile : dishFile;
        var text = File.ReadAllText(isIngredient ? ingredientPath : dishPath);
        try
        {
            problem = null;
            return new Target(id, isIngredient, file, text, isIngredient ? IngredientFile.Parse(text).Image : DishFile.Parse(text).Image);
        }
        catch (JsonException ex)
        {
            problem = Messages.FileBroken(file, ex.Message);
            return null;
        }
    }

    /// <summary>A picture the size SPEC §7.5 does not ask for still works, but the person who took it may want to know.</summary>
    private static List<string> Warnings(bool isIngredient, ProcessedImage made)
    {
        var warnings = new List<string>();
        if (Math.Max(made.SourceWidth, made.SourceHeight) < ImageProcessor.FullPixels)
        {
            warnings.Add(Messages.SmallOriginal(made.SourceWidth, made.SourceHeight, ImageProcessor.FullPixels));
        }

        var ratio = (double)Math.Max(made.SourceWidth, made.SourceHeight) / Math.Min(made.SourceWidth, made.SourceHeight);
        var wanted = isIngredient ? 1.0 : 4.0 / 3.0;
        if (Math.Abs(ratio - wanted) / wanted > 0.03)
        {
            warnings.Add(isIngredient ? Messages.NotSquare(made.SourceWidth, made.SourceHeight) : Messages.NotFourByThree(made.SourceWidth, made.SourceHeight));
        }

        return warnings;
    }

    private static string Describe(Rendition rendition, string name) => Messages.Rendition(name, rendition.Width, rendition.Height, rendition.Webp.Length);

    private static Outcome Failed(string id, string message) => new(id, OutcomeKind.Failed, message, []);

    /// <summary>Writes through a temporary file next to the target, so a file is never left half written.</summary>
    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex IdPattern();
}
