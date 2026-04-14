using EaaS.WebhookProcessor.Services;
using FluentAssertions;
using Xunit;

namespace EaaS.WebhookProcessor.Tests;

public class LengthLimitingStreamTests
{
    [Fact]
    public async Task Read_BelowLimit_Succeeds()
    {
        var inner = new MemoryStream(new byte[1000]);
        using var stream = new LengthLimitingStream(inner, 256_000);

        var buffer = new byte[1024];
        var n = await stream.ReadAsync(buffer);

        n.Should().Be(1000);
    }

    [Fact]
    public async Task Read_Exceeding256KB_Throws_PayloadTooLarge()
    {
        var inner = new MemoryStream(new byte[300_000]);
        using var stream = new LengthLimitingStream(inner, 256_000);

        var buffer = new byte[300_000];
        var act = async () => await stream.ReadAsync(buffer);

        await act.Should().ThrowAsync<PayloadTooLargeException>();
    }

    [Fact]
    public async Task Read_In_Chunks_Throws_When_Cumulative_Exceeds_Limit()
    {
        var inner = new MemoryStream(new byte[300_000]);
        using var stream = new LengthLimitingStream(inner, 256_000);

        var buffer = new byte[64_000];
        PayloadTooLargeException? thrown = null;
        try
        {
            while (true)
            {
                var n = await stream.ReadAsync(buffer);
                if (n == 0) break;
            }
        }
        catch (PayloadTooLargeException ex) { thrown = ex; }

        thrown.Should().NotBeNull();
    }
}
