using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class TranscriptionTaskQueue : ITranscriptionTaskQueue
{
    private readonly System.Threading.Channels.Channel<int> _channel =
        System.Threading.Channels.Channel.CreateUnbounded<int>();

    public ValueTask QueueAsync(int transcriptionJobDbId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(transcriptionJobDbId, cancellationToken);
    }

    public ValueTask<int> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}

public sealed class TranscriptionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITranscriptionTaskQueue _taskQueue;
    private readonly ILogger<TranscriptionWorker> _logger;

    public TranscriptionWorker(
        IServiceScopeFactory scopeFactory,
        ITranscriptionTaskQueue taskQueue,
        ILogger<TranscriptionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingTasksAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var transcriptionJobDbId = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var transcriptionService = scope.ServiceProvider.GetRequiredService<ITranscriptionService>();
                await transcriptionService.ProcessQueuedTranscriptionAsync(transcriptionJobDbId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Transcription background worker failed for job {TranscriptionJobDbId}", transcriptionJobDbId);
            }
        }
    }

    private async Task RecoverPendingTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeeMusicDbContext>();

        var pendingJobs = await dbContext.TranscriptionJobs
            .Where(item => item.Status == "queued" || item.Status == "processing")
            .ToListAsync(cancellationToken);

        if (pendingJobs.Count == 0)
        {
            return;
        }

        foreach (var job in pendingJobs)
        {
            job.Status = "queued";
            job.Progress = 0;
            job.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var job in pendingJobs)
        {
            await _taskQueue.QueueAsync(job.Id, cancellationToken);
        }
    }
}
