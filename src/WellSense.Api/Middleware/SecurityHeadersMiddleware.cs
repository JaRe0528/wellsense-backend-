namespace WellSense.Api.Middleware;

/// <summary>
/// Bloque 10 (hardening de código, no de infraestructura). Solo los headers que tiene
/// sentido fijar desde la app en sí:
/// - X-Content-Type-Options: nosniff — evita que el navegador intente "adivinar" un
///   tipo de contenido distinto al declarado (protección contra ataques de MIME-sniffing).
/// - X-Frame-Options: DENY — esta API nunca debe embeberse en un iframe (no es una app
///   con UI, es puramente una API JSON/SignalR).
/// - Referrer-Policy: strict-origin-when-cross-origin — no filtra la URL completa
///   (que podría incluir tokens en query string, ej. el `access_token` de SignalR) a
///   sitios de terceros vía el header Referer.
///
/// CSP y HSTS quedan deliberadamente FUERA de este bloque — ambos dependen de que haya
/// HTTPS real en producción (HSTS le dice al navegador "solo hables conmigo por HTTPS",
/// y CSP tiene que declarar orígenes de scripts/estilos que este backend, al no servir
/// ninguna página HTML, no puede anticipar correctamente sin coordinarse con Web) — eso
/// es responsabilidad de DevSecOps cuando el despliegue real tenga TLS terminado, no de
/// este chat.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            return Task.CompletedTask;
        });

        await next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
