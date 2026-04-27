using Microsoft.AspNetCore.Mvc;
using growy_server.Services;
using growy_server.Models;

namespace growy_server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class StatisticsController(IStatisticsJobService statisticsJobService, IStatisticsService statisticsService) : ControllerBase
    {
        [HttpPost("start")]
        public IActionResult StartJob([FromBody] StartStatisticJobParameters parameters)
        {
            try
            {
                var jobId = statisticsJobService.StartJob(parameters);
                return Ok(new { JobId = jobId });
            }
            catch (BadHttpRequestException ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpGet("history/{symbol}")]
        public async Task<IActionResult> GetSymbolHistory(string symbol, [FromQuery] GetSymbolHistoryParameters parameters, CancellationToken cancellationToken)
        {
            var result = await statisticsService.GetSymbolHistory(symbol, parameters, cancellationToken);
            return Ok(result);
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
