using System.Collections.Generic;
using PowerGuardCoreApi.Models;

namespace PowerGuardCoreApi.Models
{
    // ─── Requests ───────────────────────────────────────────────────────────────

    public class AuthenticateRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class RegisterRequest
    {
        public string Title { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
        public string Role { get; set; } = "User";
        public string? Uid { get; set; }
        public bool AcceptTerms { get; set; }
    }

    public class VerifyEmailRequest
    {
        public string Token { get; set; } = null!;
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = null!;
    }

    public class ValidateResetTokenRequest
    {
        public string Token { get; set; } = null!;
    }

    public class ResetPasswordRequest
    {
        public string Token { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }

    public class RevokeTokenRequest
    {
        public string? Token { get; set; }
    }

    public class CreateAccountRequest
    {
        public string Title { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
        public string Role { get; set; } = "User";
        public string? Uid { get; set; }
        public int? BranchId { get; set; }
        public List<int>? RoomIds { get; set; }
    }

    public class UpdateAccountRequest
    {
        public string? Title { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public string? Role { get; set; }
        public string? Uid { get; set; }
        public bool? IsActive { get; set; }
        public int? BranchId { get; set; }
        public List<int>? RoomIds { get; set; }
    }

    public class UpdatePreferencesRequest
    {
        public string? Theme { get; set; }
        public string? Language { get; set; }
        public bool? EmailNotifications { get; set; }
    }

    public class ActivityFilterRequest
    {
        public string? ActionType { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
    }

    public class ActivityLogFilterRequest
    {
        public string? ActionType { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public int? AccountId { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
    }

    // ─── Responses ───────────────────────────────────────────────────────────────

    public class AuthenticateResponse
    {
        public AccountDto Account { get; set; } = null!;
        public string JwtToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }

    public class RefreshTokenResponse
    {
        public AccountDto Account { get; set; } = null!;
        public string JwtToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }

    // ─── DTOs ────────────────────────────────────────────────────────────────────

    public class AccountDto
    {
        public int AccountId { get; set; }
        public string Title { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string? Uid { get; set; }
        public int? BranchId { get; set; }
        public bool IsActive { get; set; }
        public bool IsVerified { get; set; }
        public System.DateTime Created { get; set; }
        public System.DateTime? Updated { get; set; }
        public List<RoomDto>? Rooms { get; set; }
    }

    public class ActivityLogDto
    {
        public int ActivityLogId { get; set; }
        public int AccountId { get; set; }
        public string ActionType { get; set; } = null!;
        public string? ActionDetails { get; set; }
        public System.DateTime Timestamp { get; set; }
    }
}
