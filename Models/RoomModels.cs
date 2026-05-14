using System;
using System.Collections.Generic;
using System.Linq;
using PowerGuardCoreApi._Helpers;

namespace PowerGuardCoreApi.Models
{
    // ─── Request Models ───────────────────────────────────────────────────────────

    public class CreateRoomRequest
    {
        public string RoomName { get; set; } = null!;
        public int RoomNumber { get; set; }
        public int Floor { get; set; }
        public string? Building { get; set; }
        public string DeviceId { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class UpdateRoomRequest
    {
        public string? RoomName { get; set; }
        public int? RoomNumber { get; set; }
        public int? Floor { get; set; }
        public string? Building { get; set; }
        public string? DeviceId { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
    }

    public class UpdateRoomStatusRequest
    {
        public bool IsActive { get; set; }
    }

    public class TogglePowerStatusRequest
    {
        public string Status { get; set; } = null!;
    }

    public class CreateAccessRequest
    {
        public int RoomId { get; set; }
        public string RequestType { get; set; } = "TimeLimited"; // "TimeLimited" or "Permanent"
        public DateTime? RequestedExpiryDate { get; set; }
        public string? Reason { get; set; }
    }

    public class ProcessAccessRequest
    {
        public string Status { get; set; } = "Approved"; // "Approved" or "Rejected"
        public string? AdminComment { get; set; }
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    public class RoomDto
    {
        public int? RoomId { get; set; }
        public string RoomName { get; set; } = null!;
        public int RoomNumber { get; set; }
        public int Floor { get; set; }
        public string? Building { get; set; }
        public string? DeviceId { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public string PowerStatus { get; set; } = null!;
        public string? LastAccountName { get; set; }
        public string? LastEvent { get; set; }
        public DateTime? LastEventTimestamp { get; set; }
        public string? CurrentAccountName { get; set; }
        public bool IsCardPresent { get; set; }
        public bool IsAuthorized { get; set; }
        public int? UserCount { get; set; }
        public DateTime? LastActiveAt { get; set; }
        public DateTime? InactiveSince { get; set; }

        public RoomDto() { }

        public RoomDto(Room r)
        {
            RoomId = r.RoomId;
            RoomName = r.RoomName;
            RoomNumber = r.RoomNumber;
            Floor = r.Floor;
            Building = r.Building;
            DeviceId = r.DeviceId;
            IsActive = r.IsActive;
            PowerStatus = r.PowerStatus;
            LastActiveAt = r.LastActiveAt.HasValue ? DateTimeHelper.ConvertToPhilippineTime(r.LastActiveAt.Value) : null;
            InactiveSince = r.InactiveSince.HasValue ? DateTimeHelper.ConvertToPhilippineTime(r.InactiveSince.Value) : null;
            UserCount = r.AccountRooms?.Count;
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
        public string? DeviceId { get; set; }
        public bool IsActive { get; set; }
        public string? LastSeen { get; set; }
    }

    public class ValidationAttemptDto
    {
        public int AttemptId { get; set; }
        public string CardUID { get; set; } = null!;
        public string DeviceId { get; set; } = null!;
        public bool Authorized { get; set; }
        public string Message { get; set; } = null!;
        public int? AccountId { get; set; }
        public string? UserName { get; set; }
        public int? RoomId { get; set; }
        public string? RoomName { get; set; }
        public DateTime Timestamp { get; set; }

        public ValidationAttemptDto() { }

        public ValidationAttemptDto(ValidationAttempt a)
        {
            AttemptId = a.AttemptId;
            CardUID = a.CardUID;
            DeviceId = a.DeviceId;
            Authorized = a.Authorized;
            Message = a.Message;
            AccountId = a.AccountId;
            UserName = a.Account != null ? $"{a.Account.FirstName} {a.Account.LastName}" : null;
            RoomId = a.RoomId;
            RoomName = a.Room?.RoomName;
            Timestamp = DateTimeHelper.ConvertToPhilippineTime(a.Timestamp);
        }
    }

    public class AccessRequestDto
    {
        public int RequestId { get; set; }
        public int AccountId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public int RoomId { get; set; }
        public string? RoomName { get; set; }
        public string RequestType { get; set; } = null!;
        public DateTime? RequestedExpiryDate { get; set; }
        public string Status { get; set; } = null!;
        public string? Reason { get; set; }
        public string? AdminComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public AccessRequestDto() { }

        public AccessRequestDto(RoomAccessRequest r)
        {
            RequestId = r.RequestId;
            AccountId = r.AccountId;
            UserName = r.Account != null ? $"{r.Account.FirstName} {r.Account.LastName}" : null;
            UserEmail = r.Account?.Email;
            RoomId = r.RoomId;
            RoomName = r.Room?.RoomName;
            RequestType = r.RequestType;
            RequestedExpiryDate = r.RequestedExpiryDate.HasValue ? DateTimeHelper.ConvertToPhilippineTime(r.RequestedExpiryDate.Value) : null;
            Status = r.Status;
            Reason = r.Reason;
            AdminComment = r.AdminComment;
            CreatedAt = DateTimeHelper.ConvertToPhilippineTime(r.CreatedAt);
            UpdatedAt = DateTimeHelper.ConvertToPhilippineTime(r.UpdatedAt);
        }
    }
}
