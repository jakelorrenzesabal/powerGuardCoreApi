using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi._Helpers;

namespace PowerGuardCoreApi.Services
{
    public interface IAccessRequestService
    {
        Task<AccessRequestDto> CreateRequestAsync(CreateAccessRequest request, int accountId);
        Task<IEnumerable<AccessRequestDto>> GetAllRequestsAsync(string? status);
        Task<IEnumerable<AccessRequestDto>> GetRequestsByAccountAsync(int accountId);
        Task<AccessRequestDto> ProcessRequestAsync(int requestId, ProcessAccessRequest request, int adminId, string ipAddress, string browserInfo);
    }

    public class AccessRequestService : IAccessRequestService
    {
        private readonly PowerGuardDbContext _db;
        private readonly IAccountService _accountService;

        public AccessRequestService(PowerGuardDbContext db, IAccountService accountService)
        {
            _db = db;
            _accountService = accountService;
        }

        public async Task<AccessRequestDto> CreateRequestAsync(CreateAccessRequest request, int accountId)
        {
            var room = await _db.Rooms.FindAsync(request.RoomId);
            if (room == null) throw new Exception("Room not found");

            var existingPending = await _db.RoomAccessRequests
                .AnyAsync(r => r.AccountId == accountId && r.RoomId == request.RoomId && r.Status == "Pending");
            if (existingPending) throw new AppException("You already have a pending request for this room");

            // The RequestedExpiryDate comes from the frontend datetime-local input, which is
            // in Philippine local time (UTC+8). We must convert it to UTC before storing.
            DateTime? expiryUtc = null;
            if (request.RequestType != "Permanent" && request.RequestedExpiryDate.HasValue)
            {
                expiryUtc = DateTimeHelper.ConvertToUtc(
                    DateTime.SpecifyKind(request.RequestedExpiryDate.Value, DateTimeKind.Unspecified)
                );
            }

            var accessRequest = new RoomAccessRequest
            {
                AccountId = accountId,
                RoomId = request.RoomId,
                RequestType = request.RequestType,
                RequestedExpiryDate = expiryUtc,
                Reason = request.Reason,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.RoomAccessRequests.Add(accessRequest);
            await _db.SaveChangesAsync();

            // Reload to include navigation properties
            var result = await _db.RoomAccessRequests
                .Include(r => r.Account)
                .Include(r => r.Room)
                .FirstOrDefaultAsync(r => r.RequestId == accessRequest.RequestId);

            return new AccessRequestDto(result!);
        }

        public async Task<IEnumerable<AccessRequestDto>> GetAllRequestsAsync(string? status)
        {
            var query = _db.RoomAccessRequests
                .Include(r => r.Account)
                .Include(r => r.Room)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(r => r.Status == status);

            var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            return requests.Select(r => new AccessRequestDto(r));
        }

        public async Task<IEnumerable<AccessRequestDto>> GetRequestsByAccountAsync(int accountId)
        {
            var requests = await _db.RoomAccessRequests
                .Include(r => r.Account)
                .Include(r => r.Room)
                .Where(r => r.AccountId == accountId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return requests.Select(r => new AccessRequestDto(r));
        }

        public async Task<AccessRequestDto> ProcessRequestAsync(int requestId, ProcessAccessRequest request, int adminId, string ipAddress, string browserInfo)
        {
            var accessRequest = await _db.RoomAccessRequests
                .Include(r => r.Account)
                .Include(r => r.Room)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (accessRequest == null) throw new Exception("Request not found");
            if (accessRequest.Status != "Pending") throw new Exception("Request has already been processed");

            accessRequest.Status = request.Status;
            accessRequest.AdminComment = request.AdminComment;
            accessRequest.UpdatedAt = DateTime.UtcNow;

            if (request.Status == "Approved")
            {
                // Logic to add the user to the room
                await _accountService.AddRoomToAccountAsync(
                    accessRequest.AccountId, 
                    accessRequest.RoomId, 
                    ipAddress, 
                    browserInfo, 
                    accessRequest.RequestedExpiryDate
                );
            }

            await _db.SaveChangesAsync();

            // Log activity
            var admin = await _db.Accounts.FindAsync(adminId);
            var adminName = admin != null ? $"{admin.FirstName} {admin.LastName}" : "Admin";
            
            _db.ActivityLogs.Add(new ActivityLog
            {
                AccountId = adminId,
                ActionType = "request_processed",
                ActionDetails = $"Admin {adminName} {request.Status.ToLower()} room access request for {accessRequest.Account?.Email} to Room {accessRequest.Room?.RoomName}. Details: {ipAddress}, {browserInfo}",
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return new AccessRequestDto(accessRequest);
        }
    }
}
