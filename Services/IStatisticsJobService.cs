using growy_server.Models;

namespace growy_server.Services
{
    public interface IStatisticsJobService
    {
        Guid StartJob(StartStatisticJobParameters parameters);

        StatisticJobInfo GetStatusForJob(Guid jobId);
    }
}
