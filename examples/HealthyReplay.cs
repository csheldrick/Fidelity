#!/usr/bin/env dotnet
#:include ../src/Fidelity/Replay.cs
#:package Refit@8.0.0

using Refit;
using System.Text.Json;
using System.Text.Json.Serialization;

var replay = new ReplayHttpMessageHandler(ReplayFixture.Read("fixtures/healthy.json"));
using var httpClient = new HttpClient(replay)
{
    BaseAddress = new Uri("https://fidelity.invalid/")
};
var client = RestService.For<IHealthyApi>(httpClient, new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
    {
        TypeInfoResolver = HealthyJsonContext.Default
    })
});

var exitCode = await Fidelity.RunAsync(
    "healthy replay",
    replay,
    () => client.GetHealthAsync(),
    expectations =>
    {
        expectations.Equal("result.status", "ok", response => response.Result?.Status);
        expectations.Equal("result.message", "replay-is-real", response => response.Result?.Message);
        expectations.Equal("result.count", 3, response => response.Result?.Count);
    });

return exitCode;

public interface IHealthyApi
{
    [Get("/health")]
    Task<HealthyResponse> GetHealthAsync();
}

public sealed class HealthyResponse
{
    [JsonPropertyName("result")]
    public HealthyResult? Result { get; set; }
}

public sealed class HealthyResult
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

[JsonSerializable(typeof(HealthyResponse))]
internal partial class HealthyJsonContext : JsonSerializerContext
{
}
