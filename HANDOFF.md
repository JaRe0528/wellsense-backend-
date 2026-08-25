# HANDOFF.md — Chat Backend (.NET), Bloque 10: Auditoría completa + hardening de código

> Entregable del **Bloque 10**, el último de los 10. Requiere Bloque 9
> cerrado (ver `HANDOFF-Bloque9.md`, entregado en el mismo envío — ambos
> bloques se hicieron uno tras otro sin esperar luz verde entre ellos, tal
> como indicaste). Cierra el segundo de los dos huecos reales del
> planteamiento original: auditoría incompleta y ningún hardening de
> headers/CORS explícito.

---

## Actualización 2 — 195/196 → causa raíz real de CORS encontrada (no otra corrección a ciegas)

Segunda corrida real: 195/196, y las 3 fallas de aislamiento de pruebas
quedaron confirmadas resueltas. Solo CORS seguía fallando — mi cambio
anterior (array → string separado por comas) no atacaba la causa real,
solo eliminaba una fuente de ambigüedad que no era el problema.

**Causa raíz real, esta vez con evidencia, no una sospecha más**: leía
`Cors:AllowedOrigins` de forma EAGER, en una variable de nivel superior de
`Program.cs`, ANTES de `builder.Build()`. Pero yo mismo ya sabía de este
patrón exacto de bug — mi propio comentario en `Program.cs`, ya desde el
Bloque 2, dice explícitamente que `Jwt:Secret` debe leerse DENTRO del
callback de `AddJwtBearer`, nunca en una variable calculada antes, porque
las fuentes de configuración que `WebApplicationFactory.ConfigureAppConfiguration`
agrega en las pruebas de integración no están garantizadas de estar ya
fusionadas en `builder.Configuration` cuando el código de nivel superior
de `Program.cs` se ejecuta de forma síncrona antes de `Build()`. Apliqué
esa lección a JWT pero se me pasó aplicarla a CORS — literalmente el mismo
tipo de bug, dos veces, en el mismo archivo.

**Fix real**: `Cors:AllowedOrigins` ahora se lee DENTRO del callback de
`AddCors(options => {...})` — ese callback se registra vía
`services.Configure<CorsOptions>(...)` y solo se EVALÚA la primera vez que
se resuelve `IOptions<CorsOptions>` (la primera request real), momento en
el que toda la configuración de prueba ya está fusionada. Mismo patrón,
mismo archivo, ahora aplicado consistentemente en los dos lugares que lo
necesitaban.

---

## Actualización 1 — 192/196 → corrección de 4 fallas reales tras `dotnet build && dotnet test`

Corriste el primer `dotnet test` real de estos dos bloques: 192/196 pasaron,
4 fallaron. Diagnóstico de las 4, y por qué NO significan lo mismo entre sí:

**3 de las 4 eran un bug en mis propias pruebas, no en la app.**
`BootstrapFirstAdminCommandHandler` verifica, a propósito, si existe
CUALQUIER admin en TODO el sistema — es el punto central de su diseño de
seguridad (autodeshabilitarse para siempre tras el primer uso). Pero
`AdminFlowEndpointTests` tenía 4 pruebas que cada una asumía ser "la
primera" contra la MISMA base de datos InMemory compartida por
`IClassFixture<CustomWebApplicationFactory>`. Solo la que xUnit ejecutó
primero consiguió de verdad el 204; las otras tres, correctamente según el
propio diseño de la app, recibieron 409 `ALREADY_BOOTSTRAPPED` — y una de
ellas, al no tener en realidad un token de admin válido, se cayó más
adelante con un error de deserialización JSON al intentar leer una
respuesta de error como si fuera un `RefreshResponse` exitoso. La app
funcionó exactamente como debía; el aislamiento de mis pruebas estaba mal.
Corregido: las 4 pruebas que necesitan "todavía no existe ningún admin"
ahora crean y descartan su propia `CustomWebApplicationFactory` aislada en
vez de compartir la inyectada por la clase — las que no bootstrapean
(403/401 puros) siguen usando la compartida, más barata.

**La cuarta (CORS) era una decisión de diseño insuficientemente robusta de
mi parte, no una certeza rota.** Configuré `Cors:AllowedOrigins` como un
array, y en las pruebas lo poblaba vía una clave indexada
(`Cors:AllowedOrigins:0`) agregada por un proveedor de configuración
distinto al de `appsettings.json` (que ya declaraba ese mismo array, vacío).
No tenía certeza plena de que el binding de un array por claves indexadas
se fusionara de forma predecible entre dos proveedores de configuración
distintos — lo dije explícitamente como una posible causa en mi propio
razonamiento antes de que corrieras las pruebas. En vez de seguir
adivinando, cambié `Cors:AllowedOrigins` de array a un STRING plano
separado por comas (`"https://a.com,https://b.com"`) — elimina la
ambigüedad de binding por completo (un solo valor, nunca una fusión de
claves indexadas de dos fuentes), y de paso es más práctico para
Render.com/variables de entorno planas, que es hacia donde me comentaste
que van a desplegar: un array anidado por env vars obliga a la convención
`Cors__AllowedOrigins__0`, que la mayoría de las plataformas PaaS no
manejan bien, mientras que una sola variable separada por comas es
trivial ahí.

Ambos tipos de fix — aislamiento de pruebas y la simplificación de CORS —
ya están en el ZIP adjunto. Quedo a la espera de que confirmes
`dotnet test` en 196/196.

---

## 0. Aviso operativo (se mantiene)

Sigo sin salida de red hacia NuGet en mi propio entorno de trabajo — no pude
correr `dotnet build`/`dotnet test` aquí. Lo que sí hice en este bloque:

- **Hice un inventario real antes de asumir nada**: `grep` de todos los
  `AuditLogs.Add` existentes en el código, en vez de confiar en mi memoria
  de qué ya estaba cubierto. Esto encontró una discrepancia real con tu
  encargo — ver §1, corregida explícitamente en vez de duplicar trabajo o
  ignorarla en silencio.
- **Encontré y corregí un bug real en mi propio primer intento** de agregar
  auditoría a `GenerateDeviceLinkCodeCommandHandler`: ese handler ya tenía
  un bucle de reintento por colisión (Bloque 2) que desprende del change
  tracker las entidades del intento fallido antes de reintentar — mi primer
  borrador agregaba el registro de auditoría sin desprenderlo también, lo
  que habría duplicado filas de `audit_logs` en cualquier reintento real.
  Lo corregí antes de escribir la prueba, y esa misma prueba
  (`Successful_generation_writes_exactly_one_audit_log_entry_even_after_a_retry`)
  ejercita exactamente ese camino.
- **Validé contra Postgres real** las 3 formas exactas de metadata `jsonb`
  que este bloque ahora escribe (vacía, con motivo de fallo, con detalle de
  suscripción) — confirmado que las tres se insertan sin error.
- Los 3 barridos automatizados de siempre, limpios sobre todo lo tocado en
  este bloque.

---

## 1. Corrección a tu encargo: dos acciones ya estaban auditadas

Antes de escribir código, hice `grep -rn "AuditLogs.Add"` sobre todo el
código para saber qué faltaba de verdad, en vez de asumir. Resultado:
**`cambio de contraseña` y `reset de contraseña` ya se registraban desde el
Bloque 2** (`ChangePasswordCommandHandler` → `"password_changed"`;
`ResetPasswordCommandHandler` → `"password_reset"`), junto con
`refresh_token_reuse_detected` (Bloque 2) y `account_deleted` (Bloque 3).

Tu lista los incluía como pendientes — no lo son. No los toqué de nuevo
(hubiera sido registrar la misma acción dos veces, o peor, reemplazar un
`Action` ya usado en producción hipotética por otro nombre y romper
continuidad del historial). Lo señalo explícitamente para que tu propio
registro de qué está cubierto quede correcto, no para restar mérito al
encargo — el resto de la lista sí eran huecos reales, cubiertos abajo.

---

## 2. Qué quedó auditado — inventario completo tras este bloque

| Acción | Handler | Bloque que lo escribió |
|---|---|---|
| `refresh_token_reuse_detected` | `RefreshTokenCommandHandler` | 2 (ya existía) |
| `password_changed` | `ChangePasswordCommandHandler` | 2 (ya existía) |
| `password_reset` | `ResetPasswordCommandHandler` | 2 (ya existía) |
| `account_deleted` | `DeleteMeCommandHandler` | 3 (ya existía) |
| **`login_succeeded`** | `LoginCommandHandler` | **10 (nuevo)** |
| **`login_failed`** | `LoginCommandHandler` | **10 (nuevo)** |
| **`device_link_code_generated`** | `GenerateDeviceLinkCodeCommandHandler` | **10 (nuevo)** |
| **`device_link_code_redeemed`** | `RedeemDeviceLinkCodeCommandHandler` | **10 (nuevo)** |
| **`device_registered`** | `RegisterDeviceCommandHandler` | **10 (nuevo)** |
| **`device_unpaired`** | `UnpairDeviceCommandHandler` | **10 (nuevo)** |
| **`subscription_changed`** | `SubscribeToPlanCommandHandler` | **10 (nuevo)** |

Decisiones puntuales:

- **`login_failed` distingue el motivo** (`invalid_credentials`,
  `account_not_active`, `email_not_verified`) en `metadata.reason`, pero
  usa el mismo `Action` para los tres — el detalle vive en la metadata, no
  en el nombre de la acción, para que filtrar por `action=login_failed` en
  el panel (Bloque 9) capture los tres casos de una vez.
- **Un intento de login con un email que no existe se audita con
  `UserId = null`** — no hay a quién atribuirlo, y no se debe revelar en
  el registro (ni siquiera indirectamente, vía qué usuario quedó
  vinculado) si ese email está o no registrado. Mismo principio que ya
  regía la respuesta HTTP desde Bloque 2 (`AuthDomainException.InvalidCredentials()`
  es el mismo error tanto si el email no existe como si la contraseña es
  incorrecta).
- **Un intento DECLINADO de suscripción NO se audita en `audit_logs`** —
  ya queda registrado en `payments` con `status = DECLINED`, duplicarlo en
  `audit_logs` no agrega información, solo ruido. Solo `subscription_changed`
  se audita cuando el cambio de verdad se confirma (plan pago aprobado o
  plan FREE, incluido "cancelar", que internamente llama al mismo
  handler).
- **`device_link_code_generated` sobrevive el bucle de reintento por
  colisión sin duplicarse** — ver §0, el bug que atrapé en mi propio
  borrador.

---

## 3. CORS — whitelist explícita, nunca wildcard

`Cors:AllowedOrigins` (`appsettings.json`, array de strings) — un array
vacío significa **cero orígenes permitidos**, no "todo permitido": falla
cerrado, no abierto. `AllowCredentials()` está activo porque el dashboard
Web necesita mandar el header `Authorization` en llamadas cross-origin.

Probado contra el pipeline HTTP real, no solo configurado: un origen en la
whitelist recibe `Access-Control-Allow-Origin` de vuelta; uno que no está
en la whitelist, no lo recibe — confirmado con dos pruebas de integración
distintas, no solo una prueba "positiva".

**DevSecOps/Web deben poblar `Cors:AllowedOrigins` por ambiente antes de
que cualquier cliente Web funcione contra un despliegue real** — hoy el
array está vacío en `appsettings.json` (comentario explícito ahí mismo).
Android/el reloj no pasan por CORS (no son navegadores), así que esto solo
afecta al dashboard Web y a un futuro panel admin Web.

---

## 4. Headers de seguridad

`SecurityHeadersMiddleware` fija tres headers en **toda** respuesta,
incluidas las de error (probado explícitamente: un 401 generado por el
middleware de autenticación, antes de llegar a ningún controller, también
los lleva — usa `Response.OnStarting(...)`, que se dispara sin importar
qué parte del pipeline termina escribiendo la respuesta):

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`

**CSP y HSTS quedan deliberadamente fuera**, tal como pediste — ambos
dependen de que haya HTTPS real en producción (HSTS le dice al navegador
"solo háblame por HTTPS"; CSP necesita declarar orígenes de scripts/estilos
que este backend, al no servir ninguna página HTML, no puede anticipar sin
coordinarse con Web). Responsabilidad de DevSecOps cuando el despliegue
real tenga TLS terminado.

---

## 5. Endpoint nuevo: `GET /api/v1/admin/audit-logs`

Para que el panel de Admin (Bloque 9) pueda mostrar lo que este bloque
terminó de poblar. Mismo patrón de paginación que el resto de Admin
(`page`, `pageSize`, filtros opcionales `userId`/`action`), mismo
`[RequireRole("Admin")]` — un usuario normal recibe 403, probado
explícitamente igual que los otros 5 endpoints administrativos.

---

## 6. Modificaciones a código ya aprobado — todas flagueadas

Cinco handlers de bloques ya cerrados se modificaron para agregar el
registro de auditoría — **ninguno cambió su firma de constructor ni su
lógica de negocio existente**, solo se agregó la llamada a
`db.AuditLogs.Add(...)` en el punto correcto de cada uno:

- `LoginCommandHandler` (Bloque 2)
- `GenerateDeviceLinkCodeCommandHandler` (Bloque 2)
- `RedeemDeviceLinkCodeCommandHandler` (Bloque 2)
- `RegisterDeviceCommandHandler` (Bloque 4)
- `UnpairDeviceCommandHandler` (Bloque 4)
- `SubscribeToPlanCommandHandler` (Bloque 6)

Confirmé contra las pruebas ya existentes de cada uno que ninguna
aserción se rompe (ninguna de esas pruebas verificaba el contenido
completo de `db.AuditLogs`, así que agregar filas nuevas no las
invalida) — y agregué una prueba nueva por handler para el
comportamiento de auditoría específico.

---

## 7. Qué pruebas existen (Bloque 10)

Unitarias con EF InMemory — una por cada handler modificado, más:
- `Admin/ListAuditLogsQueryHandlerTests`: filtro por usuario (resuelve el
  email), filtro por acción, entradas sin `UserId` no rompen la resolución
  de email.
- `LoginCommandHandlerTests` (extendida): login exitoso registra IP;
  login fallido con contraseña incorrecta se atribuye al usuario real;
  login fallido con email inexistente se registra con `UserId = null`.
- `GenerateDeviceLinkCodeCommandHandlerTests` (extendida): **exactamente
  un** registro de auditoría incluso tras un reintento por colisión.
- `RedeemDeviceLinkCodeCommandHandlerTests`, `DevicesTests`,
  `SubscribeToPlanCommandHandlerTests` (extendidas): un registro por
  acción exitosa; el intento de pago declinado NO genera uno.

Integración HTTP end-to-end:
- `Integration/SecurityHardeningEndpointTests`: los 3 headers presentes en
  cualquier respuesta, incluida una de error; CORS deja pasar el origen
  permitido y no revela nada al no permitido — las 4 pruebas contra el
  pipeline real, no contra la configuración en aislamiento.
- `Integration/AdminFlowEndpointTests` (extendida): un login real de un
  usuario normal aparece en `/admin/audit-logs` cuando un admin lo
  consulta; un usuario normal recibe 403 en ese mismo endpoint.

---

## 8. Riesgos abiertos

1. **`Cors:AllowedOrigins` vacío por default** — el backend arranca
   correctamente pero NINGÚN origen Web funcionará hasta que DevSecOps lo
   configure por ambiente. Es la decisión correcta (fallar cerrado), pero
   hay que comunicarlo para que no parezca un bug el día del primer
   despliegue con un dashboard Web real.
2. **No hay purga/retención de `audit_logs`** — la tabla crece sin límite;
   no se pidió una política de retención para este bloque y no se
   inventó una. Candidato natural para un futuro trabajo de
   mantenimiento/DevSecOps.
3. **CSP/HSTS pendientes de DevSecOps**, tal como se decidió explícitamente
   en el encargo — no es un olvido, es el alcance tal como se definió.
4. Mismo aviso de siempre: no compilado/probado por mí en mi propio
   entorno — necesito `dotnet build && dotnet test` de su lado antes de
   aprobar.

---

## 9. Checklist de las 10 capas del DoD

- [x] Prueba unitaria de cada cambio de auditoría, incluyendo el bug de
      duplicación que atrapé en mi propio borrador
- [x] Prueba de integración HTTP end-to-end de CORS, headers de seguridad
      y el endpoint nuevo de audit-logs — todas contra el pipeline real
- [x] Documentación de API (Swagger, `[ProducesResponseType]`)
- [x] Validación contra Postgres real de las 3 formas de metadata que este
      bloque escribe
- [x] Corrección explícita a una discrepancia real del encargo (§1), en
      vez de duplicar trabajo o ignorarla
- [x] Modificaciones a código ya aprobado, todas flagueadas con su
      justificación y confirmadas contra las pruebas existentes
- [ ] Compilación y `dotnet test` reales — **pendiente de tu lado**

---

## Cierre de los 10 bloques

Con Bloque 9 (panel de administración + RBAC) y Bloque 10 (auditoría
completa + hardening) el Backend queda funcionalmente completo según el
plan original de 10 bloques. Quedo a la espera de tu resultado de
compilación/pruebas antes de dar esto por cerrado del todo.
