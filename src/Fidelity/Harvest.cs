using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public enum AzureTelemetryMode
{
    ApplicationInsights,
    LogAnalytics
}

public sealed record HarvestOptions(
    AzureTelemetryMode Mode,
    string Target,
    string Query,
    string ResponseField,
    string OutputPath,
    int? RowIndex = null,
    bool WriteProvenance = true,
    string? ProvenancePath = null,
    bool IncludeQueryText = false,
    string? Offset = null);

public sealed record AzCliProcessResult(int ExitCode, string StandardOutput, string StandardError);

public delegate Task<AzCliProcessResult> AzCliRunner(string[] arguments, CancellationToken cancellationToken);

public enum HarvestFailureReason
{
    None,
    MalformedEnvelope,
    ZeroRows,
    AmbiguousRows,
    InvalidRowIndex,
    MissingField,
    EmptyField
}

public sealed record ExtractionResult(
    bool Success,
    HarvestFailureReason FailureReason,
    string? Message,
    string? RawValue,
    int? SelectedRowIndex,
    int TotalRows)
{
    public static ExtractionResult Ok(string rawValue, int selectedRowIndex, int totalRows) =>
        new(true, HarvestFailureReason.None, null, rawValue, selectedRowIndex, totalRows);

    public static ExtractionResult Fail(HarvestFailureReason reason, string message, int? selectedRowIndex = null, int totalRows = 0) =>
        new(false, reason, message, null, selectedRowIndex, totalRows);
}

// Understands only the shared Kusto result envelope emitted by both
// `az monitor app-insights query` and `az monitor log-analytics query`
// (a `tables[].columns[]` / `tables[].rows[]` shape). It has no knowledge
// of any application's telemetry schema; the caller-owned query and
// --response-field decide what column holds the raw response body.
public static class AzureCliQueryEnvelope
{
    public static ExtractionResult Extract(string envelopeJson, string responseField, int? rowIndex)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(envelopeJson);
        }
        catch (JsonException exception)
        {
            return ExtractionResult.Fail(HarvestFailureReason.MalformedEnvelope, $"CLI output was not valid JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("tables", out var tables) ||
                tables.ValueKind != JsonValueKind.Array ||
                tables.GetArrayLength() == 0)
            {
                return ExtractionResult.Fail(HarvestFailureReason.MalformedEnvelope, "CLI result envelope did not contain a non-empty 'tables' array.");
            }

            var table = tables[0];
            if (!table.TryGetProperty("columns", out var columns) || columns.ValueKind != JsonValueKind.Array)
            {
                return ExtractionResult.Fail(HarvestFailureReason.MalformedEnvelope, "CLI result table did not contain a 'columns' array.");
            }

            if (!table.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return ExtractionResult.Fail(HarvestFailureReason.MalformedEnvelope, "CLI result table did not contain a 'rows' array.");
            }

            var columnIndex = -1;
            var index = 0;
            foreach (var column in columns.EnumerateArray())
            {
                if (column.ValueKind == JsonValueKind.Object &&
                    column.TryGetProperty("name", out var nameElement) &&
                    string.Equals(nameElement.GetString(), responseField, StringComparison.Ordinal))
                {
                    columnIndex = index;
                    break;
                }

                index++;
            }

            if (columnIndex < 0)
            {
                return ExtractionResult.Fail(HarvestFailureReason.MissingField, $"Response field '{responseField}' was not present in the query result columns.");
            }

            var totalRows = rows.GetArrayLength();
            if (totalRows == 0)
            {
                return ExtractionResult.Fail(HarvestFailureReason.ZeroRows, "The query returned zero rows.", totalRows: 0);
            }

            int selectedRow;
            if (rowIndex is not null)
            {
                if (rowIndex.Value < 0 || rowIndex.Value >= totalRows)
                {
                    return ExtractionResult.Fail(
                        HarvestFailureReason.InvalidRowIndex,
                        $"Row index {rowIndex.Value} is invalid; the query returned {totalRows} row(s).",
                        totalRows: totalRows);
                }

                selectedRow = rowIndex.Value;
            }
            else if (totalRows > 1)
            {
                return ExtractionResult.Fail(
                    HarvestFailureReason.AmbiguousRows,
                    $"The query returned {totalRows} rows; pass --row-index to select one explicitly.",
                    totalRows: totalRows);
            }
            else
            {
                selectedRow = 0;
            }

            var row = rows[selectedRow];
            if (row.ValueKind != JsonValueKind.Array || columnIndex >= row.GetArrayLength())
            {
                return ExtractionResult.Fail(HarvestFailureReason.MalformedEnvelope, "The selected row did not contain the expected number of columns.", selectedRow, totalRows);
            }

            var valueElement = row[columnIndex];
            if (valueElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return ExtractionResult.Fail(HarvestFailureReason.EmptyField, $"Response field '{responseField}' was null in the selected row.", selectedRow, totalRows);
            }

            // The response body column is a string containing the raw HTTP
            // response text. GetString() undoes only the envelope's own JSON
            // string-escaping; it does not deserialize/reserialize the body itself.
            var rawValue = valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString() ?? string.Empty
                : valueElement.GetRawText();

            if (string.IsNullOrEmpty(rawValue))
            {
                return ExtractionResult.Fail(HarvestFailureReason.EmptyField, $"Response field '{responseField}' was empty in the selected row.", selectedRow, totalRows);
            }

            return ExtractionResult.Ok(rawValue, selectedRow, totalRows);
        }
    }
}

// Executes the real `az` CLI. This is the only part of the harvester that
// touches a process; extraction above is pure and independently testable.
public static class AzCliProcess
{
    public static async Task<AzCliProcessResult> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe")
            : new ProcessStartInfo("az");

        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("az");
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new AzCliProcessResult(process.ExitCode, await standardOutputTask, await standardErrorTask);
    }
}

// Orchestrates one harvest: run the caller-owned query via az CLI (or an
// injected runner in tests), extract the configured response field with
// AzureCliQueryEnvelope, and write it as an ordinary Fidelity fixture plus
// an optional, physically separate provenance sidecar. This class owns no
// telemetry schema knowledge and does not participate in replay semantics.
public static class HarvestRunner
{
    public static async Task<int> RunAsync(HarvestOptions options, AzCliRunner runner, CancellationToken cancellationToken = default)
    {
        var sourceKind = options.Mode == AzureTelemetryMode.ApplicationInsights ? "application-insights" : "log-analytics";
        Console.WriteLine($"HARVEST: source={sourceKind} target={options.Target}");

        if (options.Mode == AzureTelemetryMode.ApplicationInsights && string.IsNullOrWhiteSpace(options.Offset))
        {
            Console.WriteLine("[FAIL] --offset is required for --mode application-insights: Azure CLI silently applies a 1-hour default query window when it is omitted, which can intersect away rows a caller's own KQL predicate (e.g. timestamp > ago(4d)) expects to see. Pass an explicit --offset, e.g. --offset 4d.");
            return 1;
        }

        var arguments = BuildAzArguments(options);

        AzCliProcessResult processResult;
        try
        {
            processResult = await runner(arguments, cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine("[FAIL] az CLI invocation failed to start");
            Console.WriteLine($"       {exception.GetType().Name}: {exception.Message}");
            return 1;
        }

        if (processResult.ExitCode != 0)
        {
            Console.WriteLine($"[FAIL] az CLI exited with code {processResult.ExitCode}");
            if (!string.IsNullOrWhiteSpace(processResult.StandardError))
            {
                Console.WriteLine($"       {processResult.StandardError.Trim()}");
            }

            return 1;
        }

        var extraction = AzureCliQueryEnvelope.Extract(processResult.StandardOutput, options.ResponseField, options.RowIndex);
        if (!extraction.Success)
        {
            Console.WriteLine($"[FAIL] {extraction.FailureReason}: {extraction.Message}");
            return 1;
        }

        Console.WriteLine($"[PASS] extracted response field '{options.ResponseField}' from row {extraction.SelectedRowIndex} of {extraction.TotalRows}");

        try
        {
            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(options.OutputPath, extraction.RawValue);
        }
        catch (Exception exception)
        {
            Console.WriteLine("[FAIL] could not write fixture output");
            Console.WriteLine($"       {exception.GetType().Name}: {exception.Message}");
            return 1;
        }

        Console.WriteLine($"[PASS] wrote fixture: {options.OutputPath}");

        if (options.WriteProvenance)
        {
            var provenancePath = options.ProvenancePath ?? options.OutputPath + ".provenance.json";
            try
            {
                WriteProvenance(provenancePath, options, extraction, sourceKind);
            }
            catch (Exception exception)
            {
                Console.WriteLine("[FAIL] could not write provenance sidecar");
                Console.WriteLine($"       {exception.GetType().Name}: {exception.Message}");
                return 1;
            }

            Console.WriteLine($"[PASS] wrote provenance: {provenancePath}");
        }

        return 0;
    }

    private static string[] BuildAzArguments(HarvestOptions options) => options.Mode switch
    {
        AzureTelemetryMode.ApplicationInsights =>
            ["monitor", "app-insights", "query", "--app", options.Target, "--analytics-query", options.Query, "--offset", options.Offset!, "-o", "json"],
        AzureTelemetryMode.LogAnalytics =>
            ["monitor", "log-analytics", "query", "--workspace", options.Target, "--analytics-query", options.Query, "-o", "json"],
        _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unknown Azure telemetry mode.")
    };

    private static void WriteProvenance(string path, HarvestOptions options, ExtractionResult extraction, string sourceKind)
    {
        var provenance = new JsonObject
        {
            ["harvestedAtUtc"] = DateTime.UtcNow.ToString("o"),
            ["sourceKind"] = sourceKind,
            ["target"] = options.Target,
            ["selectedRowIndex"] = extraction.SelectedRowIndex,
            ["totalRows"] = extraction.TotalRows
        };

        if (options.Mode == AzureTelemetryMode.ApplicationInsights)
        {
            provenance["offset"] = options.Offset;
        }

        if (options.IncludeQueryText)
        {
            provenance["queryText"] = options.Query;
        }
        else
        {
            provenance["queryFingerprint"] = Fingerprint(options.Query);
        }

        File.WriteAllText(path, provenance.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Fingerprint(string query)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(query));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
