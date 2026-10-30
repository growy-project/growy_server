using growy_server.Models;
using growy_server.Strategies;
using Microsoft.Extensions.Caching.Memory;

namespace growy_server.Services
{
    public class StatisticsJobService(IServiceScopeFactory scopeFactory, IHostApplicationLifetime lifetime, ILogger<StatisticsJobService> logger) : IStatisticsJobService
    {
        private readonly IHostApplicationLifetime _lifetime = lifetime;
        public const int KeepJobInListXMinutes = 5;
        public const int PollSlidingScaleXMinutes = 1;

        private static readonly MemoryCache JobList = new(new MemoryCacheOptions { ExpirationScanFrequency = new TimeSpan(0, PollSlidingScaleXMinutes, 0) });

        public Guid StartJob(StartStatisticJobParameters parameters)
        {
            ValidateParameters(parameters);

            var jobInfo = CreateJob(parameters);

            RunJob(jobInfo, async (scope, ji, ct) =>
            {
                var momentum = scope.ServiceProvider.GetRequiredService<IScreeningStrategy<MomentumRequest, SymbolResult>>();
                var request = new MomentumRequest(parameters.Exchange, parameters.StartUnixDate, parameters.EndUnixDate, parameters.MinimumExpectedGrowth);
                return await momentum.ScreenAsync(request, new JobProgress(ji), ct);
            });

            return jobInfo.JobId;
        }

        public Guid StartWatchlistJob(StartWatchlistJobParameters parameters)
        {
            ValidateWatchlistParameters(parameters);

            var jobInfo = CreateJob(startJobParameters: null);

            RunJob(jobInfo, async (scope, ji, ct) =>
            {
                var watchlistService = scope.ServiceProvider.GetRequiredService<IWatchlistService>();
                var momentum = scope.ServiceProvider.GetRequiredService<IScreeningStrategy<MomentumRequest, SymbolResult>>();

                var entries = await watchlistService.GetSymbolsAsync(parameters.UserId, ct);
                if (entries.Count == 0)
                    return new List<SymbolResult>();

                ji.ProcessingMessage = $"Computing statistics for {entries.Count} watchlist symbols";
                var progress = new JobProgress(ji);

                // One query per exchange, since each exchange may live in a different price table.
                var results = new List<SymbolResult>();
                foreach (var group in entries.GroupBy(e => e.Exchange))
                {
                    var symbols = group.Select(e => e.Symbol).Distinct().ToArray();
                    var request = new MomentumRequest(group.Key, parameters.StartUnixDate, parameters.EndUnixDate, Symbols: symbols);
                    results.AddRange(await momentum.ScreenAsync(request, progress, ct));
                }

                return results.OrderByDescending(r => r.PercentageChange).ToList();
            });

            return jobInfo.JobId;
        }

        public StatisticJobInfo GetStatusForJob(Guid jobId)
        {
            return GetJobInfo(jobId);
        }


        private static void ValidateParameters(StartStatisticJobParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            if (parameters?.StartUnixDate == 0)
                throw new BadHttpRequestException("Start date must be greater than zero");
        }

        private static void ValidateWatchlistParameters(StartWatchlistJobParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            if (parameters.StartUnixDate == 0)
                throw new BadHttpRequestException("Start date must be greater than zero");
            if (parameters.EndUnixDate == 0)
                throw new BadHttpRequestException("End date must be greater than zero");
            if (parameters.UserId <= 0)
                throw new BadHttpRequestException("Invalid user id");
        }


        private static StatisticJobInfo CreateJob(StartStatisticJobParameters? startJobParameters)
        {
            var jobInfo = new StatisticJobInfo
            {
                JobId = Guid.NewGuid(),
                StartJobParameters = startJobParameters,
                AutoClearAfterStatusJobCheck = true,
            };

            JobList.Set(jobInfo.JobId, jobInfo, new MemoryCacheEntryOptions
            {
                SlidingExpiration = new TimeSpan(0, 0, KeepJobInListXMinutes, 0),
            });

            return jobInfo;
        }

        private void RunJob(StatisticJobInfo jobInfo, Func<IServiceScope, StatisticJobInfo, CancellationToken, Task<List<SymbolResult>>> work)
        {
            _ = Task.Run(() => RunInBackground(jobInfo, work, _lifetime.ApplicationStopping));
        }

        private static StatisticJobInfo GetJobInfo(Guid jobId)
        {
            ValidateJobId(jobId);

            if (!JobList.TryGetValue(jobId, out StatisticJobInfo? jobInfo) || jobInfo is null)
                throw new BadHttpRequestException($"Invalid job id: {jobId}");

            return jobInfo;
        }

        private static void ValidateJobId(Guid jobId)
        {
            if (jobId == Guid.Empty)
                throw new InvalidOperationException("Invalid job id");
        }

        private async Task RunInBackground(StatisticJobInfo jobInfo, Func<IServiceScope, StatisticJobInfo, CancellationToken, Task<List<SymbolResult>>> work, CancellationToken cancellationToken)
        {
            logger.LogInformation("Background task started for job {JobId}", jobInfo.JobId);

            try
            {
                using var scope = scopeFactory.CreateScope();
                jobInfo.Result = await work(scope, jobInfo, cancellationToken);

                jobInfo.SetJobInfoStatus(100);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background task failed for job {JobId}", jobInfo.JobId);
                jobInfo.SetJobInfoStatus(100, ex.ToString());
            }
        }

        // Reports strategy progress straight onto the polled job. Progress<T> would post
        // to the thread pool and could apply messages out of order.
        private sealed class JobProgress(StatisticJobInfo jobInfo) : IProgress<string>
        {
            public void Report(string value) => jobInfo.ProcessingMessage = value;
        }
    }
}
