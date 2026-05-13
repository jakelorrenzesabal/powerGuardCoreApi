using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi._Helpers;

namespace PowerGuardCoreApi.Services
{
    public interface IArduinoLogService
    {
        Task<object> ProcessEventAsync(LogEventRequest eventData);
        Task<object> LogEventAsync(LogEventRequest logData);
        Task<IEnumerable<ArduinoLogDto>> GetAllLogsAsync(LogFilterRequest filters);
        Task<IEnumerable<ArduinoLogDto>> GetAccountLogsAsync(int accountId, LogFilterRequest filters);
        Task<IEnumerable<ArduinoLogDto>> GetRoomLogsAsync(int roomId, LogFilterRequest filters);
        Task<ValidateUidResult> ValidateUIDAsync(string uid, string deviceId, string deviceKey);
        Task<LogCountResult> GetLogCountsAsync(LogCountFilterRequest filters);
        Task TurnOffPowerIfDeviceNotDetectedAsync();
        Task<IEnumerable<ValidationAttemptDto>> GetAllValidationAttemptsAsync(ValidationAttemptFilterRequest filters);
        Task<IEnumerable<BlockedUidDto>> GetAllBlockedUidsAsync();
        Task<object> UnblockUidAsync(string uid);
    }

    public class ArduinoLogService : IArduinoLogService
    {
        private readonly PowerGuardDbContext _db;
        private readonly IDeviceKeyHelper _deviceKeyHelper;

        public ArduinoLogService(PowerGuardDbContext db, IDeviceKeyHelper deviceKeyHelper)
        {
            _db = db;
            _deviceKeyHelper = deviceKeyHelper;
        }

        public async Task<object> ProcessEventAsync(LogEventRequest eventData)
        {
            if (eventData.Event == "validate_card")
            {
                var result = await ValidateUIDAsync(eventData.CardUID ?? "", eventData.DeviceId ?? "", eventData.DeviceKey ?? "");
                if (result.Authorized)
                {
                    var log = await LogEventAsync(new LogEventRequest
                    {
                        CardUID = eventData.CardUID,
                        Event = "card_on",
                        Details = $"Validation success for {result.FirstName} {result.LastName}",
                        DeviceId = eventData.DeviceId,
                        AccountId = result.AccountId
                    });
                    return new { success = result.Success, authorized = result.Authorized, firstName = result.FirstName, lastName = result.LastName, accountId = result.AccountId };
                }
                return new { success = result.Success, authorized = result.Authorized, message = result.Message };
            }

            if (eventData.Event == "card_off" || eventData.Event == "CARD_REMOVED")
            {
                var last = await _db.ValidationAttempts
                    .Where(x => x.CardUID == eventData.CardUID && x.DeviceId == eventData.DeviceId)
                    .OrderByDescending(x => x.Timestamp).FirstOrDefaultAsync();

                if (last != null && last.Authorized)
                {
                    var log = await LogEventAsync(new LogEventRequest
                    {
                        CardUID = eventData.CardUID,
                        Event = "card_off",
                        Details = $"Card removed for UID: {eventData.CardUID}",
                        DeviceId = eventData.DeviceId,
                        AccountId = last.AccountId
                    });
                    return new { success = true };
                }
                return new { success = true, message = "Unauthorized card removed" };
            }

            await LogEventAsync(eventData);
            return new { success = true };
        }

        public async Task<object> LogEventAsync(LogEventRequest logData)
        {
            Account? account = null;
            if (logData.AccountId.HasValue)
                account = await _db.Accounts.Include(a => a.AccountRooms).FirstOrDefaultAsync(a => a.AccountId == logData.AccountId.Value);
            else if (!string.IsNullOrEmpty(logData.CardUID))
                account = await _db.Accounts.Include(a => a.AccountRooms).FirstOrDefaultAsync(a => a.Uid == logData.CardUID);

            int? roomId = null;
            if (!string.IsNullOrEmpty(logData.DeviceId))
            {
                var room = await _db.Rooms.FirstOrDefaultAsync(r => r.DeviceId == logData.DeviceId);
                if (room != null)
                {
                    roomId = room.RoomId;
                    var onEvents = new[] { "card_on", "CARD_DETECTED" };
                    var offEvents = new[] { "card_off", "CARD_REMOVED", "unauthorized_off" };
                    var allPower = new[] { "card_on", "CARD_DETECTED", "card_off", "CARD_REMOVED", "power_on", "power_off", "unauthorized_off" };

                    if (room.IsActive)
                    {
                        if (onEvents.Contains(logData.Event)) { room.PowerStatus = "on"; await _db.SaveChangesAsync(); }
                        else if (offEvents.Contains(logData.Event)) { room.PowerStatus = "off"; await _db.SaveChangesAsync(); }
                    }
                    else if (allPower.Contains(logData.Event) && room.PowerStatus != "off")
                    {
                        room.PowerStatus = "off"; await _db.SaveChangesAsync();
                    }
                }
            }

            var arduinoLog = new ArduinoLog
            {
                CardUID = logData.CardUID ?? "UNKNOWN",
                Event = logData.Event ?? "unknown",
                Details = Sanitize(logData.Details ?? (account != null
                    ? $"User {account.FirstName} {account.LastName} {logData.Event}"
                    : $"{logData.Event} (no account linked)")),
                Timestamp = DateTime.UtcNow,
                AccountId = account?.AccountId,
                RoomId = roomId
            };
            _db.ArduinoLogs.Add(arduinoLog);
            await _db.SaveChangesAsync();
            return arduinoLog;
        }

        public async Task<IEnumerable<ArduinoLogDto>> GetAllLogsAsync(LogFilterRequest filters)
        {
            var q = _db.ArduinoLogs.Include(l => l.Account).Include(l => l.Room).AsQueryable();
            if (!string.IsNullOrEmpty(filters.Event)) q = q.Where(l => l.Event.Contains(filters.Event));
            if (DateTime.TryParse(filters.StartDate, out var sd)) q = q.Where(l => l.Timestamp >= sd);
            if (DateTime.TryParse(filters.EndDate, out var ed)) q = q.Where(l => l.Timestamp <= ed);
            if (!string.IsNullOrEmpty(filters.RoomName)) q = q.Where(l => l.Room != null && l.Room.RoomName.Contains(filters.RoomName));
            return (await q.OrderByDescending(l => l.Timestamp).ToListAsync()).Select(ArduinoLogDto.FromLog);
        }

        public async Task<IEnumerable<ArduinoLogDto>> GetAccountLogsAsync(int accountId, LogFilterRequest filters)
        {
            var q = _db.ArduinoLogs.Include(l => l.Account).Include(l => l.Room).Where(l => l.AccountId == accountId).AsQueryable();
            if (!string.IsNullOrEmpty(filters.Event)) q = q.Where(l => l.Event.Contains(filters.Event));
            if (DateTime.TryParse(filters.StartDate, out var sd)) q = q.Where(l => l.Timestamp >= sd);
            if (DateTime.TryParse(filters.EndDate, out var ed)) q = q.Where(l => l.Timestamp <= ed);
            return (await q.OrderByDescending(l => l.Timestamp).ToListAsync()).Select(ArduinoLogDto.FromLog);
        }

        public async Task<IEnumerable<ArduinoLogDto>> GetRoomLogsAsync(int roomId, LogFilterRequest filters)
        {
            var q = _db.ArduinoLogs.Include(l => l.Account).Include(l => l.Room).Where(l => l.RoomId == roomId).AsQueryable();
            if (!string.IsNullOrEmpty(filters.Event)) q = q.Where(l => l.Event.Contains(filters.Event));
            if (DateTime.TryParse(filters.StartDate, out var sd)) q = q.Where(l => l.Timestamp >= sd);
            if (DateTime.TryParse(filters.EndDate, out var ed)) q = q.Where(l => l.Timestamp <= ed);
            return (await q.OrderByDescending(l => l.Timestamp).ToListAsync()).Select(ArduinoLogDto.FromLog);
        }

        public async Task<ValidateUidResult> ValidateUIDAsync(string uid, string deviceId, string deviceKey)
        {
            if (!string.IsNullOrEmpty(uid))
            {
                var blocked = await _db.BlockedUids.FirstOrDefaultAsync(b => b.Uid == uid && b.IsBlocked);
                if (blocked != null)
                    return new ValidateUidResult { Success = false, Authorized = false, Message = "Card is blocked due to excessive failed attempts." };
            }

            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(deviceId))
            {
                await AddAttemptAsync(uid ?? "UNKNOWN", deviceId ?? "UNKNOWN", null, null, false, "Missing UID or device ID");
                return new ValidateUidResult { Success = true, Authorized = false, Message = "Missing UID or device ID" };
            }

            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.DeviceId == deviceId);
            if (room == null)
            {
                await AddAttemptAsync(uid, deviceId, null, null, false, "Device not registered");
                return new ValidateUidResult { Success = true, Authorized = false, Message = "Device not registered" };
            }

            if (string.IsNullOrEmpty(deviceKey) || string.IsNullOrEmpty(room.DeviceKeyHash))
            {
                await AddAttemptAsync(uid, deviceId, room.RoomId, null, false, "Missing device key");
                return new ValidateUidResult { Success = true, Authorized = false, Message = "Missing device key" };
            }

            if (!_deviceKeyHelper.BcryptCompare(deviceKey, room.DeviceKeyHash))
            {
                await AddAttemptAsync(uid, deviceId, room.RoomId, null, false, "Device key invalid");
                return new ValidateUidResult { Success = true, Authorized = false, Message = "Device key invalid" };
            }

            var account = await _db.Accounts.Include(a => a.AccountRooms).FirstOrDefaultAsync(a => a.Uid == uid);
            if (account == null)
            {
                await AddAttemptAsync(uid, deviceId, room.RoomId, null, false, "Card not registered");
                await CheckAndBlockUidAsync(uid, room.RoomId);
                return new ValidateUidResult { Success = true, Authorized = false, Message = "Card not registered" };
            }

            var assignment = account.AccountRooms.FirstOrDefault(ar => ar.RoomId == room.RoomId);
            var hasAccess = assignment != null && (assignment.ExpiryDate == null || assignment.ExpiryDate > DateTime.UtcNow);
            await AddAttemptAsync(uid, deviceId, room.RoomId, account.AccountId, hasAccess,
                hasAccess ? "Access granted" : "Access denied - Not authorized for this room");

            if (!hasAccess) await CheckAndBlockUidAsync(uid, room.RoomId);

            return new ValidateUidResult
            {
                Success = true,
                Authorized = hasAccess,
                AccountId = account.AccountId,
                FirstName = account.FirstName,
                LastName = account.LastName,
                Message = hasAccess ? "Access granted" : "Access denied - Not authorized for this room",
                RoomAccess = hasAccess
            };
        }

        public async Task<LogCountResult> GetLogCountsAsync(LogCountFilterRequest filters)
        {
            var q = _db.ArduinoLogs.AsQueryable();
            if (filters.AccountId.HasValue) q = q.Where(l => l.AccountId == filters.AccountId.Value);
            if (filters.RoomId.HasValue) q = q.Where(l => l.RoomId == filters.RoomId.Value);

            if (filters.Update == "true")
            {
                q = q.Where(l => l.Event.Contains("update") || l.Event.Contains("activated") || l.Event.Contains("deactivated"));
            }
            else
            {
                var evList = new List<string>();
                if (filters.Type == "card") evList.AddRange(new[] { "card_on", "card_off", "CARD_DETECTED", "CARD_REMOVED" });
                else if (filters.Type == "manual") evList.AddRange(new[] { "power_on", "power_off" });

                if (filters.Status == "on") { var on = new[] { "card_on", "power_on", "CARD_DETECTED" }; evList = evList.Count > 0 ? evList.Intersect(on).ToList() : on.ToList(); }
                else if (filters.Status == "off") { var off = new[] { "card_off", "power_off", "CARD_REMOVED" }; evList = evList.Count > 0 ? evList.Intersect(off).ToList() : off.ToList(); }

                if (!string.IsNullOrEmpty(filters.Event)) evList = new List<string> { filters.Event };
                if (evList.Count > 0) q = q.Where(l => evList.Contains(l.Event));
            }

            var total = await q.CountAsync();
            var byEvent = await q.GroupBy(l => l.Event).Select(g => new { Event = g.Key, Count = g.Count() }).ToListAsync();
            var logs = await q.Include(l => l.Account).Include(l => l.Room).OrderByDescending(l => l.Timestamp).ToListAsync();
            var attempts = await _db.ValidationAttempts.Where(a => a.RoomId.HasValue)
                .GroupBy(a => a.RoomId!.Value).Select(g => new { RoomId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.RoomId, x => x.Count);

            return new LogCountResult
            {
                Total = total,
                ByEventType = byEvent.ToDictionary(x => x.Event, x => x.Count),
                Logs = logs.Select(ArduinoLogDto.FromLog).ToList(),
                AttemptCounts = attempts
            };
        }

        public async Task TurnOffPowerIfDeviceNotDetectedAsync()
        {
            var rooms = await _db.Rooms.Where(r => !r.IsActive && r.PowerStatus != "off").ToListAsync();
            foreach (var room in rooms)
            {
                room.PowerStatus = "off";
                _db.ArduinoLogs.Add(new ArduinoLog { CardUID = "SYSTEM", Event = "power_off", Details = "Device not detected. Power turned off automatically.", Timestamp = DateTime.UtcNow, RoomId = room.RoomId });
                await _db.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ValidationAttemptDto>> GetAllValidationAttemptsAsync(ValidationAttemptFilterRequest filters)
        {
            var q = _db.ValidationAttempts.Include(a => a.Account).Include(a => a.Room).AsQueryable();
            if (!string.IsNullOrEmpty(filters.CardUID)) q = q.Where(a => a.CardUID == filters.CardUID);
            if (!string.IsNullOrEmpty(filters.DeviceId)) q = q.Where(a => a.DeviceId == filters.DeviceId);
            if (DateTime.TryParse(filters.StartDate, out var sd)) q = q.Where(a => a.Timestamp >= sd);
            if (DateTime.TryParse(filters.EndDate, out var ed)) q = q.Where(a => a.Timestamp <= ed);
            return (await q.OrderByDescending(a => a.Timestamp).ToListAsync()).Select(a => new ValidationAttemptDto(a));
        }

        public async Task<IEnumerable<BlockedUidDto>> GetAllBlockedUidsAsync()
        {
            var items = await _db.BlockedUids.Include(b => b.Room).Where(b => b.IsBlocked).ToListAsync();
            var result = new List<BlockedUidDto>();
            foreach (var item in items)
            {
                var acct = await _db.Accounts.FirstOrDefaultAsync(a => a.Uid == item.Uid);
                result.Add(new BlockedUidDto
                {
                    Id = item.Id,
                    Uid = item.Uid,
                    RoomId = item.RoomId,
                    RoomName = item.Room?.RoomName,
                    RoomNumber = item.Room?.RoomNumber.ToString(),
                    BlockedAt = item.BlockedAt,
                    Reason = item.Reason,
                    IsBlocked = item.IsBlocked,
                    UserName = acct != null ? $"{acct.FirstName} {acct.LastName}" : "Unknown/Unregistered Card"
                });
            }
            return result;
        }

        public async Task<object> UnblockUidAsync(string uid)
        {
            var blocked = await _db.BlockedUids.FirstOrDefaultAsync(b => b.Uid == uid);
            if (blocked == null) return new { success = false, message = "UID not found in blocklist." };
            blocked.IsBlocked = false;
            var failed = await _db.ValidationAttempts.Where(a => a.CardUID == uid && !a.Authorized).ToListAsync();
            _db.ValidationAttempts.RemoveRange(failed);
            await _db.SaveChangesAsync();
            return new { success = true, message = $"UID {uid} unblocked and attempt counter reset." };
        }

        private async Task AddAttemptAsync(string uid, string deviceId, int? roomId, int? accountId, bool authorized, string message)
        {
            _db.ValidationAttempts.Add(new ValidationAttempt { CardUID = uid, DeviceId = deviceId, RoomId = roomId, AccountId = accountId, Authorized = authorized, Message = message, Timestamp = DateTime.UtcNow });
            await _db.SaveChangesAsync();
        }

        private async Task CheckAndBlockUidAsync(string uid, int roomId)
        {
            var recent = await _db.ValidationAttempts.Where(a => a.CardUID == uid).OrderByDescending(a => a.Timestamp).Take(5).ToListAsync();
            if (recent.Count < 5 || recent.Any(a => a.Authorized)) return;
            var existing = await _db.BlockedUids.FirstOrDefaultAsync(b => b.Uid == uid);
            if (existing != null) { existing.IsBlocked = true; existing.Reason = "5 consecutive failed validation attempts"; existing.BlockedAt = DateTime.UtcNow; existing.RoomId = roomId; }
            else _db.BlockedUids.Add(new BlockedUid { Uid = uid, Reason = "5 consecutive failed validation attempts", IsBlocked = true, RoomId = roomId, BlockedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync();
        }

        private static string Sanitize(string? text) =>
            string.IsNullOrEmpty(text) ? text ?? "" : System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", string.Empty);
    }
}