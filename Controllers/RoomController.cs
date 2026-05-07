using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi.Services;
using PowerGuardCoreApi._Helpers;
using System.Threading.Tasks;

namespace PowerGuardCoreApi.Controllers
{
    [ApiController]
    [Route("api/room")]
    public class RoomController : ControllerBase
    {
        private readonly IRoomService _roomService;

        public RoomController(IRoomService roomService)
        {
            _roomService = roomService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest model)
        {
            var accountId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            var (room, deviceKey) = await _roomService.CreateRoomAsync(model, accountId);
            return Ok(new { success = true, room, deviceKey });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllRooms()
        {
            var isAdmin = User.IsInRole(UserRoles.Admin);
            var accountId = isAdmin ? (int?)null : int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            var (rooms, count) = await _roomService.GetAllRoomsAsync(accountId);
            return Ok(new { success = true, count, rooms });
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRoomById(int id)
        {
            var room = await _roomService.GetRoomByIdAsync(id);
            if (room == null)
                return NotFound(new { success = false, message = "Room not found" });
            return Ok(new { success = true, room });
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRoom(int id, [FromBody] UpdateRoomRequest model)
        {
            var accountId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            try
            {
                var room = await _roomService.UpdateRoomAsync(id, model, accountId);
                return Ok(new { success = true, room });
            }
            catch (RoomInactiveException ex)
            {
                return BadRequest(new { success = false, message = ex.Message, errorType = "ROOM_INACTIVE" });
            }
        }

        [Authorize]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateRoomStatus(int id, [FromBody] UpdateRoomStatusRequest model)
        {
            var accountId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");

            var room = await _roomService.UpdateRoomStatusAsync(id, model.IsActive, accountId);
            return Ok(new { success = true, room });
        }

        [Authorize]
        [HttpPatch("{id}/power")]
        public async Task<IActionResult> TogglePowerStatus(int id, [FromBody] TogglePowerStatusRequest model)
        {
            var accountId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            var room = await _roomService.GetRoomByIdAsync(id);
            if (room == null || !room.IsActive)
                return BadRequest(new { success = false, message = "Cannot toggle power status for inactive room", errorType = "ROOM_INACTIVE" });

            try
            {
                var updatedRoom = await _roomService.TogglePowerStatusAsync(id, model.Status, accountId);
                return Ok(new { success = true, message = $"Power turned {model.Status}", room = updatedRoom });
            }
            catch (DeviceOfflineException ex)
            {
                return StatusCode(503, new { success = false, message = ex.Message, errorType = "DEVICE_OFFLINE" });
            }
        }

        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetRoomsByPowerStatus(string status)
        {
            var isAdmin = User.IsInRole(UserRoles.Admin);
            var accountId = isAdmin ? (int?)null : int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            var result = await _roomService.GetRoomsByPowerStatusAsync(status, accountId);
            return Ok(result);
        }

        [Authorize]
        [HttpGet("power-status")]
        public async Task<IActionResult> GetRoomsByPowerStatusQuery([FromQuery] string status)
        {
            var isAdmin = User.IsInRole(UserRoles.Admin);
            var accountId = isAdmin ? (int?)null : int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
            var result = await _roomService.GetRoomsByPowerStatusAsync(status, accountId);
            return Ok(result);
        }

        [HttpGet("arduino/power-status")]
        public async Task<IActionResult> GetPowerStatus([FromQuery] string deviceId)
        {
            var result = await _roomService.GetPowerStatusByDeviceIdAsync(deviceId);
            return Ok(new { success = true, result.PowerStatus, result.RoomId, result.RoomName });
        }

        [Authorize]
        [HttpGet("arduino/device-status")]
        public async Task<IActionResult> GetDeviceStatus()
        {
            var devices = await _roomService.GetDeviceActivityStatusAsync();
            return Ok(new { success = true, devices });
        }

        [Authorize]
        [HttpGet("{id}/validation-attempts")]
        public async Task<IActionResult> GetValidationAttempts(int id)
        {
            var attempts = await _roomService.GetValidationAttemptsAsync(id);
            return Ok(attempts);
        }
    }
}