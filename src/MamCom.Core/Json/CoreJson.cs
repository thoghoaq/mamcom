using System.Text.Encodings.Web;
using System.Text.Json;

namespace MamCom.Core.Json;

/// <summary>
/// The one set of JSON options for Core types. Output is indented with two spaces and LF line endings, writes
/// nulls explicitly and leaves Vietnamese text unescaped, so files edited by tools produce clean Git diffs.
/// </summary>
public static class CoreJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(CoreJsonContext.Default.Options)
        {
            // Data files are not embedded in HTML, so the relaxed encoder is safe and keeps diacritics readable.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.MakeReadOnly();
        return options;
    }
}
