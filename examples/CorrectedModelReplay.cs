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
var client = RestService.For<ICorrectedApi>(httpClient, new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
    {
        TypeInfoResolver = CorrectedJsonContext.Default
    })
});

var exitCode = await Fidelity.RunAsync(
    "corrected model replay",
    replay,
    () => client.GetOperationAsync(),
    expectations =>
    {
        RequiredSemantics.ApplicationError(expectations);
    });

return exitCode;

public interface ICorrectedApi
{
    [Get("/operation")]
    Task<ErrorResponse> GetOperationAsync();
}

public sealed class ErrorResponse
{
    [JsonPropertyName("result")]
    public ErrorResult? Result { get; set; }
}

public sealed class ErrorResult
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public ErrorDetails? Error { get; set; }
}

public sealed class ErrorDetails
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }
}

[JsonSerializable(typeof(ErrorResponse))]
internal partial class CorrectedJsonContext : JsonSerializerContext
{
}
