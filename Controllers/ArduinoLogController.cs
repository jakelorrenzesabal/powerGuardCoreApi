using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PowerGuardCoreApi._Helpers;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi.Services;
using System.Threading.Tasks;

namespace PowerGuardCoreApi.Controllers
{
    [ApiController]
    [Route("api/arduino/log")]
    public class ArduinoLogController : ControllerBase
    {
        private readonly IArduinoLogService _arduinoLogService;

        public ArduinoLogController(IArduinoLogService arduinoLogService)
        {
            _arduinoLogService = arduinoLogService;
        }

        // POST /api/arduino/log
        [HttpPost]
        public async Task<IActionResult> LogEvent([FromBody] LogEventRequest request)
        {
            var result = await _arduinoLogService.ProcessEventAsync(request);
            return Ok(result);
        }

        // GET /api/arduino/log
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllLogs([FromQuery] LogFilterRequest filters)
        {
            var logs = await _arduinoLogService.GetAllLogsAsync(filters);
            return Ok(new { success = true, logs });
        }

        // GET /api/arduino/log/account/{AccountId}
        [Authorize]
        [HttpGet("account/{AccountId}")]
        public async Task<IActionResult> GetAccountLogs(int AccountId, [FromQuery] LogFilterRequest filters)
        {
            var logs = await _arduinoLogService.GetAccountLogsAsync(AccountId, filters);
            return Ok(new { success = true, logs });
        }

        // GET /api/arduino/log/room/{roomId}
        [Authorize]
        [HttpGet("room/{roomId}")]
        public async Task<IActionResult> GetRoomLogs(int roomId, [FromQuery] LogFilterRequest filters)
        {
            var logs = await _arduinoLogService.GetRoomLogsAsync(roomId, filters);
            return Ok(new { success = true, logs });
        }

        // GET /api/arduino/log/counts
        [Authorize]
        [HttpGet("counts")]
        public async Task<IActionResult> GetLogCounts([FromQuery] LogCountFilterRequest filters)
        {
            // If not Admin, force filter by their own AccountId
            if (!User.IsInRole(UserRoles.Admin))
            {
                filters.AccountId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            }
            var result = await _arduinoLogService.GetLogCountsAsync(filters);
            return Ok(new { success = true, result.Total, result.ByEventType });
        }

        // GET /api/arduino/log/validation-attempts
        [Authorize]
        [HttpGet("/api/arduino/log/validation-attempts")]
        public async Task<IActionResult> GetAllValidationAttempts([FromQuery] ValidationAttemptFilterRequest filters)
        {
            var attempts = await _arduinoLogService.GetAllValidationAttemptsAsync(filters);
            return Ok(new { success = true, attempts });
        }

        // GET /api/arduino/log/blocked
        [Authorize]
        [HttpGet("/api/arduino/log/blocked")]
        public async Task<IActionResult> GetBlockedUids()
        {
            var uids = await _arduinoLogService.GetAllBlockedUidsAsync();
            return Ok(new { success = true, uids });
        }

        // POST /api/arduino/log/unblock
        [Authorize(Roles = UserRoles.Admin)]
        [HttpPost("/api/arduino/log/unblock")]
        public async Task<IActionResult> UnblockUid([FromBody] UnblockUidRequest request)
        {
            var result = await _arduinoLogService.UnblockUidAsync(request.Uid);
            return Ok(result);
        }
    }
}