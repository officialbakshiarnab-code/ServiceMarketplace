using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ServiceMarketplace.Application.Exceptions;
using System.Net;

namespace ServiceMarketplace.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var traceId = context.TraceIdentifier;

        var (statusCode, title, errorType, errors) = ex switch
        {
            ValidationException vex => (
                (int)HttpStatusCode.BadRequest,
                "Validation failed",
                "validation_error",
                vex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ),
            NotFoundException nf => ((int)HttpStatusCode.NotFound, nf.Message, "not_found", null),
            UnauthorizedAccessException ua => ((int)HttpStatusCode.Unauthorized, ua.Message, "unauthorized", null),
            ForbiddenException fb => ((int)HttpStatusCode.Forbidden, fb.Message, "forbidden", null),
            _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred", "server_error", null)
        };

        if (statusCode >= 500)
        {
            _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", traceId);
        }
        else
        {
            _logger.LogInformation(ex, "Request failed. Type={ErrorType} TraceId={TraceId}", errorType, traceId);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = errorType,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"] = traceId;
        if (errors != null)
            problem.Extensions["errors"] = errors;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(problem);
    }
}
