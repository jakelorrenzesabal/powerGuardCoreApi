using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PowerGuardCoreApi._Helpers;
using PowerGuardCoreApi.Models;
using PowerGuardCoreApi.Services;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate([FromBody] AuthenticateRequest model)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
        var result = await _accountService.AuthenticateAsync(model, ipAddress, browserInfo);
        SetTokenCookie(result.RefreshToken);
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        var token = Request.Cookies["refreshToken"] ?? string.Empty;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var result = await _accountService.RefreshTokenAsync(token, ipAddress);
        SetTokenCookie(result.RefreshToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("revoke-token")]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest model)
    {
        var token = model.Token ?? Request.Cookies["refreshToken"] ?? string.Empty;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(token))
            return BadRequest(new { message = "Token is required" });

        await _accountService.RevokeTokenAsync(token, ipAddress, browserInfo);
        return Ok(new { message = "Token revoked" });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest model)
    {
        await _accountService.RegisterAsync(model, Request.Headers["Origin"].ToString());
        return Ok(new { message = "Registration successful, please check your email for verification instructions" });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest model)
    {
        await _accountService.VerifyEmailAsync(model);
        return Ok(new { message = "Verification successful, you can now login" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest model)
    {
        await _accountService.ForgotPasswordAsync(model, Request.Headers["Origin"].ToString());
        return Ok(new { message = "Please check your email for password reset instructions" });
    }

    [HttpPost("validate-reset-token")]
    public async Task<IActionResult> ValidateResetToken([FromBody] ValidateResetTokenRequest model)
    {
        await _accountService.ValidateResetTokenAsync(model);
        return Ok(new { message = "Token is valid" });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest model)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
        await _accountService.ResetPasswordAsync(model, ipAddress, browserInfo);
        return Ok(new { message = "Password reset successful, you can now login" });
    }

    [Authorize]
    [HttpGet("{preferenceId}/preferences")]
    public async Task<IActionResult> GetPreferences(int preferenceId)
    {
        // Add role/user check as needed
        var preferences = await _accountService.GetPreferencesAsync(preferenceId);
        return Ok(preferences);
    }

    [Authorize]
    [HttpPut("{preferenceId}/preferences")]
    public async Task<IActionResult> UpdatePreferences(int preferenceId, [FromBody] UpdatePreferencesRequest model)
    {
        // Add role/user check as needed
        await _accountService.UpdatePreferencesAsync(preferenceId, model);
        return Ok(new { message = "Preferences updated successfully" });
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpGet("room/{roomId}")]
    public async Task<IActionResult> GetAccountsByRoom(int roomId)
    {
        var accounts = await _accountService.GetAccountsByRoomAsync(roomId);
        return Ok(accounts);
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpGet("unassigned/{roomId}")]
    public async Task<IActionResult> GetUnassignedAccounts(int roomId, [FromQuery] string search)
    {
        var accounts = await _accountService.GetUnassignedAccountsAsync(roomId, search);
        return Ok(accounts);
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpPost("{AccountId}/rooms/{roomId}")]
    public async Task<IActionResult> AddRoomToAccount(int AccountId, int roomId)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
        await _accountService.AddRoomToAccountAsync(AccountId, roomId, ipAddress, browserInfo);
        return Ok(new { message = "Room added to account successfully" });
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpDelete("{AccountId}/rooms/{roomId}")]
    public async Task<IActionResult> RemoveRoomFromAccount(int AccountId, int roomId)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
        await _accountService.RemoveRoomFromAccountAsync(AccountId, roomId, ipAddress, browserInfo);
        return Ok(new { message = "Room removed from account successfully" });
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string search, [FromQuery] bool? isActive)
    {
        var accounts = await _accountService.GetAllAsync(search, isActive);
        return Ok(accounts);
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetAccountCount()
    {
        var count = await _accountService.GetCountAsync();
        return Ok(new { count });
    }

    [Authorize]
    [HttpPost("{AccountId}/activity")]
    public async Task<IActionResult> GetActivities(int AccountId, [FromBody] ActivityFilterRequest filters)
    {
        // Add role/user check as needed
        var activities = await _accountService.GetAccountActivitiesAsync(AccountId, filters);
        return Ok(activities);
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpGet("activity-logs")]
    public async Task<IActionResult> GetAllActivityLogs([FromQuery] ActivityLogFilterRequest filters)
    {
        var logs = await _accountService.GetAllActivityLogsAsync(filters);
        return Ok(new { success = true, data = logs });
    }

    [Authorize]
    [HttpGet("{AccountId}")]
    public async Task<IActionResult> GetById(int AccountId)
    {
        // Add role/user check as needed
        var account = await _accountService.GetByIdAsync(AccountId);
        if (account == null) return NotFound();
        return Ok(account);
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest model)
    {
        var adminId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
        var account = await _accountService.CreateAsync(model, adminId, ipAddress, browserInfo);
        return Ok(account);
    }

    [Authorize]
    [HttpPut("{AccountId}")]
    public async Task<IActionResult> Update(int AccountId, [FromBody] UpdateAccountRequest model)
    {
        // Add role/user check as needed
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
        var userRole = User.FindFirst("role")?.Value ?? string.Empty;
        var userId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
        var account = await _accountService.UpdateAsync(AccountId, model, ipAddress, browserInfo, userRole, userId);
        return Ok(new { success = true, message = "Account updated successfully", account });
    }

    [Authorize]
    [HttpDelete("{AccountId}")]
    public async Task<IActionResult> Delete(int AccountId)
    {
        // Add role/user check as needed
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var browserInfo = Request.Headers["User-Agent"].ToString() ?? string.Empty;
        var userRole = User.FindFirst("role")?.Value ?? string.Empty;
        var userId = int.Parse(User.FindFirst("AccountId")?.Value ?? "0");
        await _accountService.DeleteAsync(AccountId, ipAddress, browserInfo, userRole, userId);
        return Ok(new { message = "Account deleted successfully" });
    }

    private void SetTokenCookie(string token)
    {
        if (string.IsNullOrEmpty(token))
            return;

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Expires = DateTime.UtcNow.AddDays(7),
            SameSite = SameSiteMode.Lax,
            Secure = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
        };
        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }
}