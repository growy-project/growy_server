using growy_server.Models;

namespace growy_server.Services
{
    public interface IStatisticsJobService
    {
        Guid StartJob(StartStatisticJobParameters parameters);

        Guid StartWatchlistJob(StartWatchlistJobParameters parameters);

        StatisticJobInfo GetStatusForJob(Guid jobId);
    }
}
