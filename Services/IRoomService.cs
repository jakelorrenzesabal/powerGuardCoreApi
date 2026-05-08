using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi._Helpers;

namespace PowerGuardCoreApi.Services
{
    public interface IRoomService
    {
        Task<(Room room, string deviceKey)> CreateRoomAsync(CreateRoomRequest request, int accountId);
        Task<(IEnumerable<RoomDto> rooms, int count)> GetAllRoomsAsync(int? accountId);
        Task<Room?> GetRoomByIdAsync(int id);
        Task<Room> UpdateRoomAsync(int id, UpdateRoomRequest request, int accountId);
        Task<Room> UpdateRoomStatusAsync(int id, bool isActive, int accountId);
        Task<Room> TogglePowerStatusAsync(int id, string status, int accountId);
        Task<PowerStatusDto> GetPowerStatusByDeviceIdAsync(string deviceId);
        Task<IEnumerable<DeviceStatusDto>> GetDeviceActivityStatusAsync();
        Task<object> GetRoomsByPowerStatusAsync(string status, int? accountId);
        Task<IEnumerable<ValidationAttemptDto>> GetValidationAttemptsAsync(int roomId);
    }

    public class RoomService : IRoomService
    {
        private readonly PowerGuardDbContext _db;
        private readonly IDeviceKeyHelper _deviceKeyHelper;

        public RoomService(PowerGuardDbContext db, IDeviceKeyHelper deviceKeyHelper)
        {
            _db = db;
            _deviceKeyHelper = deviceKeyHelper;
        }

        public async Task<(Room room, string deviceKey)> CreateRoomAsync(CreateRoomRequest request, int accountId)
        {
            if (await _db.Rooms.AnyAsync(r => r.DeviceId == request.DeviceId))
                throw new Exception($"Device ID \"{request.DeviceId}\" is already registered");
            if (await _db.Rooms.AnyAsync(r => r.RoomName == request.RoomName))
                throw new Exception($"Room Name \"{request.RoomName}\" is already registered");
            if (request.RoomNumber <= 0)
                throw new Exception("Room Number must be a positive integer");
            if (await _db.Rooms.AnyAsync(r => r.RoomNumber == request.RoomNumber))
                throw new Exception($"Room Number \"{request.RoomNumber}\" is already registered");

            var plain = _deviceKeyHelper.GenerateDeviceKey();
            var room = new Room
            {
                RoomName = request.RoomName,
                RoomNumber = request.RoomNumber,
                Floor = request.Floor,
                Building = request.Building,
                DeviceId = request.DeviceId,
                IsActive = true,
                PowerStatus = "off",
                DeviceKeyEncrypted = _deviceKeyHelper.EncryptDeviceKey(plain),
                DeviceKeyHash = _deviceKeyHelper.BcryptHash(plain),
                DeviceKeyCreatedAt = DateTime.UtcNow
            };
            _db.Rooms.Add(room);
            await _db.SaveChangesAsync();

            // Auto-authorize creator
            if (accountId > 0)
            {
                var creator = await _db.Accounts.Include(a => a.Rooms).FirstOrDefaultAsync(a => a.AccountId == accountId);
                if (creator != null) { creator.Rooms.Add(room); await _db.SaveChangesAsync(); }
            }

            return (room, plain);
        }

        public async Task<(IEnumerable<RoomDto> rooms, int count)> GetAllRoomsAsync(int? accountId)
        {
            var query = _db.Rooms.AsQueryable();
            if (accountId.HasValue)
                query = query.Where(r => r.Accounts.Any(a => a.AccountId == accountId.Value));

            var rooms = await query.OrderBy(r => r.RoomName).ToListAsync();
            var count = await _db.Rooms.CountAsync();

            var dtos = new List<RoomDto>();
            bool changed = false;

            foreach (var room in rooms)
            {
                if (CheckHeartbeat(room)) changed = true;

                var dto = new RoomDto(room);

                var lastLog = await _db.ArduinoLogs
                    .Include(l => l.Account)
                    .Where(l => l.RoomId == room.RoomId)
                    .OrderByDescending(l => l.Timestamp)
                    .FirstOrDefaultAsync();

                if (lastLog != null)
                {
                    dto.LastAccountName = lastLog.Account != null
                        ? $"{lastLog.Account.FirstName} {lastLog.Account.LastName}"
                        : lastLog.CardUID == "SYSTEM" ? "SYSTEM" : "Unknown";
                    dto.LastEvent = lastLog.Event;
                    dto.LastEventTimestamp = lastLog.Timestamp;
                }

                if (room.PowerStatus == "on")
                {
                    var onEvents = new[] { "card_on", "power_on", "CARD_DETECTED", "room_activated" };
                    var curLog = await _db.ArduinoLogs
                        .Include(l => l.Account)
                        .Where(l => l.RoomId == room.RoomId && onEvents.Contains(l.Event))
                        .OrderByDescending(l => l.Timestamp)
                        .FirstOrDefaultAsync();

                    if (curLog != null)
                    {
                        dto.CurrentAccountName = curLog.Account != null
                            ? $"{curLog.Account.FirstName} {curLog.Account.LastName}"
                            : curLog.CardUID == "SYSTEM" ? "SYSTEM" : "Unknown";
                        dto.IsCardPresent = new[] { "card_on", "CARD_DETECTED" }.Contains(curLog.Event);
                    }
                }

                dto.UserCount = await _db.Accounts.CountAsync(a => a.Rooms.Any(r => r.RoomId == room.RoomId));
                dtos.Add(dto);
            }

            if (changed) await _db.SaveChangesAsync();

            return (dtos, count);
        }

        public async Task<Room?> GetRoomByIdAsync(int id) => await _db.Rooms.FindAsync(id);

        public async Task<Room> UpdateRoomAsync(int id, UpdateRoomRequest request, int accountId)
        {
            var room = await GetRoomByIdAsync(id) ?? throw new Exception("Room not found");

            if (request.RoomNumber.HasValue && request.RoomNumber <= 0)
                throw new Exception("Room Number must be a positive integer");
            if (request.RoomNumber.HasValue && request.RoomNumber != room.RoomNumber &&
                await _db.Rooms.AnyAsync(r => r.RoomNumber == request.RoomNumber))
                throw new Exception($"Room Number \"{request.RoomNumber}\" is already registered");
            if (!string.IsNullOrEmpty(request.RoomName) && request.RoomName != room.RoomName &&
                await _db.Rooms.AnyAsync(r => r.RoomName == request.RoomName))
                throw new Exception($"Room Name \"{request.RoomName}\" is already registered");
            if (!string.IsNullOrEmpty(request.DeviceId) && request.DeviceId != room.DeviceId &&
                await _db.Rooms.AnyAsync(r => r.DeviceId == request.DeviceId))
                throw new Exception($"Device ID \"{request.DeviceId}\" is already registered");

            if (request.RoomName != null) room.RoomName = request.RoomName;
            if (request.RoomNumber.HasValue) room.RoomNumber = request.RoomNumber.Value;
            if (request.Floor.HasValue) room.Floor = request.Floor.Value;
            if (request.Building != null) room.Building = request.Building;
            if (request.DeviceId != null) room.DeviceId = request.DeviceId;
            room.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return room;
        }

        public async Task<Room> UpdateRoomStatusAsync(int id, bool isActive, int accountId)
        {
            var room = await GetRoomByIdAsync(id) ?? throw new Exception("Room not found");

            if (!isActive && room.IsActive)
            {
                room.IsActive = false;
                room.InactiveSince = DateTime.UtcNow;
                if (room.PowerStatus == "on") room.PowerStatus = "off";
            }
            else if (isActive && !room.IsActive)
            {
                room.IsActive = true;
                room.LastActiveAt = DateTime.UtcNow;
                room.InactiveSince = null;
            }

            room.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return room;
        }

        public async Task<Room> TogglePowerStatusAsync(int id, string status, int accountId)
        {
            if (status != "on" && status != "off")
                throw new Exception("Invalid status. Use \"on\" or \"off\".");

            var room = await GetRoomByIdAsync(id) ?? throw new Exception("Room not found");

            if (!room.IsActive && status == "on")
                throw new RoomInactiveException($"Cannot turn ON power for inactive room \"{room.RoomName}\". Please activate the room first.");

            if (room.PowerStatus == status) return room;

            room.PowerStatus = status;
            room.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return room;
        }

        public async Task<PowerStatusDto> GetPowerStatusByDeviceIdAsync(string deviceId)
        {
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.DeviceId == deviceId)
                ?? throw new Exception("Room not found");

            // Always update LastActiveAt when device checks in
            room.LastActiveAt = DateTime.UtcNow;
            room.UpdatedAt = DateTime.UtcNow;

            if (!room.IsActive)
            {
                room.IsActive = true;
                room.InactiveSince = null;

                _db.ArduinoLogs.Add(new ArduinoLog
                {
                    RoomId = room.RoomId,
                    Event = "room_activated",
                    CardUID = "SYSTEM",
                    Details = "Device detected. Room reactivated automatically.",
                    Timestamp = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync();

            return new PowerStatusDto { PowerStatus = room.PowerStatus, RoomId = room.RoomId, RoomName = room.RoomName };
        }

        public async Task<IEnumerable<DeviceStatusDto>> GetDeviceActivityStatusAsync()
        {
            var rooms = await _db.Rooms.Where(r => r.DeviceId != null).ToListAsync();
            bool changed = false;
            var result = rooms.Select(r =>
            {
                if (CheckHeartbeat(r)) changed = true;
                return new DeviceStatusDto
                {
                    RoomId = r.RoomId,
                    RoomName = r.RoomName,
                    DeviceId = r.DeviceId,
                    IsActive = r.IsActive,
                    LastSeen = r.LastActiveAt?.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }).ToList();

            if (changed) await _db.SaveChangesAsync();
            return result;
        }

        private bool CheckHeartbeat(Room room)
        {
            // Auto-deactivation logic: If Active but not seen for 5 seconds (or never seen), set IsActive = false
            bool isDeviceFound = room.LastActiveAt.HasValue && room.LastActiveAt.Value > DateTime.UtcNow.AddSeconds(-5);

            if (room.IsActive && !isDeviceFound)
            {
                room.IsActive = false;
                room.InactiveSince = DateTime.UtcNow;
                room.PowerStatus = "off"; // Turn off power if device is lost or never found

                _db.ArduinoLogs.Add(new ArduinoLog
                {
                    RoomId = room.RoomId,
                    Event = "power_off",
                    CardUID = "SYSTEM",
                    Details = room.LastActiveAt.HasValue
                        ? "Device heartbeat lost. Room deactivated automatically."
                        : "No device detected. Room deactivated automatically.",
                    Timestamp = DateTime.UtcNow
                });

                return true;
            }
            return false;
        }

        public async Task<object> GetRoomsByPowerStatusAsync(string status, int? accountId)
        {
            var q = _db.Rooms.AsQueryable();
            if (accountId.HasValue) q = q.Where(r => r.Accounts.Any(a => a.AccountId == accountId.Value));
            if (status == "on" || status == "off") q = q.Where(r => r.PowerStatus == status);
            var rooms = await q.OrderBy(r => r.RoomName).ToListAsync();
            return new { count = rooms.Count, rooms };
        }

        public async Task<IEnumerable<ValidationAttemptDto>> GetValidationAttemptsAsync(int roomId)
        {
            var attempts = await _db.ValidationAttempts
                .Include(a => a.Account).Include(a => a.Room)
                .Where(a => a.RoomId == roomId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
            return attempts.Select(a => new ValidationAttemptDto(a));
        }
    }
}