using System.Threading;
using System.Threading.Tasks;
using Spectre.Console.Cli;

namespace nugraph;

internal abstract class CancelableCommand<TSettings>(CancellationToken cancellationToken) : AsyncCommand<TSettings> where TSettings : CommandSettings
{
    public override async Task<int> ExecuteAsync(CommandContext commandContext, TSettings settings)
    {
        return await ExecuteAsync(commandContext, settings, cancellationToken);
    }

    protected abstract Task<int> ExecuteAsync(CommandContext commandContext, TSettings settings, CancellationToken cancellationToken);
}