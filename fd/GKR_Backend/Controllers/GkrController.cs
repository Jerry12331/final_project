using System;
using System.Threading.Tasks;
using GKR_Backend.Services;
using Microsoft.AspNetCore.Mvc;
using GKR_Backend;

namespace GKR_Backend.Controllers
{
    [ApiController]
    [Route("api")]
    public class GkrController : ControllerBase
    {
        private readonly AskStepService _askStepService;

        public GkrController(AskStepService askStepService)
        {
            _askStepService = askStepService;
        }

        [HttpPost("run_gkr")]
        public IActionResult RunGkr([FromBody] GkrRequest request)
        {
            if (request == null || request.Circuit == null || request.Inputs == null)
            {
                return BadRequest("Invalid request data.");
            }

            var service = new GkrService();
            var logs = service.RunGkr(request);

            return Ok(new GkrResponse { Log = logs });
        }

        [HttpPost("ask_step")]
        public async Task<IActionResult> AskStep([FromBody] AskStepRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new { error = "問題不可為空。" });
            }

            if (AskStepService.IsQuestionTooLong(request.Question))
            {
                return BadRequest(new { error = "問題長度不可超過 200 字。" });
            }

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (_askStepService.IsRateLimited(clientIp))
            {
                return StatusCode(429, new { error = "已達每小時最多 20 次的呼叫上限，請稍後再試。" });
            }

            try
            {
                var (answer, fromCache) = await _askStepService.AskAsync(request);
                return Ok(new AskStepResponse { Answer = answer, FromCache = fromCache });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "AI 回答產生失敗，請稍後再試。", detail = ex.Message });
            }
        }
    }
}