using System;
using System.IO;

namespace nugraph;

public static class MemoryStreamExtensions
{
    // Because MemoryStream.InternalReadSpan() is not public, see https://github.com/dotnet/runtime/issues/106524
    public static Span<byte> AsSpan(this MemoryStream stream)
    {
        return stream.GetBuffer().AsSpan(0, Convert.ToInt32(stream.Position));
    }
}