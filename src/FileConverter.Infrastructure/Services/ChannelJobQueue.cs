using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FileConverter.Domain.Interfaces;

namespace FileConverter.Infrastructure.Services;

public class ChannelJobQueue : IJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public async ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public async IAsyncEnumerable<Guid> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var jobId in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return jobId;
        }
    }
}
