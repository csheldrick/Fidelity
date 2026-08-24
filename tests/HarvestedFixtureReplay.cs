#!/usr/bin/env dotnet
#:include ../src/Fidelity/Harvest.cs
#:include ../src/Fidelity/Replay.cs
#:package Refit@8.0.0

using Refit;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Proves the harvester's output is an ordinary Fidelity fixture: harvest a
// response body from a captured (non-live) Application Insights CLI
// envelope, then replay the resulting file through the real, unmodified
// Fidelity replay transport and typed-client pipeline, with no
// Application Insights involvement in the replay path itself.

var sourceResponseBody = File.ReadAllText("fixtures/healthy.json");
var envelope = BuildEnvelope("responseBody", sourceResponseBody);

var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvested-{Guid.NewGuid():N}.json");
try
{
    var harvestExitCode = await HarvestRunner.RunAsync(
        new HarvestOptions(
            AzureTelemetryMode.ApplicationInsights,
            Target: "test-app",
            Query: "traces | take 1",
            ResponseField: "responseBody",
            OutputPath: outputPath,
            WriteProvenance: false,
            Offset: "4d"),
        (_, _) => Task.FromResult(new AzCliProcessResult(0, envelope, string.Empty)));

    if (harvestExitCode != 0)
    {
        Console.WriteLine("[FAIL] harvester did not exit 0");
        return 1;
    }

    var harvestedBody = File.ReadAllText(outputPath);
    if (harvestedBody != sourceResponseBody)
    {
        Console.WriteLine("[FAIL] harvested fixture did not match the source response body byte-for-byte");
        return 1;
    }

    Console.WriteLine("[PASS] harvested fixture matches the source response body byte-for-byte");

    var replay = new ReplayHttpMessageHandler(ReplayFixture.Read(outputPath));
    using var httpClient = new HttpClient(replay)
    {
        BaseAddress = new Uri("https://fidelity.invalid/")
    };
    var client = RestService.For<IHarvestedHealthyApi>(httpClient, new RefitSettings
    {
        ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
        {
            TypeInfoResolver = HarvestedHealthyJsonContext.Default
        })
    });

    return await Fidelity.RunAsync(
        "harvested fixture replay",
        replay,
        () => client.GetHealthAsync(),
        expectations =>
        {
            expectations.Equal("result.status", "ok", response => response.Result?.Status);
            expectations.Equal("result.message", "replay-is-real", response => response.Result?.Message);
            expectations.Equal("result.count", 3, response => response.Result?.Count);
        });
}
finally
{
    if (File.Exists(outputPath))
    {
        File.Delete(outputPath);
    }
}

static string BuildEnvelope(string responseFieldName, string responseBody)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartObject();
        writer.WriteStartArray("tables");
        writer.WriteStartObject();
        writer.WriteString("name", "PrimaryResult");

        writer.WriteStartArray("columns");
        writer.WriteStartObject();
        writer.WriteString("name", "timestamp");
        writer.WriteString("type", "datetime");
        writer.WriteEndObject();
        writer.WriteStartObject();
        writer.WriteString("name", responseFieldName);
        writer.WriteString("type", "string");
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WriteStartArray("rows");
        writer.WriteStartArray();
        writer.WriteStringValue("2026-08-20T12:00:00Z");
        writer.WriteStringValue(responseBody);
        writer.WriteEndArray();
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    return Encoding.UTF8.GetString(stream.ToArray());
}

public interface IHarvestedHealthyApi
{
    [Get("/health")]
    Task<HarvestedHealthyResponse> GetHealthAsync();
}

public sealed class HarvestedHealthyResponse
{
    [JsonPropertyName("result")]
    public HarvestedHealthyResult? Result { get; set; }
}

public sealed class HarvestedHealthyResult
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

[JsonSerializable(typeof(HarvestedHealthyResponse))]
internal partial class HarvestedHealthyJsonContext : JsonSerializerContext
{
}
