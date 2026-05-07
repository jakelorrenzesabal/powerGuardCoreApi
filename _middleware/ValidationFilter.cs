using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FluentValidation;
using FluentValidation.Results;

namespace PowerGuardCoreApi._middleware
{
    public class ValidationFilter<T> : IAsyncActionFilter where T : class
    {
        private readonly IValidator<T> _validator;

        public ValidationFilter(IValidator<T> validator)
        {
            _validator = validator;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var model = context.ActionArguments.Values.OfType<T>().FirstOrDefault();
            if (model == null)
            {
                await next();
                return;
            }

            ValidationResult result = await _validator.ValidateAsync(model, options =>
            {
                options.IncludeAllRuleSets();
                options.ThrowOnFailures();
            });

            if (!result.IsValid)
            {
                var messages = string.Join(", ", result.Errors.Select(x => x.ErrorMessage));
                context.Result = new BadRequestObjectResult(new { message = $"Validation error: {messages}" });
                return;
            }

            await next();
        }
    }
}