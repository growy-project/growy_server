using Microsoft.AspNetCore.Mvc;
using growy_server.Services;
using growy_server.Models;

namespace growy_server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StatisticsController(IStatisticsJobService statisticsJobService) : ControllerBase
    {
        [HttpPost("start")]
        public IActionResult StartJob([FromBody] StartStatisticJobParameters parameters)
        {
            //Missing try catch here
            var jobId = statisticsJobService.StartJob(parameters);
            return Ok(new { JobId = jobId });
        }

        [HttpGet("status/{jobId}")]
        public IActionResult GetJobStatus(Guid jobId)
        {
            var jobInfo = statisticsJobService.GetStatusForJob(jobId);

            if (jobInfo == null)
            {
                return NotFound(new { Message = "Job not found" });
            }

            return Ok(jobInfo);
        }
    }
}
