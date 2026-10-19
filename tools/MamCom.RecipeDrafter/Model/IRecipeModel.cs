namespace MamCom.RecipeDrafter.Model;

/// <summary>One call to the model: the instructions, the dish, and the JSON Schema its answer must follow.</summary>
public sealed record ModelRequest(string System, string User, string JsonSchema);

/// <summary>Tokens a call used. Cache reads and writes are counted apart because they are billed differently.</summary>
public sealed record TokenUsage(long InputTokens, long OutputTokens, long CacheCreationInputTokens, long CacheReadInputTokens);

/// <param name="Text">What the model wrote (JSON when all went well); null when it wrote no text, as in a refusal.</param>
/// <param name="StopReason"><c>end_turn</c> for a complete answer; <c>max_tokens</c> when it was cut off; <c>refusal</c> when it declined.</param>
/// <param name="RefusalCategory">The category the model gave for a refusal, when it gave one.</param>
/// <param name="Model">The model that answered.</param>
public sealed record ModelReply(string? Text, string StopReason, string? RefusalCategory, TokenUsage Usage, string Model);

public enum ModelFailure
{
    /// <summary>No key, or the key was refused.</summary>
    Auth,

    /// <summary>Over the request limit even after the SDK's retries.</summary>
    RateLimited,

    /// <summary>The request itself was rejected (a bad schema, an unknown model).</summary>
    Rejected,

    /// <summary>The service had a problem (5xx) even after the SDK's retries.</summary>
    Unavailable,

    /// <summary>Nothing came back: a network failure or a timeout.</summary>
    Network,

    /// <summary>Anything else.</summary>
    Other,
}

public sealed class ModelException(ModelFailure kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public ModelFailure Kind { get; } = kind;
}

/// <summary>The language model that writes drafts. A stand-in for the real one is all a test needs.</summary>
public interface IRecipeModel
{
    /// <summary>The model id, for the report.</summary>
    string Name { get; }

    /// <exception cref="ModelException">The call failed.</exception>
    Task<ModelReply> DraftAsync(ModelRequest request, CancellationToken cancellationToken);
}
