using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using System.Threading.Tasks;
using PowerGuardCoreApi._Helpers;

public class AuthorizeRolesAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _roles;

    public AuthorizeRolesAttribute(params string[] roles)
    {
        _roles = roles ?? Array.Empty<string>();
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = 401 };
            return;
        }

        var accountIdClaim = user.Claims.FirstOrDefault(c => c.Type == "AccountId");
        if (accountIdClaim == null)
        {
            context.Result = new JsonResult(new { message = "Invalid token: AccountId missing" }) { StatusCode = 401 };
            return;
        }

        var db = context.HttpContext.RequestServices.GetService(typeof(PowerGuardDbContext)) as PowerGuardDbContext;
        if (db == null)
        {
            context.Result = new JsonResult(new { message = "Internal server error" }) { StatusCode = 500 };
            return;
        }

        var accountId = int.Parse(accountIdClaim.Value);
        var account = await db.Accounts.FindAsync(accountId);
        if (account == null)
        {
            context.Result = new JsonResult(new { message = "Account no longer exists" }) { StatusCode = 401 };
            return;
        }

        if (!account.IsActive)
        {
            context.Result = new JsonResult(new { message = "Account is deactivated" }) { StatusCode = 403 };
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(account.Role))
        {
            context.Result = new JsonResult(new { message = "Unauthorized - Insufficient role permissions" }) { StatusCode = 403 };
            return;
        }

        context.HttpContext.Items["User"] = new
        {
            AccountId = account.AccountId,
            account.Role,
            account.BranchId,
            account.Email
        };
    }
}