using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace MamCom.RecipeDrafter.Model;

/// <summary>
/// The Claude API, through the official Anthropic SDK. One request per dish: the instructions and the ingredient list
/// are the system prompt and carry a cache breakpoint, so a run over many dishes pays for them once; the answer is
/// constrained to the JSON Schema with structured outputs. Thinking is left at the model's default (adaptive).
/// </summary>
public sealed class AnthropicRecipeModel : IRecipeModel
{
    public const string DefaultModel = "claude-opus-5-5";
    public const string DefaultEffort = "medium";
    public const int MaxTokens = 16000;

    private static readonly Dictionary<string, Effort> Efforts = new(StringComparer.Ordinal)
    {
        ["low"] = Effort.Low,
        ["medium"] = Effort.Medium,
        ["high"] = Effort.High,
        ["max"] = Effort.Max,
    };

    private readonly AnthropicClient _client;
    private readonly Effort _effort;

    /// <param name="httpClient">Only for tests: the HTTP stack to use, together with <paramref name="baseUrl"/>.</param>
    /// <exception cref="ArgumentException">The effort is not one of low, medium, high, max.</exception>
    public AnthropicRecipeModel(string model, string effort, string apiKey, int maxRetries = 2, HttpClient? httpClient = null, string? baseUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (!Efforts.TryGetValue(effort, out _effort))
        {
            throw new ArgumentException($"The effort '{effort}' must be one of: {string.Join(", ", Efforts.Keys)}.", nameof(effort));
        }

        Name = model;
        var timeout = TimeSpan.FromMinutes(5);
        _client = httpClient is null
            ? new AnthropicClient { ApiKey = apiKey, MaxRetries = maxRetries, Timeout = timeout }
            : new AnthropicClient { ApiKey = apiKey, MaxRetries = maxRetries, Timeout = timeout, HttpClient = httpClient, BaseUrl = baseUrl ?? "https://api.anthropic.com" };
    }

    public static IReadOnlyCollection<string> EffortNames => Efforts.Keys;

    public string Name { get; }

    public async Task<ModelReply> DraftAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.JsonSchema)
            ?? throw new ArgumentException("The JSON Schema is not an object.", nameof(request));

        var parameters = new MessageCreateParams
        {
            Model = Name,
            MaxTokens = MaxTokens,
            System = new List<TextBlockParam>
            {
                new() { Text = request.System, CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = request.User }],
            OutputConfig = new OutputConfig { Effort = _effort, Format = new JsonOutputFormat { Schema = schema } },
        };

        Message response;
        try
        {
            response = await _client.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (AnthropicUnauthorizedException ex)
        {
            throw new ModelException(ModelFailure.Auth, "The API key was refused (HTTP 401).", ex);
        }
        catch (AnthropicForbiddenException ex)
        {
            throw new ModelException(ModelFailure.Auth, "The API key may not use this model or feature (HTTP 403).", ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new ModelException(ModelFailure.RateLimited, "The API is rate limiting this key (HTTP 429).", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new ModelException(ModelFailure.Unavailable, "The API had a server problem (HTTP 5xx).", ex);
        }
        catch (Anthropic4xxException ex)
        {
            throw new ModelException(ModelFailure.Rejected, $"The API rejected the request: {ex.Message}", ex);
        }
        catch (AnthropicIOException ex)
        {
            throw new ModelException(ModelFailure.Network, $"Could not reach the API: {ex.Message}", ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new ModelException(ModelFailure.Other, $"The API call failed: {ex.Message}", ex);
        }

        var text = string.Concat(response.Content.Select(block => block.Value).OfType<TextBlock>().Select(block => block.Text));
        var usage = new TokenUsage(
            response.Usage.InputTokens,
            response.Usage.OutputTokens,
            response.Usage.CacheCreationInputTokens ?? 0,
            response.Usage.CacheReadInputTokens ?? 0);

        return new ModelReply(
            text.Length == 0 ? null : text,
            response.StopReason?.Raw() ?? "unknown",
            response.StopDetails?.Category?.Raw(),
            usage,
            response.Model.Raw());
    }
}
