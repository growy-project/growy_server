
namespace growy_server.Models
{
    public class StatisticJobInfo
    {
        public StatisticJobInfo()
        {
            SetJobInfoStatus(0);
        }

        public required StartStatisticJobParameters StartJobParameters { get; init; }

        public bool AutoClearAfterStatusJobCheck { get; init; } = true;

        public Guid JobId { get; init; }

        public string? Errors { get; private set; }

        public StatisticsJobStatus Status { get; private set; }

        public List<SymbolResult> Result { get; internal set; } = new();

        public int PercentComplete { get; private set; }

        public string ProcessingMessage { get; internal set; } = "";

        public int CurrentPage { get; set; } = 1;


        public void SetJobInfoStatus(int percentComplete, string? errors = null)
        {
            PercentComplete = percentComplete;
            Errors = errors;

            Status = percentComplete switch
            {
                100 => !string.IsNullOrEmpty(Errors) ? StatisticsJobStatus.CompletedWithErrors : StatisticsJobStatus.Completed,
                > 0 => StatisticsJobStatus.InProgress,
                _ => StatisticsJobStatus.NotStarted
            };
        }

        public bool IsFinished => Status == StatisticsJobStatus.Completed || Status == StatisticsJobStatus.CompletedWithErrors;
    }
}
