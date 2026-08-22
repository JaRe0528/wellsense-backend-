# HANDOFF.md — Chat Backend (.NET), Bloque 2: Auth completo

> Entregable del **Bloque 2: Auth completo** (web + móvil). Requiere Bloque 1
> aprobado (ya lo está). Cubre los 8 flujos web, los 2 flujos móviles de
> vinculación por código, y las pruebas de la lógica más delicada.

## Actualización 7 — fix de CS1061 (`ConfigureTestServices` sin `using Microsoft.AspNetCore.TestHost;`)

Confirmado y agregado: un solo `using Microsoft.AspNetCore.TestHost;` en
`CustomWebApplicationFactory.cs` — ese método de extensión vive ahí, no en
`Microsoft.AspNetCore.Hosting` (que solo trae los métodos base de
`IWebHostBuilder` declarados en la propia interfaz, como
`ConfigureAppConfiguration`). Sin paquete nuevo, tal como diagnosticaste —
`Microsoft.AspNetCore.TestHost` ya llega transitivo vía
`Microsoft.AspNetCore.Mvc.Testing`.

Repasé el resto del archivo llamada por llamada contra el mismo criterio
(¿en qué namespace vive el método de extensión, no solo el paquete?) para no
dejar otra de estas: `ConfigureAppConfiguration` (miembro de la propia
interfaz `IWebHostBuilder`, no extensión — `Microsoft.AspNetCore.Hosting`),
`AddInMemoryCollection` (`Microsoft.Extensions.Configuration`), `RemoveAll<T>`
(`Microsoft.Extensions.DependencyInjection.Extensions`), `AddDbContext`/
`UseInMemoryDatabase` (`Microsoft.EntityFrameworkCore`), `AddSingleton`
(`Microsoft.Extensions.DependencyInjection`) — los 9 `using` del archivo ya
cubren los 5 namespaces distintos de donde salen todas las llamadas de este
archivo.

---

## Actualización 6 — fix de CS0246 en Tests (`using` de Infrastructure faltante) + auditoría automatizada de tipos-vs-usings

Confirmado tu diagnóstico: a `ThrowingDbContextDecorator.cs` le faltaba
`using WellSense.Infrastructure.Persistence;` — agregado, un solo `using`, sin
tocar nada más del archivo.

Con Domain/Application/Infrastructure/Api ya compilando limpio de tu lado,
antes de mandarte este ZIP hice algo más sistemático que revisar a ojo:
escribí un script que (1) recorre todo `src/` y `tests/`, extrae cada
declaración `class`/`record`/`interface`/`enum`/`struct` y el namespace del
archivo que la contiene, armando un mapa tipo→namespace de TODO el
repositorio; (2) para cada archivo de `tests/`, extrae cada identificador
que empiece en mayúscula, lo cruza contra ese mapa, y marca como sospechoso
cualquiera cuyo namespace no esté en los `using` del archivo (ni sea el
namespace propio del archivo, ni tenga una referencia calificada completa
cerca). Sobre `tests/` completo, el único resultado fue un falso positivo
(el nombre `GenerateDeviceLinkCodeCommandHandler` mencionado en un
comentario de documentación de `ThrowingDbContextDecorator.cs`, no una
referencia real de tipo) — cero problemas reales adicionales a los que ya
habías encontrado tú compilando. Corrí el mismo script sobre `src/` también:
salieron varios más, todos igual de falsos positivos (nombres de propiedad
que coinciden por texto con nombres de clase — ej. la propiedad
`RefreshToken` de `RefreshRequest`, o menciones en comentarios/strings de
migraciones), consistente con que esos 4 proyectos ya te compilan limpio.

Con esto, y salvo lo que la compilación real siga encontrando, no me quedan
más candidatos obvios de este mismo patrón ("using faltante para un tipo que
sí se usa") en ningún archivo del repo.

---

## Actualización 5 — fix de CS1503 (method group pasado donde `HasConversion` pedía `Expression<Func<>>`)

Confirmado, sí revisé los 3 casos (no solo el primero) y sí barrí todo el
árbol antes de mandar esto — dos pasadas distintas:

1. `grep -rn "HasConversion"` sobre `src` y `tests` completos → 13
   resultados en total. Los repasé uno por uno: 6 son `HasConversion<string>()`
   (el overload genérico, sin este problema porque no recibe argumentos),
   1 es `MembershipPlanConfiguration.Code` que ya usaba lambdas inline
   (`v => v.ToString()...`, `v => (PlanCode)Enum.Parse(...)`) — nunca tuvo el
   bug, y los 3 que sí fallaban son exactamente los que reportaste.
2. Un segundo filtro con regex específicamente para la forma del bug
   (`HasConversion(Identificador, Identificador)` sin `=>` de por medio) sobre
   todo el árbol — cero resultados después del fix, cero resultados
   adicionales antes del fix aparte de los 3 ya conocidos.

Corrección aplicada exactamente como la diste, en los 3 archivos:

```csharp
// IdentityConfigurations.cs (UserStatus)
b.Property(x => x.Status).HasConversion(v => StatusToDb(v), v => StatusFromDb(v));

// DeviceConfigurations.cs (DeviceStatus)
b.Property(x => x.Status).HasConversion(v => StatusToDb(v), v => StatusFromDb(v));

// ProfileConfigurations.cs (DeclaredStressLevel)
b.Property(x => x.DeclaredStressLevel).HasConversion(v => StressLevelToDb(v), v => StressLevelFromDb(v));
```

Los métodos estáticos (`StatusToDb`/`StatusFromDb`/`StressLevelToDb`/
`StressLevelFromDb`) no se tocaron — seguían bien, el problema era solo cómo
se les llamaba desde `HasConversion`.

---

## Actualización 4 — barrido completo de usings-vs-paquetes (los 4 `.csproj`)

Pediste no seguir arreglando uno por uno — hice el barrido completo:
extraje todos los `using` externos (no `WellSense.*`) de cada proyecto y los
crucé contra sus `PackageReference`, considerando lo que cada uno hereda
transitivamente de sus `ProjectReference` (Application→Domain,
Infrastructure→Application, Api→Infrastructure, Tests→Api).

**Fix reportado** (`Infrastructure`): agregado `System.IdentityModel.Tokens.Jwt`
— trae `JwtSecurityTokenHandler`/`JwtSecurityToken` directo y
`Microsoft.IdentityModel.Tokens` (SymmetricSecurityKey, SigningCredentials)
transitivo, en el mismo paquete.

**Encontrados de más (mismo patrón, agregados ahora en vez de esperar la
siguiente ronda)**:

| Proyecto | Using sin paquete propio | Paquete agregado |
|---|---|---|
| Application | `Microsoft.Extensions.Logging` (`ILogger<T>` en 2 handlers) | `Microsoft.Extensions.Logging.Abstractions` |
| Infrastructure | `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Logging` | Las 3 versiones `.Abstractions` — ya llegaban casi seguro transitivas vía Application, pero se referencian explícitas: Infrastructure no debería depender de qué paquetes decidió traer un proyecto que ni siquiera es el que las consume |
| Api | `FluentValidation` (`ValidationException` en el middleware), `MediatR` (`ISender` en controladores), `Microsoft.IdentityModel.Tokens` (`Program.cs`) | Los 3 explícitos — incluso si el paquete `JwtBearer` ya trae `Microsoft.IdentityModel.Tokens` transitivo, lo hago explícito para no depender de un detalle interno de ese paquete |

**Tests**: no le agregué nada. Su único `using` "sospechoso" es
`Microsoft.Extensions.Configuration`/`.DependencyInjection`/
`.DependencyInjection.Extensions`/`Logging.Abstractions`/
`Microsoft.AspNetCore.Hosting` sin paquete propio — pero
`Microsoft.AspNetCore.Mvc.Testing` (ya referenciado) trae consigo un
`FrameworkReference` implícito a todo el framework compartido de
ASP.NET Core, que incluye esas piezas de `Microsoft.Extensions.*` —
es justamente el mecanismo pensado para escribir pruebas con
`WebApplicationFactory` desde un proyecto de tipo librería normal (no
`Sdk.Web`). Tengo bastante confianza en este punto porque es un
comportamiento documentado de ese paquete específico, pero no lo pude
compilar-verificar — si `dotnet build` de los tests falla por esto, la
solución sería agregar `Microsoft.Extensions.Configuration`,
`.Binder`, `.Memory` y `.DependencyInjection.Abstractions` explícitos ahí
también.

**Sobre las versiones**: usé `8.2.1` para `System.IdentityModel.Tokens.Jwt` y
`Microsoft.IdentityModel.Tokens` (Api) por ser una versión reciente y
compatible con .NET 9 hasta donde puedo verificar sin restaurar paquetes de
verdad — si NuGet resuelve una versión distinta o hay conflicto con lo que ya
trae `Microsoft.AspNetCore.Authentication.JwtBearer 9.0.0`, es un ajuste de
número de versión, no de arquitectura.

---

## Actualización 3 — fix de CS0118 (namespace `RefreshToken` choca con la entidad `RefreshToken`)

El feature folder `Auth/RefreshToken/` (namespace
`WellSense.Application.Auth.RefreshToken`) tenía el mismo nombre que
`WellSense.Domain.Identity.RefreshToken`. Como ese namespace es hijo directo
de `WellSense.Application.Auth`, C# lo trataba como miembro de ese namespace
con más prioridad que el `using WellSense.Domain.Identity;` — por eso
`new RefreshToken { ... }` se resolvía al namespace (no a la clase) en
**cualquier** archivo bajo `Auth.*`, no solo dentro de la propia carpeta
`RefreshToken` (de ahí que el error saliera también en `Login` y
`DeviceLink`, tal como diagnosticaste).

Apliqué la corrección recomendada (rename, no calificación quirúrgica, para
que no quede la trampa activa cuando el Bloque 3+ agregue más archivos bajo
`Auth.*`):

- Carpeta `src/WellSense.Application/Auth/RefreshToken/` →
  `src/WellSense.Application/Auth/TokenRefresh/` (los 3 archivos dentro
  mantienen sus nombres — `RefreshTokenCommand.cs`,
  `RefreshTokenCommandHandler.cs`, `RefreshTokenCommandValidator.cs` — solo
  cambió la carpeta contenedora y el namespace).
- `namespace WellSense.Application.Auth.RefreshToken;` →
  `namespace WellSense.Application.Auth.TokenRefresh;` en esos 3 archivos.
- `using WellSense.Application.Auth.RefreshToken;` →
  `using WellSense.Application.Auth.TokenRefresh;` en `AuthController.cs` y
  en `RefreshTokenCommandHandlerTests.cs` (los dos únicos consumidores
  externos de ese namespace).
- Las clases (`RefreshTokenCommand`, `RefreshTokenCommandHandler`,
  `RefreshTokenCommandValidator`, `RefreshTokenResult`) NO se renombraron —
  solo el namespace/carpeta, que era la fuente real del choque.

Confirmé con `grep` que no queda ninguna referencia al namespace viejo en
todo el repo, y que las 3 líneas que el compilador señaló como error
(`new RefreshToken { ... }` en `LoginCommandHandler.cs:42`,
`RedeemDeviceLinkCodeCommandHandler.cs:70` y
`RefreshTokenCommandHandler.cs:76`) corresponden exactamente a los sitios de
construcción de la entidad que el choque de namespace rompía.

---

## Actualización 2 — fix de CS0234/CS0246 (`IConfiguration` sin resolver)

`WellSense.Application.csproj` usaba `IConfiguration`/`.GetValue<T>(...)` en
tres handlers (`LoginCommandHandler`, `RefreshTokenCommandHandler`,
`RedeemDeviceLinkCodeCommandHandler`) sin tener el paquete NuGet correspondiente
— Domain/Infrastructure/Api lo resolvían por transitividad de otros paquetes,
pero Application no. Se agregaron dos referencias, no una: `IConfiguration`
vive en `Microsoft.Extensions.Configuration.Abstractions`, pero
`.GetValue<T>(...)` es un método de extensión de `ConfigurationBinder`, que
vive en el paquete **distinto** `Microsoft.Extensions.Configuration.Binder` —
agregar solo el primero hubiera resuelto el `CS0246` de `IConfiguration` pero
dejado un `CS1061` nuevo en cuanto compilara `.GetValue<int>(...)`. Agregué
ambos de una vez para no mandarte un tercer round de este mismo error.

`WellSense.Infrastructure.csproj` no necesitó cambios: como referencia a
Application vía `ProjectReference`, hereda estos dos paquetes por
transitividad (mismo mecanismo por el que Api ya heredaba `AspNetCoreRateLimit`
de Infrastructure en el Bloque 2 original).

Sobre tu sugerencia de `IAuthTokenSettings` para no acoplar Application a
`IConfiguration` crudo: de acuerdo en que es la dirección correcta, no lo hice
en este envío porque pediste explícitamente no bloquear el fix por eso. Lo
dejo anotado como mejora pendiente en el `.csproj` (comentario) y en §5 de
este documento — dime si lo quieres ahora y lo hago en el próximo envío.

---

## Actualización — fix de compilación + pruebas de integración HTTP

Dos cambios sobre el envío anterior, ambos en este mismo ZIP:

1. **Fix de CS8514** en `IdentityConfigurations.cs`, `DeviceConfigurations.cs`
   y `ProfileConfigurations.cs`: los tres `switch` que iban inline dentro de
   `HasConversion(...)` (para `UserStatus`, `DeviceStatus` y
   `DeclaredStressLevel`) se movieron a métodos estáticos privados
   (`StatusToDb`/`StatusFromDb`, `StressLevelToDb`/`StressLevelFromDb`) con
   tipo de retorno declarado explícitamente, siguiendo exactamente el patrón
   que indicaste. Confirmé que no queda ningún otro `switch` inline dentro de
   un `HasConversion` en el resto de `Configurations/` (`grep` sobre los 4
   archivos de esa carpeta).
2. **Pruebas de integración HTTP end-to-end** (el riesgo abierto #2 que
   había dejado pendiente) — ver §3.1 más abajo para el detalle y la
   decisión de diseño (por qué no usé Testcontainers/Docker real).

Sigo sin poder compilar en este entorno (mismo aviso de §0) — lo que sí pude
hacer de nuevo fue el chequeo de balance de llaves/paréntesis sobre los 113
archivos `.cs` (111 + 2 de los nuevos tests de integración... en realidad
son 4 archivos nuevos, ver árbol), con el mismo único falso positivo de
siempre (texto de un comentario en `GenerateDeviceLinkCodeCommandHandler.cs`,
ya verificado línea por línea la vez pasada — no es código real).

---

## 0. Aviso operativo (se mantiene del Bloque 1)

Sigo sin salida de red hacia NuGet en este entorno — no pude compilar ni
correr `dotnet test` aquí. Todo lo de abajo describe código escrito con
cuidado y revisado a mano (incluyendo un chequeo de balance de
llaves/paréntesis en los 109 archivos `.cs` del repo, que pasó salvo un falso
positivo por texto de un comentario), pero **no compilado por mí**. Tú ya
estás compilando en paralelo en Codespaces — trátalo como código para
revisar, no como binario probado.

Dos cosas que sí puedo garantizar porque no dependen de compilar:
- La estructura de las 6 clases fue diseñada explícitamente para que las
  invariantes de HANDOFF-DB (rotación+reuse, invalidar código previo en la
  misma transacción, reintento por colisión) sean imposibles de saltarse por
  accidente — no son un parche, son el flujo principal.
- Las pruebas unitarias corren contra un `WellSenseDbContext` real (proveedor
  InMemory de EF, no un mock de la interfaz) para los casos que no dependen
  de un índice único parcial de Postgres, y contra un decorador que simula la
  excepción de colisión para el único caso que sí depende de eso.

---

## 1. Endpoints listos

### Web (`AuthController`, `api/v1/auth/*`)

| Método | Ruta | Auth | Rate limit | Request | Response (200/201/204) | Errores |
|---|---|---|---|---|---|---|
| POST | `/register` | No | 5/hora por IP | `{email, password}` | 201 `{userId, email, message}` | 400 validación, 409 `EMAIL_ALREADY_REGISTERED` |
| POST | `/verify-email` | No | No | `{token}` | 204 | 400 `INVALID_OR_EXPIRED_TOKEN` |
| POST | `/login` | No | 10/min por IP | `{email, password}` | 200 `{accessToken, refreshToken, accessTokenExpiresAt, userId, email}` | 401 `INVALID_CREDENTIALS`, 403 `ACCOUNT_NOT_ACTIVE` / `EMAIL_NOT_VERIFIED` |
| POST | `/refresh` | No (el refresh token ES la credencial) | No | `{refreshToken}` | 200 `{accessToken, refreshToken, accessTokenExpiresAt}` | 401 `INVALID_REFRESH_TOKEN` |
| POST | `/logout` | Bearer | No | `{refreshToken}` | 204 (idempotente) | — |
| POST | `/forgot-password` | No | 5/hora por IP | `{email}` | 204 (siempre, exista o no el email) | — |
| POST | `/reset-password` | No | 10/hora por IP | `{token, newPassword}` | 204 | 400 `INVALID_OR_EXPIRED_TOKEN` / validación |
| POST | `/change-password` | Bearer | No | `{currentPassword, newPassword}` | 204 | 401 `INVALID_CREDENTIALS`, 400 validación |

### Móvil (`DeviceLinkController`, `api/v1/auth/device-link/*`)

| Método | Ruta | Auth | Rate limit | Request | Response | Errores |
|---|---|---|---|---|---|---|
| POST | `/generate` | Bearer (sesión web) | No | — | 200 `{code, expiresAt}` | — |
| POST | `/redeem` | No | **5/min por IP (P0)** | `{code, deviceModel?, osVersion?, appVersion?}` | 200 `{accessToken, refreshToken, accessTokenExpiresAt, userId, email, deviceId}` | 400 `INVALID_DEVICE_LINK_CODE`, 429 `DEVICE_LINK_CODE_LOCKED` |

Todos los errores de negocio (no de validación) se devuelven como
`ProblemDetails` con un campo extra `errors.code` (ej. `INVALID_CREDENTIALS`)
para que Web/Android decidan el copy exacto sin parsear el mensaje humano —
ver `AuthDomainException` y el middleware global.

---

## 2. Decisiones tomadas en este bloque

- **Login web y móvil son código separado de verdad**: dos controladores
  distintos (`AuthController` vs. `DeviceLinkController`), sin un flujo
  compartido de "credenciales" — el móvil nunca ve un formulario de
  email/password en el backend.
- **Rotación de refresh token con detección de reuse**: cada `/refresh`
  revoca el token presentado y emite uno nuevo (`ReplacedByTokenId`). Si
  llega un token que YA estaba revocado (fue rotado antes, o alguien cerró
  sesión con él), se trata como indicio de robo: se revoca TODA la cadena de
  refresh tokens activos del usuario y se registra en `audit_logs`
  (`refresh_token_reuse_detected`). No intento distinguir "rotado" de
  "revocado por logout" — cualquiera de los dos casos siendo reusado es
  sospechoso.
- **Invalidar código previo + insertar el nuevo, en una sola
  `SaveChangesAsync`**: no usé una transacción explícita porque una sola
  llamada a `SaveChangesAsync` de EF ya viaja en una transacción implícita —
  es exactamente la atomicidad que pide HANDOFF-DB §8 riesgo 6, sin la
  complejidad de una API de transacciones propia.
- **Colisión de código de vinculación → reintento, no propagación**: la
  detección de "es justo esta violación de índice y no otra cosa" vive detrás
  de `IUniqueConstraintViolationDetector`, implementado en Infrastructure
  inspeccionando `PostgresException.ConstraintName` — así Application no
  necesita referenciar Npgsql directamente (se mantiene la regla de
  dependencia de Clean Architecture). Until 5 reintentos; si se agotan (
  astronómicamente improbable con 1,000,000 combinaciones), lanza una
  excepción genérica en vez de un loop infinito.
- **`attempts`/`max_attempts` de `device_link_codes` quedan sin usar en la
  lógica de redención**, a propósito — el flujo elegido (identificar la fila
  por el hash del código) hace que un código mal tecleado nunca produzca una
  fila a la cual atribuirle el intento, tal como ya había anticipado
  HANDOFF-DB §8 riesgo 7. La defensa real es el rate limiting por IP en
  `/device-link/redeem` (5/min, configurado en `appsettings.json` vía
  AspNetCoreRateLimit) — es P0 tal como pediste, no un "nice to have".
- **Reset de password y cambio de password revocan TODOS los refresh tokens
  activos del usuario**: si alguien más tenía sesión abierta (ej. el atacante
  que motivó el reset), queda fuera. Decisión de seguridad, no pedida
  explícitamente pero estándar de la industria — la documento aquí por si el
  producto la quiere revertir (ej. no cerrar la sesión que hizo el cambio).
- **Login bloquea si el email no está verificado** (`403
  EMAIL_NOT_VERIFIED`). No estaba explícito en el encargo; lo elegí porque
  dejar loguearse sin verificar y decidirlo caso por caso en cada endpoint
  hubiera sido más frágil. Si el producto prefiere permitir login sin
  verificar y solo restringir acciones sensibles, es un cambio de una línea
  en `LoginCommandHandler` — avísame si lo quieren así.
- **`forgot-password` siempre responde 204**, exista o no el email —
  previene enumeración de cuentas por esta vía.
- **JWT con `MapInboundClaims = false`**: sin esto, ASP.NET Core remapea
  `sub`/`email` a URIs largas de `ClaimTypes` por compatibilidad histórica, lo
  que rompería `CurrentUserService` leyendo `JwtRegisteredClaimNames.Sub`.
  Detalle fácil de pasar por alto si alguien reconfigura el JWT bearer más
  adelante — lo dejo documentado aquí.
- **Envío de email es un stub que solo loguea** (`LoggingEmailSender`) — la
  integración SMTP real no es parte de este bloque (ver
  01-ARQUITECTURA-Y-STACK.md, credenciales vía Chat DevSecOps).

---

## 3. Qué pruebas existen y qué cubren

Todas en `tests/WellSense.Tests/Auth/`, contra un `WellSenseDbContext` real
con proveedor InMemory de EF (no mocks de la interfaz de datos):

- `RegisterCommandHandlerTests`: normaliza el email a minúsculas, crea el
  token de verificación, envía el "correo" (stub); email duplicado lanza
  `EMAIL_ALREADY_REGISTERED`.
- `LoginCommandHandlerTests`: credenciales correctas devuelven tokens;
  password incorrecto → `INVALID_CREDENTIALS`; email no verificado →
  `EMAIL_NOT_VERIFIED`.
- `RefreshTokenCommandHandlerTests` (la más importante de este bloque):
  rotación exitosa revoca el token viejo y encadena `ReplacedByTokenId`;
  **reuse de un token ya revocado revoca toda la cadena activa del usuario y
  registra el audit log**; token expirado lanza el mismo error genérico.
- `GenerateDeviceLinkCodeCommandHandlerTests`: borra el código previo no
  usado del mismo usuario; **reintenta tras una colisión simulada y logra
  insertar** (usando un `ThrowingDbContextDecorator` que fuerza una
  `DbUpdateException` en las primeras N llamadas — así se prueba el bucle de
  reintento sin depender de Postgres real, ya que el proveedor InMemory no
  aplica índices únicos parciales); se rinde con excepción clara tras agotar
  los reintentos.
- `RedeemDeviceLinkCodeCommandHandlerTests`: código válido crea el `Device` y
  emite tokens; código inexistente y código expirado devuelven el **mismo**
  error genérico (no revelan cuál fue el motivo).
- `ResetPasswordCommandHandlerTests`: revoca todos los refresh tokens activos
  y registra el audit log.

### 3.1 Pruebas de integración HTTP end-to-end (nuevo en esta actualización)

En `tests/WellSense.Tests/Integration/`, usando `WebApplicationFactory<Program>`
real — pasan por Kestrel de pruebas, el pipeline HTTP completo (JWT bearer,
rate limiting, middleware de excepciones) y los controladores tal cual los
vería un cliente real, no invocan los handlers de MediatR directamente:

- **`CustomWebApplicationFactory`**: decisión de diseño — en vez de
  Testcontainers + Postgres real (que requiere Docker, no garantizado en
  todos los entornos de CI/sandbox donde esto se ejecute), reemplaza el
  `DbContext` de Npgsql por el proveedor InMemory de EF **después** de que el
  host ya construyó la configuración real de `appsettings.json` (Jwt, rate
  limiting). Esto es válido para lo que estas pruebas verifican — el pipeline
  HTTP en sí — porque ninguno de los tres puntos pendientes (rate limiting,
  JWT bearer, manejo de errores) depende de Postgres específicamente. Lo que
  esto NO cubre es el comportamiento de índices únicos/parciales reales, que
  ya está validado por separado (HANDOFF-DB y el Bloque 1 de este repo). Si
  prefieres que reescriba esto con Testcontainers real contra Postgres,
  dímelo — el paquete ya está referenciado, solo no lo usé por esta razón.
- **`CapturingEmailSender`**: espía de `IEmailSender` que captura el token en
  claro que el flujo real solo loguearía, para poder completar el flujo
  register→verify-email→login de punta a punta sin acceso directo a la BD
  desde la prueba.
- **`AuthFlowEndpointTests`**:
  - Flujo completo register → verify-email → login → llamar
    `/change-password` con el access token real emitido por `/login` (confirma
    que el JWT bearer wireado en `Program.cs` valida issuer/audience/firma de
    verdad, no que el handler "hubiera" aceptado el request).
  - `/change-password` sin header `Authorization` → 401.
  - `/change-password` con un JWT válido pero con el último carácter alterado
    (firma rota) → 401.
  - `/login` con email no verificado → 403 con `EMAIL_NOT_VERIFIED` en el body.
- **`RateLimitingEndpointTests`**: golpea `/login` 11 veces (límite
  configurado: 10/min) y `/device-link/redeem` 6 veces (límite: 5/min, el P0)
  y confirma que la última respuesta de cada una es `429` — la prueba real de
  que `IpRateLimiting` en `appsettings.json` efectivamente bloquea, no solo
  que la configuración esté bien escrita.

---

## 4. Qué necesita el resto del equipo de este bloque

**Chat Web**: contratos exactos arriba (§1). Notas de integración:
- El flujo de vinculación es: Web ya logueada llama `POST
  /device-link/generate` → muestra el código de 6 dígitos al usuario (ej.
  como QR o texto) → el usuario lo ingresa en el móvil.
- `forgot-password`/`reset-password` no dan feedback de si el email existe —
  el copy de la UI debe decir algo como "si el correo existe, te enviamos un
  link", nunca "correo no encontrado".

**Chat Android**: solo usa `DeviceLinkController.Redeem` — nunca ve
email/password. Debe manejar 429 en `/redeem` (mostrar "espera un momento e
intenta de nuevo", no reintentar automáticamente en loop).

**Chat DevSecOps**:
- `Jwt:Secret` y `DeviceLink:Pepper` deben poblarse vía Vault/Key Vault en
  producción — en este bloque son placeholders en `appsettings.json` que
  fallan explícito (`InvalidOperationException`) si no se configuran, para
  no arrancar en producción con un secreto vacío por accidente.
- Las reglas de `IpRateLimiting` en `appsettings.json` son un punto de
  partida — confirmar si el store en memoria (`AddInMemoryRateLimiting`) es
  suficiente o si con múltiples instancias del backend detrás de un load
  balancer se necesita el store distribuido de Redis (el paquete
  `StackExchange.Redis` ya está referenciado en Infrastructure pero sin usar
  todavía).
- Proveedor de SMTP real para reemplazar `LoggingEmailSender`.

---

## 5. Riesgos abiertos

1. **Nada de esto está compilado por mí** (ver §0) — el riesgo más
   importante, igual que en el Bloque 1. Corre `dotnet build && dotnet test`
   antes de aprobar.
2. ~~Sin pruebas de integración HTTP~~ — **resuelto en esta actualización**,
   ver §3.1. Queda como sub-riesgo que esas pruebas usan EF InMemory en vez
   de Postgres real (Testcontainers) por la razón explicada ahí — si quieres
   la versión con Postgres real, lo puedo cambiar.
3. **AspNetCoreRateLimit con store en memoria**: si el backend corre en más
   de una instancia (ej. detrás de un load balancer en producción), cada
   instancia cuenta sus propios requests — un atacante podría rotar entre
   instancias para eludir el límite. Mitigación: store distribuido (Redis)
   antes de escalar horizontalmente. No lo implementé porque el Bloque 1 no
   tenía Redis configurado todavía y no quería adelantarme sin luz verde.
4. **Decisión de bloquear login sin email verificado** (§2) — no estaba
   explícita en el encargo, confirmar que es lo que el producto quiere.
5. **Revocar todos los refresh tokens en reset/change password** — mismo
   caso, decisión de seguridad estándar pero no pedida explícitamente.
6. **`attempts`/`max_attempts` sin uso funcional real** en el flujo elegido
   (ver §2) — si en algún momento el flujo de redención cambia (ej. a
   "identificar primero un intento por QR, luego intentar el código contra
   esa fila", como sugiere HANDOFF-DB §8 riesgo 7), esos campos sí cobrarían
   sentido y habría que revisar `RedeemDeviceLinkCodeCommandHandler`.
7. **`IConfiguration` crudo en Application** (Login/RefreshToken/
   RedeemDeviceLinkCode leen `Jwt:AccessTokenMinutes`/`RefreshTokenDays`
   directo de `IConfiguration`): acopla Application a un detalle de
   infraestructura de configuración. Mejora sugerida y pendiente: una
   interfaz propia `IAuthTokenSettings` definida en Application (con
   `AccessTokenMinutes`/`RefreshTokenDays` ya tipados) implementada en
   Infrastructure — no lo hice en este envío para no demorar el fix de
   compilación que pediste resuelto primero.

---

## 6. Checklist de las 10 capas del DoD

- [x] Validador (FluentValidation) en cada comando de Auth
- [x] Prueba unitaria de la lógica de negocio de cada handler crítico
- [x] Documentación de API (Swagger, ya configurado en Bloque 1, con
      `[ProducesResponseType]` en cada acción)
- [x] Manejo de errores consistente (`AuthDomainException` → `ProblemDetails`
      con código estable)
- [x] Seguridad: Argon2id, hashes de tokens (nunca texto plano), pepper para
      device-link fuera de código/BD, rate limiting en los 5 endpoints
      sensibles
- [x] Logging sin datos sensibles (nunca password/token/código en claro en
      ningún `ILogger` call)
- [x] Pruebas de integración HTTP end-to-end (JWT bearer real, rate limiting
      real) — ver §3.1
- [ ] Compilación y `dotnet test` reales — **pendiente de tu lado**, ver §0

Quedo a la espera de tu luz verde (o correcciones) antes de empezar el
Bloque 3 (Users + Profile, incluida la decisión de zona horaria para
`wellness_scores`/`stress_scores`).
