using growy_server.Models;
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
                var statisticsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();
                return await statisticsService.GetTopGrowth(parameters, ji, ct);
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
                var statisticsService = scope.ServiceProvider.GetRequiredService<IStatisticsService>();

                var entries = await watchlistService.GetSymbolsAsync(parameters.UserId, ct);
                var pairs = entries.Select(e => (e.Symbol, e.Exchange)).ToList();

                return await statisticsService.GetWatchlistStatistics(pairs, parameters.StartUnixDate, parameters.EndUnixDate, ji, ct);
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


    }
}
