using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi._Helpers;

namespace PowerGuardCoreApi.Services
{
    public interface IAccountService
    {
        Task<AuthenticateResponse> AuthenticateAsync(AuthenticateRequest request, string ipAddress, string browserInfo);
        Task<RefreshTokenResponse> RefreshTokenAsync(string token, string ipAddress);
        Task RevokeTokenAsync(string token, string ipAddress, string browserInfo);
        Task RegisterAsync(RegisterRequest request, string origin);
        Task VerifyEmailAsync(VerifyEmailRequest request);
        Task ForgotPasswordAsync(ForgotPasswordRequest request, string origin);
        Task ValidateResetTokenAsync(ValidateResetTokenRequest request);
        Task ResetPasswordAsync(ResetPasswordRequest request, string ipAddress, string browserInfo);
        Task<IEnumerable<AccountDto>> GetAllAsync(string search, bool? isActive);
        Task<AccountDto> GetByIdAsync(int accountId);
        Task<AccountDto> CreateAsync(CreateAccountRequest request, int createdByAccountId, string ipAddress, string browserInfo);
        Task<AccountDto> UpdateAsync(int accountId, UpdateAccountRequest request, string ipAddress, string browserInfo, string requesterRole, int requesterId);
        Task DeleteAsync(int accountId, string ipAddress, string browserInfo, string requesterRole, int requesterId);
        Task<object> GetPreferencesAsync(int accountId);
        Task UpdatePreferencesAsync(int accountId, UpdatePreferencesRequest request);
        Task<IEnumerable<AccountDto>> GetAccountsByRoomAsync(int roomId);
        Task<IEnumerable<AccountDto>> GetUnassignedAccountsAsync(int roomId, string? search);
        Task AddRoomToAccountAsync(int accountId, int roomId, string ipAddress, string browserInfo);
        Task RemoveRoomFromAccountAsync(int accountId, int roomId, string ipAddress, string browserInfo);
        Task<int> GetCountAsync();
        Task<object> GetAccountActivitiesAsync(int accountId, ActivityFilterRequest filters);
        Task<object> GetAllActivityLogsAsync(ActivityLogFilterRequest filters);
    }

    public class AccountService : IAccountService
    {
        private readonly PowerGuardDbContext _db;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public AccountService(PowerGuardDbContext db, IConfiguration config, IEmailSender emailSender)
        {
            _db = db;
            _config = config;
            _emailSender = emailSender;
        }

        // ─── Authenticate ────────────────────────────────────────────────────────

        public async Task<AuthenticateResponse> AuthenticateAsync(AuthenticateRequest request, string ipAddress, string browserInfo)
        {
            var account = await _db.Accounts
                .FirstOrDefaultAsync(x => x.Email == request.Email);

            if (account == null || !account.IsVerified || !BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
                throw new AppException("Email or password is incorrect");

            if (!account.IsActive)
                throw new AppException("Account is deactivated");

            var jwtToken = GenerateJwtToken(account);
            var refreshToken = GenerateRefreshToken(account, ipAddress);

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            await LogActivityAsync(account.AccountId, "login", ipAddress, browserInfo);

            return new AuthenticateResponse
            {
                Account = MapToDto(account),
                JwtToken = jwtToken,
                RefreshToken = refreshToken.Token
            };
        }

        // ─── Refresh Token ───────────────────────────────────────────────────────

        public async Task<RefreshTokenResponse> RefreshTokenAsync(string token, string ipAddress)
        {
            var refreshToken = await _db.RefreshTokens
                .Include(rt => rt.Account)
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null || !refreshToken.IsActive)
                throw new AppException("Invalid token");

            var account = refreshToken.Account!;

            var newRefreshToken = GenerateRefreshToken(account, ipAddress);
            refreshToken.Revoked = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = newRefreshToken.Token;

            _db.RefreshTokens.Add(newRefreshToken);
            await _db.SaveChangesAsync();

            return new RefreshTokenResponse
            {
                Account = MapToDto(account),
                JwtToken = GenerateJwtToken(account),
                RefreshToken = newRefreshToken.Token
            };
        }

        // ─── Revoke Token ────────────────────────────────────────────────────────

        public async Task RevokeTokenAsync(string token, string ipAddress, string browserInfo)
        {
            var refreshToken = await _db.RefreshTokens
                .Include(rt => rt.Account)
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken == null || !refreshToken.IsActive)
                throw new AppException("Invalid token");

            refreshToken.Revoked = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            await _db.SaveChangesAsync();

            await LogActivityAsync(refreshToken.Account!.AccountId, "logout", ipAddress, browserInfo);
        }

        // ─── Register ────────────────────────────────────────────────────────────

        public async Task RegisterAsync(RegisterRequest request, string origin)
        {
            if (await _db.Accounts.AnyAsync(a => a.Email == request.Email))
                throw new AppException($"Email '{request.Email}' is already registered");

            if (request.Password != request.ConfirmPassword)
                throw new AppException("Passwords do not match");

            var account = new Account
            {
                Title = request.Title,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Role = request.Role ?? "User",
                Uid = request.Uid,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                VerificationToken = RandomTokenString(),
                Created = DateTime.UtcNow,
                IsActive = true,
                AcceptTerms = request.AcceptTerms
            };

            _db.Accounts.Add(account);
            await _db.SaveChangesAsync();

            // Create default preferences
            _db.Preferences.Add(new Preferences { AccountId = account.AccountId });
            await _db.SaveChangesAsync();

            await SendVerificationEmailAsync(account, origin);
        }

        // ─── Verify Email ────────────────────────────────────────────────────────

        public async Task VerifyEmailAsync(VerifyEmailRequest request)
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.VerificationToken == request.Token);
            if (account == null) throw new AppException("Verification failed");

            account.Verified = DateTime.UtcNow;
            account.VerificationToken = null;
            await _db.SaveChangesAsync();
        }

        // ─── Forgot Password ─────────────────────────────────────────────────────

        public async Task ForgotPasswordAsync(ForgotPasswordRequest request, string origin)
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Email == request.Email);
            if (account == null) return; // Don't reveal if email exists

            account.ResetToken = RandomTokenString();
            account.ResetTokenExpires = DateTime.UtcNow.AddDays(1);
            await _db.SaveChangesAsync();

            await SendPasswordResetEmailAsync(account, origin);
        }

        // ─── Validate Reset Token ────────────────────────────────────────────────

        public async Task ValidateResetTokenAsync(ValidateResetTokenRequest request)
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a =>
                a.ResetToken == request.Token && a.ResetTokenExpires > DateTime.UtcNow);
            if (account == null) throw new AppException("Invalid token");
        }

        // ─── Reset Password ──────────────────────────────────────────────────────

        public async Task ResetPasswordAsync(ResetPasswordRequest request, string ipAddress, string browserInfo)
        {
            if (request.Password != request.ConfirmPassword)
                throw new AppException("Passwords do not match");

            var account = await _db.Accounts.FirstOrDefaultAsync(a =>
                a.ResetToken == request.Token && a.ResetTokenExpires > DateTime.UtcNow);
            if (account == null) throw new AppException("Invalid token");

            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            account.PasswordReset = DateTime.UtcNow;
            account.ResetToken = null;
            account.ResetTokenExpires = null;
            account.Updated = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogActivityAsync(account.AccountId, "password_reset", ipAddress, browserInfo);
        }

        // ─── Get All ─────────────────────────────────────────────────────────────

        public async Task<IEnumerable<AccountDto>> GetAllAsync(string search, bool? isActive)
        {
            try
            {
                var query = _db.Accounts.Include(a => a.Rooms).AsQueryable();

                if (isActive.HasValue)
                    query = query.Where(a => a.IsActive == isActive.Value);

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(a =>
                        a.FirstName.Contains(search) ||
                        a.LastName.Contains(search) ||
                        a.Email.Contains(search) ||
                        (a.Uid != null && a.Uid.Contains(search)) ||
                        a.PhoneNumber.Contains(search));
                }

                var accounts = await query.ToListAsync();
                return accounts.Select(MapToDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetAllAsync: {ex.Message}");
                throw;
            }
        }

        // ─── Get By Id ───────────────────────────────────────────────────────────

        public async Task<AccountDto> GetByIdAsync(int accountId)
        {
            var account = await _db.Accounts
                .Include(a => a.Rooms)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (account == null) throw new AppException("Account not found");
            return MapToDto(account);
        }

        // ─── Create ──────────────────────────────────────────────────────────────

        public async Task<AccountDto> CreateAsync(CreateAccountRequest request, int createdByAccountId, string ipAddress, string browserInfo)
        {
            if (await _db.Accounts.AnyAsync(a => a.Email == request.Email))
                throw new AppException($"Email '{request.Email}' is already registered");

            if (request.Password != request.ConfirmPassword)
                throw new AppException("Passwords do not match");

            var account = new Account
            {
                Title = request.Title,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Role = request.Role ?? "User",
                Uid = request.Uid,
                BranchId = request.BranchId,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Verified = DateTime.UtcNow,
                Created = DateTime.UtcNow,
                IsActive = true
            };

            _db.Accounts.Add(account);
            await _db.SaveChangesAsync();

            _db.Preferences.Add(new Preferences { AccountId = account.AccountId });
            await _db.SaveChangesAsync();

            await LogActivityAsync(createdByAccountId, "account_created", ipAddress, browserInfo,
                $"Created account for {account.Email}");

            return MapToDto(account);
        }

        // ─── Update ──────────────────────────────────────────────────────────────

        public async Task<AccountDto> UpdateAsync(int accountId, UpdateAccountRequest request, string ipAddress, string browserInfo, string requesterRole, int requesterId)
        {
            var account = await _db.Accounts.Include(a => a.Rooms)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);
            if (account == null) throw new AppException("Account not found");

            if (request.Email != null && request.Email != account.Email &&
                await _db.Accounts.AnyAsync(a => a.Email == request.Email))
                throw new AppException($"Email '{request.Email}' is already taken");

            if (request.Password != null && request.Password != request.ConfirmPassword)
                throw new AppException("Passwords do not match");

            if (request.Title != null) account.Title = request.Title;
            if (request.FirstName != null) account.FirstName = request.FirstName;
            if (request.LastName != null) account.LastName = request.LastName;
            if (request.Email != null) account.Email = request.Email;
            if (request.PhoneNumber != null) account.PhoneNumber = request.PhoneNumber;
            if (request.Uid != null) account.Uid = request.Uid;
            if (request.BranchId.HasValue) account.BranchId = request.BranchId;
            if (request.Password != null)
                account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            if (request.IsActive.HasValue) account.IsActive = request.IsActive.Value;
            if (request.Role != null && requesterRole == "Admin") account.Role = request.Role;

            account.Updated = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogActivityAsync(requesterId, "account_updated", ipAddress, browserInfo,
                $"Updated account {account.Email}");

            return MapToDto(account);
        }

        // ─── Delete ──────────────────────────────────────────────────────────────

        public async Task DeleteAsync(int accountId, string ipAddress, string browserInfo, string requesterRole, int requesterId)
        {
            var account = await _db.Accounts.FindAsync(accountId);
            if (account == null) throw new AppException("Account not found");

            account.IsActive = false;
            account.Updated = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogActivityAsync(requesterId, "account_deleted", ipAddress, browserInfo,
                $"Deactivated account {account.Email}");
        }

        // ─── Preferences ─────────────────────────────────────────────────────────

        public async Task<object> GetPreferencesAsync(int accountId)
        {
            var pref = await _db.Preferences.FirstOrDefaultAsync(p => p.AccountId == accountId);
            if (pref == null)
            {
                pref = new Preferences { AccountId = accountId };
                _db.Preferences.Add(pref);
                await _db.SaveChangesAsync();
            }
            return pref;
        }

        public async Task UpdatePreferencesAsync(int accountId, UpdatePreferencesRequest request)
        {
            var pref = await _db.Preferences.FirstOrDefaultAsync(p => p.AccountId == accountId);
            if (pref == null)
            {
                pref = new Preferences { AccountId = accountId };
                _db.Preferences.Add(pref);
            }

            if (request.Theme != null) pref.Theme = request.Theme;
            if (request.Language != null) pref.Language = request.Language;
            if (request.EmailNotifications.HasValue) pref.EmailNotifications = request.EmailNotifications.Value;
            pref.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // ─── Room Assignment ─────────────────────────────────────────────────────

        public async Task<IEnumerable<AccountDto>> GetAccountsByRoomAsync(int roomId)
        {
            var accounts = await _db.Accounts
                .Include(a => a.Rooms)
                .Where(a => a.Rooms.Any(r => r.RoomId == roomId))
                .ToListAsync();
            return accounts.Select(MapToDto);
        }

        public async Task<IEnumerable<AccountDto>> GetUnassignedAccountsAsync(int roomId, string? search)
        {
            var query = _db.Accounts
                .Include(a => a.Rooms)
                .Where(a => !a.Rooms.Any(r => r.RoomId == roomId) && a.IsActive);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(a => a.FirstName.Contains(search) || a.LastName.Contains(search) || a.Email.Contains(search));

            var accounts = await query.ToListAsync();
            return accounts.Select(MapToDto);
        }

        public async Task AddRoomToAccountAsync(int accountId, int roomId, string ipAddress, string browserInfo)
        {
            var account = await _db.Accounts.Include(a => a.Rooms)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);
            if (account == null) throw new AppException("Account not found");

            var room = await _db.Rooms.FindAsync(roomId);
            if (room == null) throw new AppException("Room not found");

            if (!account.Rooms.Any(r => r.RoomId == roomId))
            {
                account.Rooms.Add(room);
                await _db.SaveChangesAsync();
            }

            await LogActivityAsync(accountId, "room_added", ipAddress, browserInfo,
                $"Room '{room.RoomName}' added to account {account.Email}");
        }

        public async Task RemoveRoomFromAccountAsync(int accountId, int roomId, string ipAddress, string browserInfo)
        {
            var account = await _db.Accounts.Include(a => a.Rooms)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);
            if (account == null) throw new AppException("Account not found");

            var room = account.Rooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room != null)
            {
                account.Rooms.Remove(room);
                await _db.SaveChangesAsync();
            }

            await LogActivityAsync(accountId, "room_removed", ipAddress, browserInfo,
                $"Room (ID:{roomId}) removed from account {account.Email}");
        }

        // ─── Counts & Logs ───────────────────────────────────────────────────────

        public async Task<int> GetCountAsync() => await _db.Accounts.CountAsync();

        public async Task<object> GetAccountActivitiesAsync(int accountId, ActivityFilterRequest filters)
        {
            var query = _db.ActivityLogs
                .Where(a => a.AccountId == accountId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filters.ActionType))
                query = query.Where(a => a.ActionType.Contains(filters.ActionType));

            if (DateTime.TryParse(filters.StartDate, out var sd))
                query = query.Where(a => a.Timestamp >= sd);

            if (DateTime.TryParse(filters.EndDate, out var ed))
                query = query.Where(a => a.Timestamp <= ed);

            var total = await query.CountAsync();
            var logs = await query.OrderByDescending(a => a.Timestamp)
                .Skip((filters.Page - 1) * filters.Limit)
                .Take(filters.Limit)
                .ToListAsync();

            var logDtos = logs.Select(a => new ActivityLogDto
            {
                ActivityLogId = a.ActivityLogId,
                AccountId = a.AccountId,
                ActionType = a.ActionType,
                ActionDetails = a.ActionDetails,
                Timestamp = DateTimeHelper.ConvertToPhilippineTime(a.Timestamp)
            }).ToList();

            return new { total, logs = logDtos };
        }

        public async Task<object> GetAllActivityLogsAsync(ActivityLogFilterRequest filters)
        {
            var query = _db.ActivityLogs.AsQueryable();

            if (filters.AccountId.HasValue)
                query = query.Where(a => a.AccountId == filters.AccountId.Value);

            if (!string.IsNullOrEmpty(filters.ActionType))
                query = query.Where(a => a.ActionType.Contains(filters.ActionType));

            if (DateTime.TryParse(filters.StartDate, out var sd))
                query = query.Where(a => a.Timestamp >= sd);

            if (DateTime.TryParse(filters.EndDate, out var ed))
                query = query.Where(a => a.Timestamp <= ed);

            var total = await query.CountAsync();
            var logs = await query.OrderByDescending(a => a.Timestamp)
                .Skip((filters.Page - 1) * filters.Limit)
                .Take(filters.Limit)
                .ToListAsync();

            var logDtos = logs.Select(a => new ActivityLogDto
            {
                ActivityLogId = a.ActivityLogId,
                AccountId = a.AccountId,
                ActionType = a.ActionType,
                ActionDetails = a.ActionDetails,
                Timestamp = DateTimeHelper.ConvertToPhilippineTime(a.Timestamp)
            }).ToList();

            return new { total, logs = logDtos };
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private string GenerateJwtToken(Account account)
        {
            var secret = _config["Secret"];
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(secret!);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("AccountId", account.AccountId.ToString()),
                    new Claim(ClaimTypes.Role, account.Role)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private RefreshToken GenerateRefreshToken(Account account, string ipAddress)
        {
            return new RefreshToken
            {
                AccountId = account.AccountId,
                Token = RandomTokenString(),
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }

        private string RandomTokenString()
        {
            var bytes = new byte[40];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        private AccountDto MapToDto(Account account)
        {
            return new AccountDto
            {
                AccountId = account.AccountId,
                Title = account.Title,
                FirstName = account.FirstName,
                LastName = account.LastName,
                Email = account.Email,
                PhoneNumber = account.PhoneNumber,
                Role = account.Role,
                Uid = account.Uid,
                BranchId = account.BranchId,
                IsActive = account.IsActive,
                IsVerified = account.IsVerified,
                Created = DateTimeHelper.ConvertToPhilippineTime(account.Created),
                Updated = account.Updated.HasValue ? DateTimeHelper.ConvertToPhilippineTime(account.Updated.Value) : null,
                Rooms = account.Rooms?.Select(r => new RoomDto { RoomId = r.RoomId, RoomName = r.RoomName }).ToList()
            };
        }

        private async Task LogActivityAsync(int accountId, string actionType, string ipAddress, string browserInfo, string updateDetails = "")
        {
            _db.ActivityLogs.Add(new ActivityLog
            {
                AccountId = accountId,
                ActionType = actionType,
                ActionDetails = $"IP: {ipAddress}, Browser: {browserInfo}, Details: {updateDetails}",
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        private async Task SendVerificationEmailAsync(Account account, string origin)
        {
            var verifyUrl = $"{origin}/verify-email?token={account.VerificationToken}";
            var html = $@"
                <h4>Verify Email</h4>
                <p>Thanks for registering!</p>
                <p>Please click the below link to verify your email address:</p>
                <p><a href=""{verifyUrl}"">{verifyUrl}</a></p>";

            await _emailSender.SendEmailAsync(account.Email, "PowerGuard - Verify Email", html);
        }

        private async Task SendPasswordResetEmailAsync(Account account, string origin)
        {
            var resetUrl = $"{origin}/reset-password?token={account.ResetToken}";
            var html = $@"
                <h4>Reset Password</h4>
                <p>Please click the below link to reset your password:</p>
                <p><a href=""{resetUrl}"">{resetUrl}</a></p>
                <p>The link will be valid for 1 day.</p>";

            await _emailSender.SendEmailAsync(account.Email, "PowerGuard - Reset Password", html);
        }
    }
}