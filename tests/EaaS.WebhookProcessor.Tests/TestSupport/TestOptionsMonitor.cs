using Microsoft.Extensions.Options;

namespace EaaS.WebhookProcessor.Tests.TestSupport;

/// <summary>
/// Minimal in-memory <see cref="IOptionsMonitor{T}"/> for tests. Returns the configured value
/// from <c>CurrentValue</c> / <c>Get</c>; <c>OnChange</c> is a no-op. Use <see cref="Update"/>
/// to simulate a config hot-reload in tests that exercise the kill switch or skew changes.
/// </summary>
internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    where T : class
{
    private T _value;

    public TestOptionsMonitor(T value) => _value = value;

    public T CurrentValue => _value;

    public T Get(string? name) => _value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;

    public void Update(T value) => _value = value;
}

internal static class TestOptionsMonitor
{
    public static TestOptionsMonitor<T> Create<T>(T value) where T : class => new(value);
}
