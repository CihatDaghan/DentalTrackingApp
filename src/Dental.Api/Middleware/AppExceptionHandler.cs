using Dental.Application.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dental.Api.Middleware;

/// <summary>Bilinen iş istisnalarını RFC 7807 ProblemDetails'e çevirir.</summary>
public sealed class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        (int status, string title) = exception switch
        {
            AuthFailedException => (StatusCodes.Status401Unauthorized, exception.Message),
            ValidationException => (StatusCodes.Status400BadRequest, "Doğrulama hatası."),
            KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            // Onam token'ı tek kullanımlık/süreli: kullanılmış veya süresi dolmuş link.
            Dental.Application.Consents.ConsentGoneException => (StatusCodes.Status410Gone, exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "Kayıt başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin."),
            InvalidOperationException => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (0, ""),
        };
        if (status == 0) return false;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
        };
        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken: ct);
        return true;
    }
}
