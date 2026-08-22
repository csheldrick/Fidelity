#!/usr/bin/env dotnet
#:include ../src/Fidelity/Replay.cs
#:package Refit@8.0.0

using Refit;
using System.Text.Json;
using System.Text.Json.Serialization;

var replay = new ReplayHttpMessageHandler(ReplayFixture.Read("fixtures/application-error.json"));
using var httpClient = new HttpClient(replay)
{
    BaseAddress = new Uri("https://fidelity.invalid/")
};
var client = RestService.For<ILossyApi>(httpClient, new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
    {
        TypeInfoResolver = LossyJsonContext.Default
    })
});

var exitCode = await Fidelity.RunAsync(
    "lossy model replay",
    replay,
    () => client.GetOperationAsync(),
    expectations =>
    {
        expectations.Equal("result container", "present", response =>
            response.Result is null ? "missing" : "present");

        // The incomplete model has no Status or Error properties. These required
        // semantics are therefore not observable after the typed pipeline runs.
        expectations.Equal("result.status", "error", _ => null);
        expectations.Equal("result.error.message", "operation_not_allowed", _ => null);
        expectations.Equal("result.error.code", -180, _ => null);
    });

return exitCode;

public interface ILossyApi
{
    [Get("/operation")]
    Task<IncompleteResponse> GetOperationAsync();
}

public sealed class IncompleteResponse
{
    [JsonPropertyName("result")]
    public IncompleteResult? Result { get; set; }
}

public sealed class IncompleteResult
{
    // The JSON error fields are valid, but this response model cannot represent them.
    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }
}

[JsonSerializable(typeof(IncompleteResponse))]
internal partial class LossyJsonContext : JsonSerializerContext
{
}
