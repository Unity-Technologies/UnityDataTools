using System;
using System.IO;

namespace UnityDataTools.FileSystem;

/// <summary>
/// A read-only, seekable <see cref="Stream"/> over a <see cref="UnityFile"/>. This lets code that
/// expects a standard Stream read from any path the native filesystem can open, including files
/// inside a mounted archive (e.g. "archive:/CAB-...").
/// </summary>
public class UnityFileStream : Stream
{
    private readonly UnityFile m_File;
    private readonly long m_Length;
    private long m_Position;

    public UnityFileStream(string path)
    {
        m_File = UnityFileSystem.OpenFile(path);
        m_Length = m_File.GetSize();
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => m_Length;

    public override long Position
    {
        get => m_Position;
        set => Seek(value, System.IO.SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0 || m_Position >= m_Length)
            return 0;

        long toRead = Math.Min(count, m_Length - m_Position);

        // UnityFile.Read always fills the destination buffer from index 0, so when the caller
        // wants the data at a non-zero offset we read into a temporary buffer and copy it across.
        byte[] dest = offset == 0 ? buffer : new byte[toRead];

        m_File.Seek(m_Position);
        long actual = m_File.Read(toRead, dest);

        if (offset != 0)
            Buffer.BlockCopy(dest, 0, buffer, offset, (int)actual);

        m_Position += actual;
        return (int)actual;
    }

    public override long Seek(long offset, System.IO.SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            System.IO.SeekOrigin.Begin => offset,
            System.IO.SeekOrigin.Current => m_Position + offset,
            System.IO.SeekOrigin.End => m_Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        if (newPosition < 0)
            throw new IOException("Attempted to seek before the start of the stream.");

        m_Position = newPosition;
        return m_Position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            m_File.Dispose();
        base.Dispose(disposing);
    }
}
