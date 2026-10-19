using System.Net;
using System.Text.Json.Nodes;
using MamCom.RecipeDrafter.Model;

namespace MamCom.RecipeDrafter.Tests;

/// <summary>The real model class against a fake server: what it sends, what it makes of the answer, how it reports failures.</summary>
public class AnthropicRecipeModelTests
{
    private const string Key = "sk-ant-test-key-1234";

    private static readonly ModelRequest Request = new(
        "Bạn soạn nháp công thức.",
        "Món cần soạn: Cá kho",
        """{ "type": "object", "additionalProperties": false, "required": ["a"], "properties": { "a": { "type": "string" } } }""");

    private static AnthropicRecipeModel Model(FakeAnthropicApi api, string model = "claude-opus-5-5", string effort = "medium", string key = Key) =>
        new(model, effort, key, maxRetries: 0, httpClient: new HttpClient(api), baseUrl: "https://api.test.invalid");

    private static Task<ModelReply> Draft(AnthropicRecipeModel model) => model.DraftAsync(Request, TestContext.Current.CancellationToken);

    // ---- what is sent ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task One_post_to_the_messages_endpoint_with_the_key_in_the_header()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}"));

        await Draft(Model(api));

        var sent = api.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe("POST");
        sent.Uri.AbsolutePath.ShouldBe("/v1/messages");
        sent.Uri.Host.ShouldBe("api.test.invalid");
        sent.ApiKey.ShouldBe(Key);
        sent.Uri.ToString().ShouldNotContain(Key);
    }

    [Fact]
    public async Task The_body_names_the_model_the_instructions_the_dish_and_a_json_schema_for_the_answer()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}"));

        await Draft(Model(api));

        var body = api.Requests.Single().Body;
        body["model"]!.GetValue<string>().ShouldBe("claude-opus-5-5");
        body["max_tokens"]!.GetValue<int>().ShouldBe(16000);
        body["system"]![0]!["text"]!.GetValue<string>().ShouldBe(Request.System);
        body["messages"]!.AsArray().ShouldHaveSingleItem();
        body["messages"]![0]!["role"]!.GetValue<string>().ShouldBe("user");
        body["messages"]![0]!["content"]!.GetValue<string>().ShouldBe(Request.User);

        var format = body["output_config"]!["format"]!;
        format["type"]!.GetValue<string>().ShouldBe("json_schema");
        JsonNode.DeepEquals(format["schema"], JsonNode.Parse(Request.JsonSchema)).ShouldBeTrue();
    }

    [Fact]
    public async Task The_instructions_carry_a_cache_breakpoint_so_a_run_pays_for_them_once()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}"));

        await Draft(Model(api));

        api.Requests.Single().Body["system"]![0]!["cache_control"]!["type"]!.GetValue<string>().ShouldBe("ephemeral");
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    [InlineData("max")]
    public async Task The_effort_is_sent_as_chosen(string effort)
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}"));

        await Draft(Model(api, effort: effort));

        api.Requests.Single().Body["output_config"]!["effort"]!.GetValue<string>().ShouldBe(effort);
    }

    [Fact]
    public async Task Another_model_can_be_chosen()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}"));

        await Draft(Model(api, model: "claude-sonnet-5-5"));

        api.Requests.Single().Body["model"]!.GetValue<string>().ShouldBe("claude-sonnet-5-5");
    }

    [Fact]
    public async Task Nothing_is_sent_that_the_model_does_not_accept_or_the_drafter_does_not_want()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}"));

        await Draft(Model(api));

        var body = api.Requests.Single().Body;
        foreach (var member in new[] { "temperature", "top_p", "top_k", "thinking", "tools", "tool_choice", "stream" })
        {
            body.ContainsKey(member).ShouldBeFalse(member);
        }

        body["messages"]!.AsArray().ShouldAllBe(m => m!["role"]!.GetValue<string>() == "user");   // no prefill
    }

    // ---- what is made of the answer ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_text_the_stop_reason_the_model_and_the_tokens_come_back()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{\"a\": \"b\"}", inputTokens: 1500, outputTokens: 700));

        var reply = await Draft(Model(api));

        reply.Text.ShouldBe("{\"a\": \"b\"}");
        reply.StopReason.ShouldBe("end_turn");
        reply.RefusalCategory.ShouldBeNull();
        reply.Model.ShouldBe("claude-opus-5-5");
        reply.Usage.ShouldBe(new TokenUsage(1500, 700, 1000, 0));
    }

    [Fact]
    public async Task A_refusal_has_no_text_and_names_its_category()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message(null, "refusal", "cyber"));

        var reply = await Draft(Model(api));

        reply.Text.ShouldBeNull();
        reply.StopReason.ShouldBe("refusal");
        reply.RefusalCategory.ShouldBe("cyber");
    }

    [Fact]
    public async Task An_answer_cut_off_by_the_token_limit_says_so()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{\"a\": ", "max_tokens"));

        var reply = await Draft(Model(api));

        reply.StopReason.ShouldBe("max_tokens");
        reply.Text.ShouldBe("{\"a\": ");
    }

    [Fact]
    public async Task Several_text_blocks_are_joined()
    {
        var api = FakeAnthropicApi.Returning(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                { "id": "msg_1", "type": "message", "role": "assistant", "model": "claude-opus-5-5",
                  "content": [ { "type": "text", "text": "{\"a\": " }, { "type": "text", "text": "\"b\"}" } ],
                  "stop_reason": "end_turn", "stop_sequence": null,
                  "usage": { "input_tokens": 1, "output_tokens": 1 } }
                """,
                System.Text.Encoding.UTF8,
                "application/json"),
        });

        var reply = await Draft(Model(api));

        reply.Text.ShouldBe("{\"a\": \"b\"}");
        reply.Usage.CacheReadInputTokens.ShouldBe(0);
        reply.Usage.CacheCreationInputTokens.ShouldBe(0);
    }

    // ---- failures ----------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ModelFailure.Auth)]
    [InlineData(HttpStatusCode.Forbidden, ModelFailure.Auth)]
    [InlineData(HttpStatusCode.TooManyRequests, ModelFailure.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, ModelFailure.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ModelFailure.Unavailable)]
    [InlineData(HttpStatusCode.BadRequest, ModelFailure.Rejected)]
    [InlineData(HttpStatusCode.NotFound, ModelFailure.Rejected)]
    [InlineData(HttpStatusCode.UnprocessableEntity, ModelFailure.Rejected)]
    public async Task Each_http_failure_is_told_apart(HttpStatusCode status, ModelFailure expected)
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Error(status));

        var error = await Should.ThrowAsync<ModelException>(() => Draft(Model(api)));

        error.Kind.ShouldBe(expected);
        error.Message.ShouldNotContain(Key);
    }

    [Fact]
    public async Task A_rejected_request_says_what_the_api_said()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Error(HttpStatusCode.BadRequest, "invalid_request_error", "schema is too complex"));

        var error = await Should.ThrowAsync<ModelException>(() => Draft(Model(api)));

        error.Kind.ShouldBe(ModelFailure.Rejected);
        error.Message.ShouldContain("schema is too complex");
    }

    [Fact]
    public async Task A_network_failure_is_a_network_error()
    {
        var api = FakeAnthropicApi.Always(_ => throw new HttpRequestException("no route to host"));

        var error = await Should.ThrowAsync<ModelException>(() => Draft(Model(api)));

        error.Kind.ShouldBe(ModelFailure.Network);
        error.Message.ShouldNotContain(Key);
    }

    [Fact]
    public async Task A_cancelled_call_is_cancelled_and_not_reported_as_a_model_failure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}"));

        await Should.ThrowAsync<OperationCanceledException>(() => Model(api).DraftAsync(Request, cancellation.Token));
    }

    // ---- construction ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_blank_model_key_or_a_bad_effort_is_refused()
    {
        var api = FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}"));

        Should.Throw<ArgumentException>(() => Model(api, model: " "));
        Should.Throw<ArgumentException>(() => Model(api, key: " "));
        Should.Throw<ArgumentException>(() => Model(api, effort: "extreme")).Message.ShouldContain("low, medium, high, max");
    }

    [Fact]
    public void The_defaults_are_opus_5_5_at_medium_effort()
    {
        AnthropicRecipeModel.DefaultModel.ShouldBe("claude-opus-5-5");
        AnthropicRecipeModel.DefaultEffort.ShouldBe("medium");
        AnthropicRecipeModel.MaxTokens.ShouldBe(16000);
        AnthropicRecipeModel.EffortNames.ShouldBe(["low", "medium", "high", "max"], ignoreOrder: true);
    }

    [Fact]
    public void The_name_is_the_model_that_was_asked_for()
    {
        Model(FakeAnthropicApi.Returning(FakeAnthropicApi.Message("{}")), model: "claude-sonnet-5-5").Name.ShouldBe("claude-sonnet-5-5");
    }
}
