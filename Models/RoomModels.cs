using System.Collections.Generic;
using PowerGuardCoreApi.Models;

namespace PowerGuardCoreApi.Models
{
    // ─── Requests ───────────────────────────────────────────────────────────────

    public class CreateRoomRequest
    {
        public string RoomName { get; set; } = null!;
        public int RoomNumber { get; set; }
        public int Floor { get; set; }
        public string? Building { get; set; }
        public string DeviceId { get; set; } = null!;
    }

    public class UpdateRoomRequest
    {
        public string? RoomName { get; set; }
        public int? RoomNumber { get; set; }
        public int? Floor { get; set; }
        public string? Building { get; set; }
        public string? DeviceId { get; set; }
    }

    public class UpdateRoomStatusRequest
    {
        public bool IsActive { get; set; }
    }

    public class TogglePowerStatusRequest
    {
        public string Status { get; set; } = null!;
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    public class RoomDto
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; } = null!;
        public int RoomNumber { get; set; }
        public int Floor { get; set; }
        public string? Building { get; set; }
        public string DeviceId { get; set; } = null!;
        public bool IsActive { get; set; }
        public string PowerStatus { get; set; } = "off";
        public System.DateTime? InactiveSince { get; set; }
        public System.DateTime? LastActiveAt { get; set; }
        public int? UserCount { get; set; }
        public string? LastAccountName { get; set; }
        public string? LastEvent { get; set; }
        public System.DateTime? LastEventTimestamp { get; set; }
        public string? CurrentAccountName { get; set; }
        public bool IsCardPresent { get; set; }

        public RoomDto() { }

        public RoomDto(Room room)
        {
            RoomId = room.RoomId;
            RoomName = room.RoomName;
            RoomNumber = room.RoomNumber;
            Floor = room.Floor;
            Building = room.Building;
            DeviceId = room.DeviceId;
            IsActive = room.IsActive;
            PowerStatus = room.PowerStatus;
            InactiveSince = room.InactiveSince;
            LastActiveAt = room.LastActiveAt;
        }
    }

    public class PowerStatusDto
    {
        public string PowerStatus { get; set; } = null!;
        public int RoomId { get; set; }
        public string RoomName { get; set; } = null!;
    }

    public class DeviceStatusDto
    {
        public int RoomId { get; set; }
        public string RoomName { get; set; } = null!;
        public string DeviceId { get; set; } = null!;
        public bool IsActive { get; set; }
        public bool IsOnline { get; set; }
        public string? LastSeen { get; set; }
    }

    public class ValidationAttemptDto
    {
        public int AttemptId { get; set; }
        public string CardUID { get; set; } = null!;
        public string DeviceId { get; set; } = null!;
        public int? RoomId { get; set; }
        public string? RoomName { get; set; }
        public int? AccountId { get; set; }
        public string? UserName { get; set; }
        public bool Authorized { get; set; }
        public string Message { get; set; } = null!;
        public System.DateTime Timestamp { get; set; }

        public ValidationAttemptDto() { }

        public ValidationAttemptDto(ValidationAttempt a)
        {
            AttemptId = a.AttemptId;
            CardUID = a.CardUID;
            DeviceId = a.DeviceId;
            RoomId = a.RoomId;
            RoomName = a.Room?.RoomName;
            AccountId = a.AccountId;
            UserName = a.Account != null ? $"{a.Account.FirstName} {a.Account.LastName}" : null;
            Authorized = a.Authorized;
            Message = a.Message;
            Timestamp = a.Timestamp;
        }
    }
}
