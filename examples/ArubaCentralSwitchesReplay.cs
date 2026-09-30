#!/usr/bin/env dotnet
#:include ../src/Fidelity/Replay.cs
#:package Refit@6.3.2
#:package Refit.Newtonsoft.Json@6.3.2
#:package ArubaCentral.Api@1.0.14


using Newtonsoft.Json;
using Refit;
using System.Collections.Generic;
using ArubaCentral.Api.Monitoring;

var replay = new ReplayHttpMessageHandler(ReplayFixture.Read("fixtures/aruba-central-switches.json"));
using var httpClient = new HttpClient(replay)
{
    BaseAddress = new Uri("https://fidelity.invalid/")
};
var client = RestService.For<IArubaSwitchApi>(httpClient, new RefitSettings
{
    ContentSerializer = new NewtonsoftJsonContentSerializer(new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore
    })
});

var switchResponse = await client.GetSwitches();
Console.WriteLine("CASE: Aruba Central switches replay");

if (switchResponse.Error is null)
{
    Console.WriteLine("[FAIL] expected Refit ApiResponse error was not populated");
    return 1;
}

Console.WriteLine($"[PASS] Refit ApiResponse wrapper returned (requests={replay.RequestCount})");
if (switchResponse.Content is not null)
{
    Console.WriteLine("[FAIL] typed content was unexpectedly produced");
    return 1;
}

Console.WriteLine("[PASS] typed content is null after deserialization failure");
var errorMessage = switchResponse.Error.InnerException?.Message ?? switchResponse.Error.Message;
if (!errorMessage.Contains("required property", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"[FAIL] unexpected Aruba SDK error: {errorMessage}");
    return 1;
}

Console.WriteLine("[PASS] expected Aruba SDK required-property error captured in wrapper");
Console.WriteLine($"       {errorMessage}");

var completeReplay = new ReplayHttpMessageHandler(ReplayFixture.Read("fixtures/aruba-central-switches-complete.json"));
using var completeHttpClient = new HttpClient(completeReplay)
{
    BaseAddress = new Uri("https://fidelity.invalid/")
};
var completeClient = RestService.For<IArubaSwitchApi>(completeHttpClient, new RefitSettings
{
    ContentSerializer = new NewtonsoftJsonContentSerializer(new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore
    })
});

var completeResponse = await completeClient.GetSwitches();
if (completeResponse.Error is not null || completeResponse.Content is null)
{
    var completeError = completeResponse.Error?.InnerException?.Message ?? completeResponse.Error?.Message ?? "content was null";
    Console.WriteLine($"[FAIL] complete Aruba response did not deserialize: {completeError}");
    return 1;
}

var page = new ArubaInventorySwitchPage
{
    Total = completeResponse.Content.Total,
    Switches = completeResponse.Content.Switches.Select(item => new ArubaInventorySwitch
    {
        Serial = item.Serial,
        Model = item.Model,
        FirmwareVersion = item.FirmwareVersion,
        Name = item.Name,
        IpAddress = item.IpAddress,
        Macaddr = item.Macaddr,
        Status = item.Status.ToString(),
        PublicIpAddress = item.PublicIpAddress
    }).ToList()
};

var mappedSwitch = page.Switches.Single();
if (page.Total != 1 || mappedSwitch.FirmwareVersion != "FL.10.10.0010")
{
    Console.WriteLine($"[FAIL] Aruba SDK response did not preserve required switch semantics in the page DTO: total={page.Total}, switches={page.Switches.Count}, firmware={mappedSwitch.FirmwareVersion}");
    return 1;
}

Console.WriteLine("[PASS] complete Aruba response returned typed content");
Console.WriteLine("[PASS] SDK response mapped to a Durable-safe page DTO");
Console.WriteLine($"       switches={page.Switches.Count}, firmware_version={mappedSwitch.FirmwareVersion}");
return 0;


public interface IArubaSwitchApi 
{
    [Get("/monitoring/v1/switches")]
    Task<ApiResponse<SwitchList>> GetSwitches();
}

public sealed class ArubaInventorySwitchPage
{
    public double Total { get; set; }
    public List<ArubaInventorySwitch> Switches { get; set; } = new();
}

public sealed class ArubaInventorySwitch
{
    public string? Serial { get; set; }
    public string? Model { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? Name { get; set; }
    public string? IpAddress { get; set; }
    public string? Macaddr { get; set; }
    public string? Status { get; set; }
    public string? PublicIpAddress { get; set; }
}

