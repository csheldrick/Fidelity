using System.Net;
using System.Net.Http;
using System.Text;

public sealed class ReplayHttpMessageHandler : HttpMessageHandler
{
    private readonly string rawResponse;
    private readonly HttpStatusCode statusCode;
    private readonly string mediaType;

    public ReplayHttpMessageHandler(
        string rawResponse,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string mediaType = "application/json")
    {
        this.rawResponse = rawResponse;
        this.statusCode = statusCode;
        this.mediaType = mediaType;
    }

    public int RequestCount { get; private set; }

    public HttpStatusCode LastStatusCode { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastStatusCode = statusCode;

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(rawResponse, Encoding.UTF8, mediaType),
            RequestMessage = request
        };

        return Task.FromResult(response);
    }
}

public static class ReplayFixture
{
    public static string Read(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        return File.ReadAllText(path);
    }
}

public static class Fidelity
{
    public static async Task<int> RunAsync<T>(
        string caseName,
        ReplayHttpMessageHandler replay,
        Func<Task<T>> invoke,
        Action<SemanticExpectations<T>> assertSemantics)
    {
        Console.WriteLine($"CASE: {caseName}");

        T result;
        try
        {
            result = await invoke();
        }
        catch (Exception exception)
        {
            Console.WriteLine("[FAIL] transport/client invocation failed");
            Console.WriteLine($"       {exception.GetType().Name}: {exception.Message}");
            if (exception.InnerException is not null)
            {
                Console.WriteLine($"       inner: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}");
            }
            return 1;
        }

        if (replay.RequestCount == 0)
        {
            Console.WriteLine("[FAIL] transport/client invocation did not reach the replay handler");
            return 1;
        }

        Console.WriteLine(
            $"[PASS] transport/client invocation succeeded (HTTP {(int)replay.LastStatusCode}, requests={replay.RequestCount})");

        if (result is null)
        {
            Console.WriteLine("[FAIL] typed result was not produced");
            return 1;
        }

        Console.WriteLine($"[PASS] typed result produced ({typeof(T).Name})");

        var expectations = new SemanticExpectations<T>(result);
        assertSemantics(expectations);

        if (expectations.Failures.Count > 0)
        {
            Console.WriteLine("[FAIL] required semantic expectation failed");
            foreach (var failure in expectations.Failures)
            {
                Console.WriteLine($"       {failure}");
            }

            return 1;
        }

        Console.WriteLine("[PASS] required semantic expectations");
        Console.WriteLine($"[PASS] {caseName}");
        return 0;
    }
}

public sealed class SemanticExpectations<T>
{
    private readonly T subject;

    public SemanticExpectations(T subject)
    {
        this.subject = subject;
    }

    public List<string> Failures { get; } = [];

    public void Equal(string path, object? expected, Func<T, object?> actual)
    {
        object? actualValue;
        try
        {
            actualValue = actual(subject);
        }
        catch (Exception exception)
        {
            Failures.Add($"{path}: could not observe typed value ({exception.Message})");
            return;
        }

        if (!Equals(expected, actualValue))
        {
            Failures.Add(
                $"{path}: expected {Format(expected)}, actual {Format(actualValue)}");
        }
    }

    private static string Format(object? value)
    {
        return value is null ? "<null>" : value is string text ? $"\"{text}\"" : value.ToString()!;
    }
}
