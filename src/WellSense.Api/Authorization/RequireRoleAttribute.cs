using Microsoft.AspNetCore.Authorization;

namespace WellSense.Api.Authorization;

/// <summary>
/// Envoltorio delgado sobre `[Authorize(Roles = ...)]` — NO es un middleware/mecanismo
/// nuevo de autorización, es el mecanismo estándar de ASP.NET Core que ya funciona sin
/// ningún cambio adicional: `JwtTokenService` (Bloque 2) ya emite
/// `new Claim(ClaimTypes.Role, role)` en cada token, y `RoleClaimType` nunca se
/// sobreescribió en la configuración del JWT bearer (Program.cs) — sigue siendo
/// `ClaimTypes.Role` por default, que es exactamente lo que `[Authorize(Roles=...)]`
/// verifica de forma nativa. Este atributo solo existe por legibilidad
/// (`[RequireRole("Admin")]` se lee más claro en los controllers que
/// `[Authorize(Roles = "Admin")]` repetido) — no agrega ninguna lógica de autorización
/// propia. Comportamiento heredado de ASP.NET Core, no reimplementado: sin autenticar →
/// 401; autenticado pero sin el rol → 403 (nunca 404 — ver HANDOFF de Bloque 9 sobre por
/// qué esto es deliberado para una superficie administrativa).
/// </summary>
public class RequireRoleAttribute : AuthorizeAttribute
{
    public string Role { get; }

    public RequireRoleAttribute(string role)
    {
        Role = role;
        Roles = role;
    }
}
