using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi.Services;
using PowerGuardCoreApi._Helpers;
using System.Threading.Tasks;

namespace PowerGuardCoreApi.Controllers
{
    [ApiController]
    [Route("api/requests")]
    public class AccessRequestController : ControllerBase
    {
        private readonly IAccessRequestService _accessRequestService;

        public AccessRequestController(IAccessRequestService accessRequestService)
        {
            _accessRequestService = accessRequestService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateRequest([FromBody] CreateAccessRequest model)
        {
            var accountId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            var result = await _accessRequestService.CreateRequestAsync(model, accountId);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("my")]
        public async Task<IActionResult> GetMyRequests()
        {
            var accountId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            var result = await _accessRequestService.GetRequestsByAccountAsync(accountId);
            return Ok(result);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpGet]
        public async Task<IActionResult> GetAllRequests([FromQuery] string? status)
        {
            var result = await _accessRequestService.GetAllRequestsAsync(status);
            return Ok(result);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPost("{id}/process")]
        public async Task<IActionResult> ProcessRequest(int id, [FromBody] ProcessAccessRequest model)
        {
            var adminId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
            
            try
            {
                var result = await _accessRequestService.ProcessRequestAsync(id, model, adminId, ipAddress, browserInfo);
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
