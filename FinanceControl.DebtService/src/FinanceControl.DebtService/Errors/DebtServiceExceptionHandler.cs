using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.DebtService.Errors;

internal sealed class DebtServiceExceptionHandler(
    ILogger<DebtServiceExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ResourceNotFoundException => CreateProblem(
                StatusCodes.Status404NotFound,
                "Resource not found",
                exception.Message),
            DomainValidationException validationException => CreateValidationProblem(validationException),
            InvalidOperationException => CreateProblem(
                StatusCodes.Status409Conflict,
                "Operation conflict",
                exception.Message),
            DbUpdateException => CreateProblem(
                StatusCodes.Status409Conflict,
                "Data conflict",
                "The operation conflicts with existing debt data."),
            _ => null
        };

        if (problemDetails is null)
        {
            return false;
        }

        logger.LogWarning(exception, "Debt Service request failed with status {StatusCode}", problemDetails.Status);
        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        }

        return true;
    }

    private static ProblemDetails CreateProblem(int status, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }

    private static HttpValidationProblemDetails CreateValidationProblem(
        DomainValidationException exception)
    {
        return new HttpValidationProblemDetails(exception.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more fields are invalid."
        };
    }
}
