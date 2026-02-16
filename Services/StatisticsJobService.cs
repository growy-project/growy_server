using growy_server.Models;
using Microsoft.Extensions.Caching.Memory;

namespace growy_server.Services
{
    public class StatisticsJobService(IStatisticsService statisticsService) : IStatisticsJobService
    {
        public const int KeepJobInListXMinutes = 5;
        public const int PollSlidingScaleXMinutes = 1;

        private static readonly MemoryCache JobList = new(new MemoryCacheOptions { ExpirationScanFrequency = new TimeSpan(0, PollSlidingScaleXMinutes, 0) });

        public Guid StartJob(StartStatisticJobParameters parameters)
        {
            ValidateParameters(parameters);

            var jobInfo = CreateJob(parameters);

            RunJob(jobInfo);

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
                throw new BadHttpRequestException("Start date is null");
        }


        private static StatisticJobInfo CreateJob(StartStatisticJobParameters parameters)
        {
            var jobInfo = new StatisticJobInfo
            {
                JobId = Guid.NewGuid(),
                StartJobParameters = parameters,
                AutoClearAfterStatusJobCheck = true,
                PercentComplete = 0,
                Status = StatisticsJobStatus.InProgress,
            };

            JobList.Set(jobInfo.JobId, jobInfo, new MemoryCacheEntryOptions
            {
                SlidingExpiration = new TimeSpan(0, 0, KeepJobInListXMinutes, 0),
            });

            return jobInfo;
        }

        private void RunJob(StatisticJobInfo jobInfo)
        {
            _ = Task.Run(() => RunInBackground(jobInfo));
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

        private async Task RunInBackground(StatisticJobInfo jobInfo)
        {
            Console.WriteLine("Background task started...");

            try
            {
                jobInfo.Result = await statisticsService.GetTopGrowth(jobInfo.StartJobParameters, jobInfo);

                //add volatility result and 50% 
                jobInfo.SetJobInfoStatus(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                jobInfo.SetJobInfoStatus(100, ex.ToString());
            }
        }


    }
}
