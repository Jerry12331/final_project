using GKR_Backend.Services;
using Microsoft.AspNetCore.Mvc;
using GKR_Backend;

namespace GKR_Backend.Controllers
{
    [ApiController]
    [Route("api")]
    public class GkrController : ControllerBase
    {
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
    }
}