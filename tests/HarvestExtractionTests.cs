#!/usr/bin/env dotnet
#:include ../src/Fidelity/Harvest.cs

using System.Text.Json;

// Deterministic coverage for the pure extraction logic in
// src/Fidelity/Harvest.cs against captured `az monitor ... query` result
// envelopes. No live Azure access is required or performed.

var failures = 0;

failures += Check("one-row successful extraction", () =>
{
    var result = Extract("az-single-row.json", "responseBody", rowIndex: null);
    return result.Success && result.SelectedRowIndex == 0 && result.TotalRows == 1;
});

failures += Check("exact raw body preservation", () =>
{
    var result = Extract("az-single-row.json", "responseBody", rowIndex: null);
    return result.Success &&
        result.RawValue == "{\"result\":{\"status\":\"ok\",\"message\":\"replay-is-real\",\"count\":3}}";
});

failures += Check("zero rows fails loudly", () =>
{
    var result = Extract("az-zero-rows.json", "responseBody", rowIndex: null);
    return !result.Success && result.FailureReason == HarvestFailureReason.ZeroRows;
});

failures += Check("multiple rows without explicit selection fails loudly", () =>
{
    var result = Extract("az-multi-row.json", "responseBody", rowIndex: null);
    return !result.Success && result.FailureReason == HarvestFailureReason.AmbiguousRows && result.TotalRows == 2;
});

failures += Check("valid explicit row selection extracts the selected row", () =>
{
    var result = Extract("az-multi-row.json", "responseBody", rowIndex: 1);
    return result.Success && result.SelectedRowIndex == 1 &&
        result.RawValue == "{\"result\":{\"status\":\"ok\",\"message\":\"second-row\",\"count\":2}}";
});

failures += Check("invalid explicit row index fails loudly", () =>
{
    var result = Extract("az-multi-row.json", "responseBody", rowIndex: 5);
    return !result.Success && result.FailureReason == HarvestFailureReason.InvalidRowIndex;
});

failures += Check("missing response field fails loudly", () =>
{
    var result = Extract("az-missing-field.json", "responseBody", rowIndex: null);
    return !result.Success && result.FailureReason == HarvestFailureReason.MissingField;
});

failures += Check("empty response field fails loudly", () =>
{
    var result = Extract("az-empty-field.json", "responseBody", rowIndex: null);
    return !result.Success && result.FailureReason == HarvestFailureReason.EmptyField;
});

failures += Check("malformed CLI result envelope fails loudly", () =>
{
    var result = Extract("az-malformed.json", "responseBody", rowIndex: null);
    return !result.Success && result.FailureReason == HarvestFailureReason.MalformedEnvelope;
});

failures += Check("malformed (non-JSON) CLI output fails loudly", () =>
{
    var result = AzureCliQueryEnvelope.Extract("not json at all", "responseBody", null);
    return !result.Success && result.FailureReason == HarvestFailureReason.MalformedEnvelope;
});

failures += await CheckAsync("orchestration writes the extracted body byte-for-byte and reports success", async () =>
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.ApplicationInsights,
                Target: "test-app",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: false,
                Offset: "4d"),
            FakeRunner("az-single-row.json"));

        return exitCode == 0 &&
            File.ReadAllText(outputPath) == "{\"result\":{\"status\":\"ok\",\"message\":\"replay-is-real\",\"count\":3}}";
    }
    finally
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }
});

failures += await CheckAsync("orchestration writes separate provenance sidecar without body content", async () =>
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    var provenancePath = outputPath + ".provenance.json";
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.LogAnalytics,
                Target: "test-workspace",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: true),
            FakeRunner("az-single-row.json"));

        if (exitCode != 0 || !File.Exists(provenancePath))
        {
            return false;
        }

        var provenance = JsonDocument.Parse(File.ReadAllText(provenancePath)).RootElement;
        return provenance.GetProperty("sourceKind").GetString() == "log-analytics" &&
            provenance.GetProperty("target").GetString() == "test-workspace" &&
            provenance.GetProperty("selectedRowIndex").GetInt32() == 0 &&
            provenance.GetProperty("totalRows").GetInt32() == 1 &&
            provenance.TryGetProperty("queryFingerprint", out _) &&
            !provenance.TryGetProperty("queryText", out _);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
        if (File.Exists(provenancePath)) File.Delete(provenancePath);
    }
});

failures += await CheckAsync("orchestration fails loudly when az CLI exits non-zero", async () =>
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.ApplicationInsights,
                Target: "test-app",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                Offset: "4d"),
            (_, _) => Task.FromResult(new AzCliProcessResult(1, string.Empty, "ClientAuthenticationError: please run az login")));

        return exitCode != 0 && !File.Exists(outputPath);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
});

failures += await CheckAsync("orchestration fails loudly when the output path cannot be written", async () =>
{
    // A regular file cannot also be a directory segment on any platform, so
    // treating one as the parent of the output path is a deterministic,
    // host-independent write failure (no reliance on a specific drive
    // letter or OS-specific permission-denied path).
    var blockingFile = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-blocking-file-{Guid.NewGuid():N}");
    File.WriteAllText(blockingFile, "not a directory");
    try
    {
        var unwritablePath = Path.Combine(blockingFile, "nested", "out.json");

        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.ApplicationInsights,
                Target: "test-app",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: unwritablePath,
                Offset: "4d"),
            FakeRunner("az-single-row.json"));

        return exitCode != 0;
    }
    finally
    {
        File.Delete(blockingFile);
    }
});

failures += await CheckAsync("orchestration invokes az with the expected application-insights argument shape", async () =>
{
    string[]? capturedArguments = null;
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.ApplicationInsights,
                Target: "my-app-insights-resource",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: false,
                Offset: "4d"),
            CapturingRunner("az-single-row.json", arguments => capturedArguments = arguments));

        return exitCode == 0 && capturedArguments is
        [
            "monitor", "app-insights", "query",
            "--app", "my-app-insights-resource",
            "--analytics-query", "traces | take 1",
            "--offset", "4d",
            "-o", "json"
        ];
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
});

failures += await CheckAsync("application-insights harvest without --offset fails before the injected az runner is ever called", async () =>
{
    var runnerCalled = false;
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.ApplicationInsights,
                Target: "my-app-insights-resource",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: false,
                Offset: null),
            CapturingRunner("az-single-row.json", _ => runnerCalled = true));

        return exitCode != 0 && !runnerCalled && !File.Exists(outputPath);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
});

failures += await CheckAsync("application-insights harvest with a blank --offset also fails before the injected az runner is called", async () =>
{
    var runnerCalled = false;
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.ApplicationInsights,
                Target: "my-app-insights-resource",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: false,
                Offset: "   "),
            CapturingRunner("az-single-row.json", _ => runnerCalled = true));

        return exitCode != 0 && !runnerCalled && !File.Exists(outputPath);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
});

failures += await CheckAsync("--offset is not required, and not sent, for log-analytics mode", async () =>
{
    string[]? capturedArguments = null;
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.LogAnalytics,
                Target: "my-log-analytics-workspace",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: false,
                Offset: null),
            CapturingRunner("az-single-row.json", arguments => capturedArguments = arguments));

        return exitCode == 0 && capturedArguments is
        [
            "monitor", "log-analytics", "query",
            "--workspace", "my-log-analytics-workspace",
            "--analytics-query", "traces | take 1",
            "-o", "json"
        ];
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
});

failures += await CheckAsync("provenance sidecar records the explicit application-insights offset", async () =>
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    var provenancePath = outputPath + ".provenance.json";
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.ApplicationInsights,
                Target: "my-app-insights-resource",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: true,
                Offset: "4d"),
            FakeRunner("az-single-row.json"));

        if (exitCode != 0 || !File.Exists(provenancePath))
        {
            return false;
        }

        var provenance = JsonDocument.Parse(File.ReadAllText(provenancePath)).RootElement;
        return provenance.GetProperty("offset").GetString() == "4d";
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
        if (File.Exists(provenancePath)) File.Delete(provenancePath);
    }
});

failures += await CheckAsync("provenance sidecar omits offset for log-analytics", async () =>
{
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    var provenancePath = outputPath + ".provenance.json";
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.LogAnalytics,
                Target: "my-log-analytics-workspace",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: true,
                Offset: null),
            FakeRunner("az-single-row.json"));

        if (exitCode != 0 || !File.Exists(provenancePath))
        {
            return false;
        }

        var provenance = JsonDocument.Parse(File.ReadAllText(provenancePath)).RootElement;
        return !provenance.TryGetProperty("offset", out _);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
        if (File.Exists(provenancePath)) File.Delete(provenancePath);
    }
});

failures += await CheckAsync("orchestration invokes az with the expected log-analytics argument shape", async () =>
{
    string[]? capturedArguments = null;
    var outputPath = Path.Combine(Path.GetTempPath(), $"fidelity-harvest-test-{Guid.NewGuid():N}.json");
    try
    {
        var exitCode = await HarvestRunner.RunAsync(
            new HarvestOptions(
                AzureTelemetryMode.LogAnalytics,
                Target: "my-log-analytics-workspace",
                Query: "traces | take 1",
                ResponseField: "responseBody",
                OutputPath: outputPath,
                WriteProvenance: false),
            CapturingRunner("az-single-row.json", arguments => capturedArguments = arguments));

        return exitCode == 0 && capturedArguments is
        [
            "monitor", "log-analytics", "query",
            "--workspace", "my-log-analytics-workspace",
            "--analytics-query", "traces | take 1",
            "-o", "json"
        ];
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
});

if (failures > 0)
{
    Console.WriteLine($"[FAIL] {failures} harvest extraction case(s) failed");
    return 1;
}

Console.WriteLine("[PASS] all harvest extraction cases passed");
return 0;

static ExtractionResult Extract(string fixtureFileName, string responseField, int? rowIndex)
{
    var json = File.ReadAllText(Path.Combine("tests", "fixtures", fixtureFileName));
    return AzureCliQueryEnvelope.Extract(json, responseField, rowIndex);
}

static AzCliRunner FakeRunner(string fixtureFileName)
{
    var json = File.ReadAllText(Path.Combine("tests", "fixtures", fixtureFileName));
    return (_, _) => Task.FromResult(new AzCliProcessResult(0, json, string.Empty));
}

static AzCliRunner CapturingRunner(string fixtureFileName, Action<string[]> onInvoked)
{
    var json = File.ReadAllText(Path.Combine("tests", "fixtures", fixtureFileName));
    return (arguments, _) =>
    {
        onInvoked(arguments);
        return Task.FromResult(new AzCliProcessResult(0, json, string.Empty));
    };
}

static int Check(string name, Func<bool> assertion)
{
    bool passed;
    string? error = null;
    try
    {
        passed = assertion();
    }
    catch (Exception exception)
    {
        passed = false;
        error = $"{exception.GetType().Name}: {exception.Message}";
    }

    Report(name, passed, error);
    return passed ? 0 : 1;
}

static async Task<int> CheckAsync(string name, Func<Task<bool>> assertion)
{
    bool passed;
    string? error = null;
    try
    {
        passed = await assertion();
    }
    catch (Exception exception)
    {
        passed = false;
        error = $"{exception.GetType().Name}: {exception.Message}";
    }

    Report(name, passed, error);
    return passed ? 0 : 1;
}

static void Report(string name, bool passed, string? error)
{
    Console.WriteLine(passed ? $"[PASS] {name}" : $"[FAIL] {name}");
    if (error is not null)
    {
        Console.WriteLine($"       {error}");
    }
}
