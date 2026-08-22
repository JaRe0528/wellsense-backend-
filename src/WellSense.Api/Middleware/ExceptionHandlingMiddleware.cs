using System.Net;
using System.Text.Json;
using FluentValidation;
using WellSense.Application.Common.Exceptions;

namespace WellSense.Api.Middleware;

/// <summary>
/// Middleware global de excepciones. Convierte cualquier excepción no manejada en un
/// ProblemDetails (RFC 7807) consistente. Nunca expone stack traces al cliente en
/// producción; el detalle completo va solo a Serilog.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException vex)
        {
            logger.LogWarning(vex, "Fallo de validación en {Path}", context.Request.Path);
            await WriteProblem(context, HttpStatusCode.BadRequest, "Error de validación",
                vex.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage }));
        }
        catch (AuthDomainException aex)
        {
            // Nunca loguear el password/token/código que venía en la request que
            // disparó esta excepción — solo el errorCode estable y el path.
            logger.LogWarning("Error de Auth {ErrorCode} en {Path}", aex.ErrorCode, context.Request.Path);
            await WriteProblem(context, (HttpStatusCode)aex.HttpStatus, aex.Message, new { code = aex.ErrorCode });
        }
        catch (UnauthorizedAccessException uex)
        {
            logger.LogWarning(uex, "Acceso no autorizado en {Path}", context.Request.Path);
            await WriteProblem(context, HttpStatusCode.Unauthorized, "No autorizado", null);
        }
        catch (KeyNotFoundException knf)
        {
            logger.LogWarning(knf, "Recurso no encontrado en {Path}", context.Request.Path);
            await WriteProblem(context, HttpStatusCode.NotFound, "Recurso no encontrado", null);
        }
        catch (Exception ex)
        {
            // Nunca loguear el body de la request completo aquí: podría contener
            // password/CVV/tokens. Solo se loguea el mensaje de la excepción y el path.
            logger.LogError(ex, "Error no manejado en {Path}", context.Request.Path);
            await WriteProblem(context, HttpStatusCode.InternalServerError,
                "Ocurrió un error interno. Inténtalo de nuevo más tarde.",
                env.IsDevelopment() ? new { detail = ex.Message } : null);
        }
    }

    private static async Task WriteProblem(HttpContext context, HttpStatusCode status, string title, object? errors)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;

        var problem = new
        {
            type = $"https://httpstatuses.com/{(int)status}",
            title,
            status = (int)status,
            traceId = context.TraceIdentifier,
            errors
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
