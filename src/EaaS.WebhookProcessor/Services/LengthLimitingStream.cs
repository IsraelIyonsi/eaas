namespace EaaS.WebhookProcessor.Services;

/// <summary>
/// Wraps a request body Stream and enforces a hard byte cap while reading. Exceeding the cap
/// throws <see cref="PayloadTooLargeException"/> — which the endpoint wrapper translates to 413.
/// Necessary because <c>Request.ContentLength</c> is optional (chunked encoding omits it), so we
/// can't rely solely on the header to bound the payload.
/// </summary>
internal sealed class LengthLimitingStream : Stream
{
    private readonly Stream _inner;
    private readonly long _limit;
    private long _read;

    public LengthLimitingStream(Stream inner, long limit)
    {
        _inner = inner;
        _limit = limit;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = _inner.Read(buffer, offset, count);
        Accumulate(n);
        return n;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var n = await _inner.ReadAsync(buffer, cancellationToken);
        Accumulate(n);
        return n;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var n = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        Accumulate(n);
        return n;
    }

    private void Accumulate(int n)
    {
        _read += n;
        if (_read > _limit)
        {
            throw new PayloadTooLargeException(_limit);
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class PayloadTooLargeException : Exception
{
    public PayloadTooLargeException(long limit)
        : base($"Request body exceeded {limit} bytes.")
    {
    }
}
