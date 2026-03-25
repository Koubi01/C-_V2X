using Microsoft.Extensions.Options;
using V2XDashboard.Server.Services.PcapReader.Interfaces;
using V2XDashboard.Shared;

namespace V2XDashboard.Server.Services.PcapReader.Scheduling;

public sealed class IngestionSchedulerService : BackgroundService, IIngestionScheduler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<IngestionSchedulerOptions> _optionsMonitor;
    private readonly ILogger<IngestionSchedulerService> _logger;
    private readonly Lock _statusLock = new();

    private bool _isRunning;
    private DateTime? _lastRunStartedAtUtc;
    private DateTime? _lastRunFinishedAtUtc;
    private bool? _lastRunSucceeded;
    private long? _lastRunDurationMs;
    private string? _lastError;
    private long _totalRuns;

    public IngestionSchedulerService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<IngestionSchedulerOptions> optionsMonitor,
        ILogger<IngestionSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        _isRunning = optionsMonitor.CurrentValue.Enabled;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingestion scheduler background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var intervalSeconds = Math.Max(1, options.IntervalSeconds);

            if (!options.IsSchedulerNode)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                continue;
            }

            bool runNow;
            lock (_statusLock)
            {
                runNow = _isRunning;
            }

            if (!runNow)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                continue;
            }

            var startedAt = DateTime.UtcNow;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var success = false;
            string? error = null;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ingestionService = scope.ServiceProvider.GetRequiredService<IPcapIngestionService>();
                success = await ingestionService.ProcessAllPcapFilesAsync();
            }
            catch (Exception ex)
            {
                success = false;
                error = ex.Message;
                _logger.LogError(ex, "Scheduled ingestion run failed.");
            }
            finally
            {
                stopwatch.Stop();

                lock (_statusLock)
                {
                    _totalRuns++;
                    _lastRunStartedAtUtc = startedAt;
                    _lastRunFinishedAtUtc = DateTime.UtcNow;
                    _lastRunSucceeded = success;
                    _lastRunDurationMs = stopwatch.ElapsedMilliseconds;
                    _lastError = error;
                }

                if (success)
                {
                    _logger.LogInformation(
                        "Scheduled ingestion run succeeded in {DurationMs}ms.",
                        stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning(
                        "Scheduled ingestion run failed in {DurationMs}ms. Error={Error}",
                        stopwatch.ElapsedMilliseconds,
                        error ?? "unknown");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Ingestion scheduler background service stopped.");
    }

    public Task<IngestionSchedulerStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateStatusSnapshot());
    }

    public Task<IngestionSchedulerStatusDto> StartSchedulingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_statusLock)
        {
            _isRunning = true;
        }

        _logger.LogInformation("Ingestion scheduler was started via control API.");
        return Task.FromResult(CreateStatusSnapshot());
    }

    public Task<IngestionSchedulerStatusDto> StopSchedulingAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_statusLock)
        {
            _isRunning = false;
        }

        _logger.LogInformation("Ingestion scheduler was stopped via control API.");
        return Task.FromResult(CreateStatusSnapshot());
    }

    private IngestionSchedulerStatusDto CreateStatusSnapshot()
    {
        var options = _optionsMonitor.CurrentValue;
        lock (_statusLock)
        {
            return new IngestionSchedulerStatusDto
            {
                IsSchedulerNode = options.IsSchedulerNode,
                IsRunning = _isRunning,
                IntervalSeconds = Math.Max(1, options.IntervalSeconds),
                LastRunStartedAtUtc = _lastRunStartedAtUtc,
                LastRunFinishedAtUtc = _lastRunFinishedAtUtc,
                LastRunSucceeded = _lastRunSucceeded,
                LastRunDurationMs = _lastRunDurationMs,
                LastError = _lastError,
                TotalRuns = _totalRuns
            };
        }
    }
}
