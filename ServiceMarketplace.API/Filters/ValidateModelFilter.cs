using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ServiceMarketplace.API.Filters;

// Purpose: Converts model state errors into FluentValidation exceptions for uniform handling.
public class ValidateModelFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
            return;

        var failures = context.ModelState
            .SelectMany(kvp => kvp.Value?.Errors.Select(e => new { kvp.Key, e.ErrorMessage }) ?? Enumerable.Empty<dynamic>())
            .Select(x => new FluentValidation.Results.ValidationFailure(x.Key, x.ErrorMessage));

        throw new ValidationException(failures);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
