using System;
using System.Buffers.Text;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace nugraph;

internal enum MermaidEditorMode
{
    Edit,
    View,
}

internal static class Mermaid
{
    public static Uri GetLiveEditorUri(ReadOnlySpan<byte> data, MermaidEditorMode mode)
    {
        using var memoryStream = new MemoryStream(capacity: 2048);
        using (var zlibStream = new ZLibStream(memoryStream, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new Utf8JsonWriter(zlibStream))
        {
            // See https://github.com/mermaid-js/mermaid-live-editor/blob/dc72838036719637f3947a7c16c0cbbdeba0d73b/src/lib/types.d.ts#L21-L31
            // And https://github.com/mermaid-js/mermaid-live-editor/blob/dc72838036719637f3947a7c16c0cbbdeba0d73b/src/lib/util/state.ts#L10-L23
            writer.WriteStartObject();
            writer.WriteString("code"u8, data);
            writer.WriteString("mermaid"u8, """{"theme":"default"}"""u8);
            writer.WriteBoolean("panZoom"u8, true);
            writer.WriteEndObject();
        }

        // See https://github.com/mermaid-js/mermaid-live-editor/discussions/1291
#if USE_PADDING
        var payload = Convert.ToBase64String(memoryStream.AsSpan()).Replace("/", "_").Replace("+", "-");
#else
        var payload = Base64Url.EncodeToString(memoryStream.AsSpan());
#endif
        return new Uri($"https://mermaid.live/{mode.ToString().ToLowerInvariant()}#pako:{payload}");
    }
}