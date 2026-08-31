namespace ImageEditMcp.TestingUtilities.FileSystem;

internal sealed class FakeFileSystemStream(FakeFileEntry entry, bool writable) : Stream
{
    private readonly FakeFileEntry _entry = entry;
    private readonly bool _writable = writable;
    private MemoryStream _stream = CreateStream(entry);

    private static MemoryStream CreateStream(FakeFileEntry entry)
    {
        var stream = new MemoryStream();
        if (entry.Content.Length > 0)
        {
            stream.Write(entry.Content, 0, entry.Content.Length);
            stream.Position = 0;
        }

        return stream;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => _writable;

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public override void Flush()
    {
        _entry.Content = _stream.ToArray();
    }

    public override int Read(byte[] buffer, int offset, int count)
        => _stream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin)
        => _stream.Seek(offset, origin);

    public override void SetLength(long value)
        => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!_writable)
        {
            throw new IOException("Stream is not writable.");
        }

        _stream.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Flush();
            _stream.Dispose();
        }

        base.Dispose(disposing);
    }
}
