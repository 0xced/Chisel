using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace nugraph;

internal class JsonPipeTarget<T>(JsonTypeInfo<T> jsonTypeInfo, Func<Exception> exception) : PipeTarget
{
    private T? _result;
    private JsonException? _exception;

    public override async Task CopyFromAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            _result = await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, cancellationToken) ?? throw exception();
        }
        catch (JsonException jsonException)
        {
            _exception = jsonException;
        }
    }

    public T Result
    {
        get
        {
            if (_result == null)
            {
                if (_exception != null)
                    throw _exception;

                throw new InvalidOperationException($"Result is only available after {nameof(CopyFromAsync)} has executed.");
            }
            return _result;
        }
    }
}