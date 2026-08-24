#!/usr/bin/env dotnet
#:include ../src/Fidelity/Harvest.cs

// Read-only Application Insights / Log Analytics fixture harvester.
//
// Authentication relies entirely on the caller's existing `az login`
// context; this tool never stores, refreshes, or otherwise manages Azure
// credentials. It runs one caller-owned KQL query, extracts one configured
// response-body field, and writes it byte-for-byte as an ordinary Fidelity
// fixture that the existing replay transport can consume unmodified.
//
// Usage:
//   dotnet run --file tools/HarvestApplicationInsights.cs -- \
//     --mode application-insights|log-analytics \
//     --target <app-insights-app-id-or-name | log-analytics-workspace-id> \
//     --query "<KQL>" | --query-file <path> \
//     --response-field <column-name> \
//     --output <fixture-path> \
//     --offset <value> (required for --mode application-insights; passed through
//                        unchanged to `az monitor app-insights query --offset`,
//                        which otherwise silently defaults to a 1-hour window) \
//     [--row-index <n>] \
//     [--no-provenance] [--provenance <path>] [--include-query-text]

HarvestOptions options;
try
{
    options = ParseArguments(args);
}
catch (HarvestArgumentException)
{
    return 2;
}

return await HarvestRunner.RunAsync(options, AzCliProcess.RunAsync);

static HarvestOptions ParseArguments(string[] args)
{
    AzureTelemetryMode? mode = null;
    string? target = null;
    string? query = null;
    string? queryFile = null;
    string? responseField = null;
    string? output = null;
    int? rowIndex = null;
    var writeProvenance = true;
    string? provenancePath = null;
    var includeQueryText = false;
    string? offset = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--mode":
                mode = RequireValue(args, ref i) switch
                {
                    "application-insights" => AzureTelemetryMode.ApplicationInsights,
                    "log-analytics" => AzureTelemetryMode.LogAnalytics,
                    var other => Invalid<AzureTelemetryMode?>($"Unknown --mode value '{other}'. Expected application-insights or log-analytics.")
                };
                break;
            case "--target":
                target = RequireValue(args, ref i);
                break;
            case "--query":
                query = RequireValue(args, ref i);
                break;
            case "--query-file":
                queryFile = RequireValue(args, ref i);
                break;
            case "--response-field":
                responseField = RequireValue(args, ref i);
                break;
            case "--output":
                output = RequireValue(args, ref i);
                break;
            case "--row-index":
                rowIndex = int.Parse(RequireValue(args, ref i));
                break;
            case "--no-provenance":
                writeProvenance = false;
                break;
            case "--provenance":
                provenancePath = RequireValue(args, ref i);
                break;
            case "--include-query-text":
                includeQueryText = true;
                break;
            case "--offset":
                offset = RequireValue(args, ref i);
                break;
            default:
                return Invalid<HarvestOptions>($"Unknown argument '{args[i]}'.");
        }
    }

    mode ??= AzureTelemetryMode.ApplicationInsights;

    if (query is not null && queryFile is not null)
    {
        return Invalid<HarvestOptions>("Pass --query or --query-file, not both.");
    }

    query ??= queryFile is not null ? File.ReadAllText(queryFile) : null;

    var missing = new List<string>();
    if (target is null) missing.Add("--target");
    if (query is null) missing.Add("--query or --query-file");
    if (responseField is null) missing.Add("--response-field");
    if (output is null) missing.Add("--output");
    if (mode == AzureTelemetryMode.ApplicationInsights && offset is null)
    {
        missing.Add("--offset (required for --mode application-insights; az monitor app-insights query otherwise silently defaults to a 1-hour query window)");
    }

    if (missing.Count > 0)
    {
        return Invalid<HarvestOptions>($"Missing required argument(s): {string.Join(", ", missing)}");
    }

    return new HarvestOptions(
        mode.Value,
        target!,
        query!,
        responseField!,
        output!,
        rowIndex,
        writeProvenance,
        provenancePath,
        includeQueryText,
        offset);
}

static string RequireValue(string[] args, ref int i)
{
    if (i + 1 >= args.Length)
    {
        return Invalid<string>($"Argument '{args[i]}' requires a value.");
    }

    return args[++i];
}

static T Invalid<T>(string message)
{
    Console.WriteLine($"[FAIL] {message}");
    throw new HarvestArgumentException();
}

internal sealed class HarvestArgumentException : Exception;
