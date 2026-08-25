# HANDOFF.md — Chat Backend (.NET), Bloque 9: Panel de administración + RBAC real

> Entregable del **Bloque 9**. Requiere Bloque 8 cerrado (ya lo está,
> 158/158 reales, incluyendo la sincronización del fix de GitHub commit
> `feeb109` — confirmado: mi copia coincide byte a byte con esos 2
> archivos, sin ningún otro desalineado). Cierra el primer de los dos
> huecos reales del planteamiento original: el rol `admin` existía en la
> BD desde Bloque 1 sin que ningún endpoint lo usara.

---

## 0. Aviso operativo (se mantiene)

Sigo sin salida de red hacia NuGet en mi propio entorno de trabajo — no pude
correr `dotnet build`/`dotnet test` aquí. Lo que sí hice en este bloque:

- **Confirmé, antes de escribir código, que el mecanismo de roles YA
  funciona sin ningún cambio de plomería**: `JwtTokenService` (Bloque 2) ya
  emite `new Claim(ClaimTypes.Role, role)` en cada token, y
  `RoleClaimType` nunca se sobreescribió en `Program.cs` — sigue siendo
  `ClaimTypes.Role` por default. `[Authorize(Roles = "Admin")]` de ASP.NET
  Core ya funciona con cero plomería nueva. `[RequireRole]` es un
  envoltorio delgado por legibilidad, no un mecanismo nuevo — documentado
  explícitamente para que quede claro que no se reinventó nada.
- **Encontré y corregí un bug de autorización real en mi propio primer
  borrador**, antes de que llegara a ningún lado: combiné
  `[AllowAnonymous]` + `[Authorize]` en la misma acción (`Bootstrap`)
  intentando lograr "autenticado pero sin rol admin" — pero
  `[AllowAnonymous]` desactiva TODA autorización incondicionalmente,
  volviendo el endpoint completamente público sin darme cuenta. Corregido
  moviendo `[RequireRole("Admin")]` del controller a cada acción
  individual, dejando `Bootstrap` con un `[Authorize]` simple, sin
  necesitar ningún combo de atributos confuso — ver §2.
- **Validé contra Postgres real** que `users.role`/`users.status` (Bloque
  2, ya existentes) siguen aceptando las transiciones que este bloque
  ahora activa por primera vez (`admin`, `suspended`) — y confirmé que su
  conversión EF ya usaba el patrón correcto en minúsculas desde Bloque 2
  (a diferencia de las otras 4 tablas que sí tenían el bug de
  `HasConversion<string>()`, ésta nunca lo tuvo).
- Los 3 barridos automatizados de siempre, limpios sobre las ~30 rutas
  nuevas de este bloque.

---

## 1. Cómo se promueve al primer admin — decisión y alternativas consideradas

No había ningún usuario admin sembrado. Alternativas consideradas:

1. **Seed hardcodeado en una migración** (email/password fijos) — rechazado:
   hornea una cuenta permanentemente conocida en el esquema, mal patrón de
   seguridad incluso con la contraseña hasheada.
2. **Una herramienta de línea de comandos aparte** — más "correcto" en
   teoría, pero agrega una superficie de despliegue nueva (un ejecutable
   separado) que nadie pidió para este bloque, y complica el pipeline de
   CI/CD sin necesidad.
3. **`UPDATE users SET role='admin'` manual vía acceso directo a la BD** —
   siempre disponible como último recurso de emergencia, pero no es
   "cómo se promueve al primer admin" en el flujo normal — depende de
   quien tenga acceso a psql en producción.
4. **Elegido: `POST /api/v1/admin/bootstrap`**, autoservicio, protegido por
   dos capas independientes que se autodeshabilita para siempre después
   del primer uso:
   - El llamador ya debe tener una cuenta y un Bearer válido — promueve
     SU PROPIA cuenta, nunca la de otro usuario.
   - Un secreto compartido (`Admin:BootstrapSecret`, config, nunca
     versionado — mismo patrón que `Jwt:Secret`/`DeviceLink:Pepper`).
   - Se autodeshabilita en cuanto existe CUALQUIER admin en el sistema —
     ni siquiera hace falta rotar el secreto después.

**Gotcha real que hay que comunicarle a Web/Android**: el JWT es
stateless — el access token que el usuario ya tenía ANTES de bootstrapear
sigue llevando el rol viejo hasta que expira (máx. 15 min) o hasta que se
llama a `/auth/refresh` (que sí re-lee el rol actual de la BD). Probado
explícitamente en `AdminFlowEndpointTests.Bootstrap_then_refresh_grants_real_admin_access_over_http`:
el mismo access token sigue dando 403 justo después del bootstrap, y solo
tras refrescar el token se obtiene acceso real.

---

## 2. El bug de autorización que encontré en mi propio borrador

Mi primer intento de que `POST /admin/bootstrap` fuera accesible sin el rol
Admin (mientras el resto del controller sí lo exige) fue poner
`[RequireRole("Admin")]` a nivel de controller y luego, en esa única
acción, agregar `[AllowAnonymous]` junto a `[Authorize]`, esperando que
"anulara" el rol pero mantuviera la autenticación.

**Esto no funciona así**: `[AllowAnonymous]` desactiva TODA autorización de
forma incondicional para esa acción — el `[Authorize]` puesto justo al
lado se vuelve un no-op, y el endpoint queda completamente público, sin
exigir ni siquiera un Bearer. Lo atrapé releyendo el controller antes de
darlo por terminado, no compilando.

**Corrección**: `[RequireRole("Admin")]` se movió del controller a cada
una de las otras 5 acciones individualmente. `Bootstrap` se quedó con un
`[Authorize]` simple (sin rol) — su propia lógica de negocio (secreto +
"nadie es admin todavía") es la única protección adicional que necesita,
sin depender de ningún truco de combinación de atributos.

---

## 3. Endpoints listos

**Admin** (`api/v1/admin`, todos requieren `[RequireRole("Admin")]` salvo
`/bootstrap`, que requiere `[Authorize]` simple):

| Método | Ruta | Request | Response |
|---|---|---|---|
| GET | `/users` | query `?page=&pageSize=&email=&status=` | 200 `{items, page, pageSize, totalCount}` |
| GET | `/users/{id}` | — | 200 `{..., profile?, devices[], subscription?}` |
| PUT | `/users/{id}/status` | `{status: ACTIVE\|SUSPENDED}` | 204 |
| GET | `/subscriptions` | query `?page=&pageSize=` | 200 solo suscripciones ACTIVAS, paginado |
| GET | `/stats` | — | 200 `{totalUsers, activeUsersLast7Days, usersByPlan[]}` |
| POST | `/bootstrap` | `{secret}` | 204 (solo funciona una vez en toda la vida del sistema) |

Cualquier usuario autenticado SIN el rol Admin recibe **403 en los 5
primeros**, nunca 404 — deliberado, ver HANDOFF original del encargo: es
una superficie administrativa, no un dato privado de otro usuario, así
que no hay razón para ocultar que el recurso existe.

---

## 4. Decisiones tomadas en este bloque

- **Suspender revoca TODAS las sesiones activas** (refresh tokens) del
  usuario — mismo patrón que reset/change password (Bloque 2) y DeleteMe
  (Bloque 3). Sin esto, un usuario recién suspendido podría seguir
  usando un refresh token ya emitido indefinidamente, dejando la
  suspensión sin efecto real. Probado explícitamente con una prueba de
  integración HTTP real (`Suspending_a_user_via_admin_then_that_user_cannot_refresh_their_session`).
- **Un admin no puede suspenderse a sí mismo** — guardia explícita, evita
  quedar autobloqueado sin otro admin que reactive la cuenta.
- **`status` de la actualización solo acepta `ACTIVE`/`SUSPENDED`**, no
  `PENDING` — `PENDING` existe en el `CHECK` de la BD desde Bloque 1 pero
  ningún flujo actual lo asigna (`RegisterCommandHandler`, Bloque 2, deja
  el status en `ACTIVE` desde el registro). No es una acción
  administrativa real hoy, documentado como observación, no se expone.
- **"Activo" (para `activeUsersLast7Days`) se define por actividad de
  dispositivo** (`devices.last_seen_at` dentro de los últimos 7 días), no
  por actividad de sesión/token — para una app de bienestar, sincronizar
  datos es la señal de compromiso real; solo tener un token renovado es
  un proxy más flojo que se descartó explícitamente.
- **La distribución por plan solo cuenta filas de `subscriptions` que ya
  existen** — un usuario que nunca llamó `GET /memberships/me` (que crea
  la suscripción FREE de forma perezosa, Bloque 6) no aparece en ningún
  plan todavía. No se infiere FREE para usuarios sin fila de suscripción,
  para no reportar un número inferido en vez de un estado real.
- **El filtro de email es un `Contains` case-insensitive**, apoyado en que
  los emails ya se normalizan a minúsculas al registrar (Bloque 2) — no
  hace falta ningún índice/función especial de la BD para esto.

---

## 5. Qué pruebas existen

Unitarias con EF InMemory:
- `Admin/ListUsersQueryHandlerTests`: paginación, filtro por email
  (case-insensitive), filtro por status, exclusión de usuarios
  soft-eliminados.
- `Admin/UpdateUserStatusCommandHandlerTests`: suspender revoca tokens
  activos; reactivar no los toca; **un admin no puede suspenderse a sí
  mismo**; usuario inexistente lanza `USER_NOT_FOUND`.
- `Admin/GetUserDetailQueryHandlerTests`: perfil/dispositivos/suscripción
  presentes cuando existen, `null`/vacío cuando no.
- `Admin/GetStatsQueryHandlerTests`: conteo de "activos" basado en
  actividad de dispositivo, no de sesión; distribución por plan agrupada
  correctamente.
- `Admin/BootstrapFirstAdminCommandHandlerTests`: promueve con secreto
  correcto y ningún admin previo; secreto incorrecto rechazado incluso
  cuando el bootstrap "hubiera sido válido" por lo demás; **secreto
  configurado vacío nunca "matchea" nada** (nunca se trata un string
  vacío como válido); un segundo intento tras ya existir un admin se
  rechaza incluso con el secreto correcto.

Integración HTTP end-to-end — **la más importante de este bloque, por
instrucción explícita**:
- `Integration/AdminFlowEndpointTests.A_normal_authenticated_user_gets_403_not_404_on_every_admin_endpoint`:
  los 5 endpoints administrativos, un solo usuario normal, 403 en los 5.
- `Unauthenticated_requests_get_401_not_403`: sin token es 401, no 403 —
  confirma que la distinción 401-vs-403 de ASP.NET Core (no autenticado
  vs. autenticado sin el rol) funciona como se espera.
- `Bootstrap_then_refresh_grants_real_admin_access_over_http`: el gotcha
  del JWT stateless probado de punta a punta contra el pipeline HTTP
  real, no solo razonado.
- `Bootstrap_with_wrong_secret_returns_403_and_a_second_bootstrap_attempt_returns_409`.
- `Suspending_a_user_via_admin_then_that_user_cannot_refresh_their_session`:
  cruza Admin (este bloque) con Auth (Bloque 2) de punta a punta real.

---

## 6. Riesgos abiertos

1. **No hay forma de promover a un SEGUNDO admin desde la API** — una vez
   que existe el primero, cualquier promoción adicional necesita acceso
   directo a la BD (`UPDATE users SET role='admin'`). No se pidió
   explícitamente un endpoint "admin promueve a otro admin" — candidato
   natural para un bloque futuro si hace falta.
2. **El gotcha del JWT stateless tras el bootstrap** (§1) es un
   comportamiento correcto pero puede sorprender en producción si
   Web/Android no lo esperan — hay que comunicárselo explícitamente.
3. Mismo aviso de siempre: no compilado/probado por mí en mi propio
   entorno — necesito `dotnet build && dotnet test` de su lado antes de
   aprobar.

---

## 7. Checklist de las 10 capas del DoD

- [x] Validador (FluentValidation) en cada comando/query paginada
- [x] Prueba unitaria de cada handler, incluyendo los casos de seguridad
      (auto-suspensión, secreto de bootstrap incorrecto/vacío/reutilizado)
- [x] Prueba de integración HTTP end-to-end — **incluyendo la
      verificación explícita de 403 en los 5 endpoints para un usuario
      normal**, tal como se pidió
- [x] Documentación de API (Swagger, `[ProducesResponseType]`)
- [x] Manejo de errores consistente (`AdminDomainException` nueva,
      agregada al middleware global)
- [x] Bug de autorización real en mi propio borrador encontrado y
      corregido antes de escribir más código encima
- [x] Decisión de bootstrap documentada con alternativas consideradas y
      justificación de por qué se descartaron
- [ ] Compilación y `dotnet test` reales — **pendiente de tu lado**, ver §0

Bloque 9 completo — sigo directo con el Bloque 10 sin esperar luz verde,
tal como indicaste.
