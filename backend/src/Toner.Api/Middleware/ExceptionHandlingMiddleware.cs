using System.Net;
using FluentValidation;
using Toner.Application.Common.Exceptions;

namespace Toner.Api.Middleware;

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
            var (statusCode, title, errors) = Map(ex);

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogError(ex, "Error no controlado procesando {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsJsonAsync(new
            {
                title,
                status = (int)statusCode,
                errors
            });
        }
    }

    private static (HttpStatusCode StatusCode, string Title, object? Errors) Map(Exception ex) => ex switch
    {
        ValidationException vex => (
            HttpStatusCode.BadRequest,
            "Uno o más campos no son válidos.",
            vex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

        NotFoundException => (HttpStatusCode.NotFound, ex.Message, null),
        ConflictException => (HttpStatusCode.Conflict, ex.Message, null),
        InvalidCredentialsException => (HttpStatusCode.Unauthorized, ex.Message, null),
        ForbiddenException => (HttpStatusCode.Forbidden, ex.Message, null),

        _ => (HttpStatusCode.InternalServerError, "Ocurrió un error inesperado.", null)
    };
}
