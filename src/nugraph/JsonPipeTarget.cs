using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace nugraph;

internal class JsonPipeTarget<T>(JsonTypeInfo<T> jsonTypeInfo) : PipeTarget
{
    public override async Task CopyFromAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        Result = await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken);
    }

    public T? Result { get; private set; }
}