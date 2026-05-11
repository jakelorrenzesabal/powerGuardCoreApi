using System;
using System.Collections.Generic;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi._Helpers;

namespace PowerGuardCoreApi.Models
{
    // ─── Requests ───────────────────────────────────────────────────────────────

    public class LogEventRequest
    {
        public string? CardUID { get; set; }
        public string? Event { get; set; }
        public string? Details { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceKey { get; set; }
        public int? AccountId { get; set; }
    }

    public class LogFilterRequest
    {
        public string? Event { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? RoomName { get; set; }
    }

    public class LogCountFilterRequest
    {
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? Event { get; set; }
        public string? Update { get; set; }
        public int? AccountId { get; set; }
        public int? RoomId { get; set; }
    }

    public class ValidationAttemptFilterRequest
    {
        public string? CardUID { get; set; }
        public string? DeviceId { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
    }

    public class UnblockUidRequest
    {
        public string Uid { get; set; } = null!;
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    public class ArduinoLogDto
    {
        public int CardLogId { get; set; }
        public int? AccountId { get; set; }
        public string UserEmail { get; set; } = "Unknown";
        public string UserName { get; set; } = "Unknown User";
        public string? CardUID { get; set; }
        public string Event { get; set; } = null!;
        public string? Details { get; set; }
        public string Timestamp { get; set; } = null!;
        public int? RoomId { get; set; }
        public string? RoomName { get; set; }
        public int? RoomNumber { get; set; }
        public string? Building { get; set; }
        public int? Floor { get; set; }
        public string? PowerStatus { get; set; }

        public static ArduinoLogDto FromLog(ArduinoLog log)
        {
            var philippineTime = DateTimeHelper.ConvertToPhilippineTime(log.Timestamp);
            var dto = new ArduinoLogDto
            {
                CardLogId = log.CardLogId,
                AccountId = log.AccountId,
                UserEmail = log.Account?.Email ?? "Unknown",
                UserName = log.Account != null ? $"{log.Account.FirstName} {log.Account.LastName}" : "Unknown User",
                CardUID = log.CardUID,
                Event = log.Event,
                Details = log.Details,
                Timestamp = philippineTime.ToString("MM/dd/yyyy hh:mm tt")
            };

            if (log.Room != null)
            {
                dto.RoomId = log.Room.RoomId;
                dto.RoomName = log.Room.RoomName;
                dto.RoomNumber = log.Room.RoomNumber;
                dto.Building = log.Room.Building;
                dto.Floor = log.Room.Floor;
                dto.PowerStatus = log.Room.PowerStatus;
            }

            return dto;
        }
    }

    public class BlockedUidDto
    {
        public int Id { get; set; }
        public string Uid { get; set; } = null!;
        public int? RoomId { get; set; }
        public string? RoomName { get; set; }
        public string? RoomNumber { get; set; }
        public DateTime BlockedAt { get; set; }
        public string? Reason { get; set; }
        public bool IsBlocked { get; set; }
        public string UserName { get; set; } = "Unknown/Unregistered Card";
    }

    public class ValidateUidResult
    {
        public bool Success { get; set; }
        public bool Authorized { get; set; }
        public int? AccountId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Message { get; set; } = null!;
        public bool RoomAccess { get; set; }
    }

    public class LogCountResult
    {
        public int Total { get; set; }
        public Dictionary<string, int> ByEventType { get; set; } = new();
        public List<ArduinoLogDto> Logs { get; set; } = new();
        public Dictionary<int, int> AttemptCounts { get; set; } = new();
    }
}
