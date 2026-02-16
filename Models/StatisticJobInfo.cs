
namespace growy_server.Models
{
    public class StatisticJobInfo
    {
        public StatisticJobInfo()
        {
            SetJobInfoStatus(0);
        }

        private Guid jobId;

        public StartStatisticJobParameters StartJobParameters { get; set; }

        //removed from list after 100% status has been checked

        public bool AutoClearAfterStatusJobCheck { get; set; } = true;

        public Guid JobId { get => jobId; set { jobId = value; } }

        public string Errors { get; set; }

        public StatisticsJobStatus Status { get; set; }

        public List<SymbolResult> Result { get; set; }

        public int PercentComplete { get; set; }

        public string ProcessingMessage { get; set; } = "";

        public int CurrentPage { get; set; } = 1;


        public void SetJobInfoStatus(int percentComplete, string errors = null)
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
