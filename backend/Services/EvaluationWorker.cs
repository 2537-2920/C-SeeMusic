using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class EvaluationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEvaluationTaskQueue _taskQueue;
    private readonly ILogger<EvaluationWorker> _logger;

    public EvaluationWorker(
        IServiceScopeFactory scopeFactory,
        IEvaluationTaskQueue taskQueue,
        ILogger<EvaluationWorker> logger)
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
            var evaluationDbId = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var evaluationService = scope.ServiceProvider.GetRequiredService<IEvaluationService>();
                await evaluationService.ProcessQueuedEvaluationAsync(evaluationDbId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Evaluation background worker failed for job {EvaluationDbId}", evaluationDbId);
            }
        }
    }

    private async Task RecoverPendingTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SeeMusicDbContext>();

        var pendingEvaluations = await dbContext.Evaluations
            .Where(item => item.Status == "queued" || item.Status == "processing")
            .ToListAsync(cancellationToken);

        if (pendingEvaluations.Count == 0)
        {
            return;
        }

        foreach (var evaluation in pendingEvaluations)
        {
            evaluation.Status = "queued";
            evaluation.Progress = 0;
            evaluation.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var evaluation in pendingEvaluations)
        {
            await _taskQueue.QueueAsync(evaluation.Id, cancellationToken);
        }
    }
}
