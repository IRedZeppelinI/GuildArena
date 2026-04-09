using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuildArena.Application.Combat.AI.BackgroundServices;

/// <summary>
/// A long-running background service that listens to the AI turn queue 
/// and executes the AI orchestration safely outside of the HTTP request lifecycle.
/// </summary>
public class AiTurnWorker : BackgroundService
{
    private readonly IAiTurnQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiTurnWorker> _logger;

    public AiTurnWorker(
        IAiTurnQueue queue,
        IServiceProvider serviceProvider,
        ILogger<AiTurnWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI Turn Background Worker is starting.");

        // Keep processing as long as the application is running
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // This will asynchronously wait/yield until an item is added to the queue
                var request = await _queue.DequeueAsync(stoppingToken);

                await ProcessTurnSafelyAsync(request, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down, do nothing
                _logger.LogInformation("AI Turn Background Worker is stopping gracefully.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "A fatal error occurred in the AI Turn Background Worker loop.");
                break;
            }
        }
    }

    private async Task ProcessTurnSafelyAsync(AiTurnRequest request, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing AI turn for Combat {CombatId}.", request.CombatId);

        // We MUST create a new scope. Background services are singletons. 
        // Scoped services like IBattleLogService, ICombatStateRepository or DbContexts
        // need to be isolated per execution to prevent data leaks or concurrency issues.
        using var scope = _serviceProvider.CreateScope();

        try
        {
            var orchestrator = scope.ServiceProvider.GetRequiredService<IAiTurnOrchestrator>();

            // Pass the execution to the orchestrator.
            // We do not pass the stoppingToken directly to the combat logic to avoid aborting 
            // midway through a database save if the server shuts down, preserving state integrity.
            await orchestrator.PlayTurnAsync(request.CombatId, request.AiPlayerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error executing AI turn for Combat {CombatId}, Player {PlayerId}.",
                request.CombatId,
                request.AiPlayerId);
        }
    }
}