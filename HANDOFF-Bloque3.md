# HANDOFF.md — Chat Backend (.NET), Bloque 3: Users + Profile

> Entregable del **Bloque 3: Módulo Users + Profile**. Requiere Bloque 2
> cerrado (ya lo está, 22/22 pruebas reales). Incluye la decisión de zona
> horaria para `wellness_scores`/`stress_scores` que quedó pendiente desde
> el Bloque 1.

---

## 0. Aviso operativo (se mantiene)

Sigo sin salida de red hacia NuGet en mi propio entorno de trabajo — no pude
correr `dotnet build`/`dotnet test` aquí. Lo que sí hice, igual que en
bloques anteriores:

- Validé la migración 014 (`profiles.timezone`) contra PostgreSQL 16 real:
  aplicada sobre 001-013 ya migradas, verificado el default `'UTC'` con un
  `INSERT` real, revertida sin dejar residuo, y vuelta a aplicar para dejar
  el estado final limpio.
- Corrí los mismos 3 barridos automatizados que en Bloque 2 sobre **todo**
  el repo (no solo lo nuevo): colisión de nombre de namespace contra
  entidades de Domain, balance de llaves/paréntesis, y tipos-usados-vs-`using`
  declarado. Los tres salieron limpios en la pasada final — y de hecho el
  barrido de colisión de namespaces atrapó un error real que yo mismo había
  cometido a mitad de este bloque (ver §2, decisión sobre el nombre de
  carpeta `Onboarding`) antes de que llegara a compilación.

---

## 1. La decisión pendiente: zona horaria para `wellness_scores`/`stress_scores`

**Decisión: la zona horaria LOCAL del usuario (identificador IANA, ej.
`America/Mexico_City`), no UTC.**

Justificación:

1. `wellness_scores`/`stress_scores` son agregados de "cómo te fue en el
   día" — sueño, pasos, estrés a lo largo del día que la persona vivió
   realmente. Si el "día" se calculara con el corte de medianoche UTC, un
   usuario en Ciudad de México (UTC-6) tendría su día cortado a las 6:00 pm
   hora local — toda su tarde/noche caería en el "día siguiente" desde su
   propia perspectiva. El dashboard diría "tu estrés de ayer fue alto"
   mientras para el usuario todavía es "hoy en la tarde". Confuso e
   incorrecto medio día, todos los días, para cualquier usuario que no esté
   en UTC+0.
2. Es el estándar de facto de la industria: Fitbit, Oura, Whoop, Apple
   Health y Google Fit calculan agregados diarios sobre el día calendario
   local del dispositivo/usuario, no UTC.
3. Costo aceptado: hay que saber la zona horaria del usuario. `profiles` no
   tenía ese dato — se agrega en este bloque (`profiles.timezone`, migración
   014, default `'UTC'` para quien todavía no la configuró — un fallback
   universal seguro que simplemente alinea sus "días" a UTC hasta que su
   cliente envíe la zona real).
4. El DST no rompe esto: convertir un `timestamptz` a una zona IANA
   (`AT TIME ZONE 'America/Mexico_City'`, o `TimeZoneInfo` en .NET) ya
   resuelve correctamente las transiciones de horario de verano — la base
   de datos tzdata que usan tanto Postgres como .NET en Linux ya tiene esas
   reglas, no hay que calcularlas a mano.
5. **Riesgo aceptado y documentado**: un usuario que viaja de zona horaria o
   cambia `profile.timezone` a mitad de mes solo afecta el cálculo de
   `wellness_scores`/`stress_scores` **futuros** — los ya calculados quedan
   con la zona horaria que estaba vigente cuando se generaron. No es un bug,
   es una consecuencia esperable de que la zona horaria no es un dato
   inmutable de un usuario.

**Importante — qué NO se hizo en este bloque**: la implementación del job
que efectivamente calcula `wellness_scores`/`stress_scores` día por día no
es parte de Users+Profile — eso le toca al bloque de ML/Dashboard. Este
bloque solo (a) toma y documenta la decisión, y (b) deja lista la pieza de
esquema/API que ese job va a necesitar (`profiles.timezone`, ya expuesto vía
`GET/PUT /profiles/me`). Cuando llegue ese bloque, la regla que debe seguir
cualquier query que "agrupe por día" es: `(recorded_at AT TIME ZONE
profile.timezone)::date`, nunca `recorded_at::date` a secas (eso sería UTC
por default en Postgres).

---

## 2. Qué quedó armado

### Migraciones
- **014_ProfileTimezone**: `ALTER TABLE profiles ADD COLUMN timezone text
  NOT NULL DEFAULT 'UTC'`. No estaba en el DDL original de HANDOFF-DB
  (001-013) — es una extensión de esquema que propone este bloque. Si en
  algún momento se retoca el diseño maestro de `profiles`, el chat de
  DB/orquestador debería quedar al tanto de este cambio puntual.

### Endpoints listos

**Users** (`api/v1/users`, todos requieren Bearer):

| Método | Ruta | Request | Response | Errores |
|---|---|---|---|---|
| GET | `/me` | — | 200 `{id, email, emailVerified, role, status, createdAt}` | 401 |
| DELETE | `/me` | `{currentPassword}` | 204 | 401 `INVALID_CREDENTIALS` |

**Profiles** (`api/v1/profiles/me`, todos requieren Bearer):

| Método | Ruta | Request | Response | Errores |
|---|---|---|---|---|
| GET | `` | — | 200 `{id, firstName, lastName, birthDate, weightKg, heightCm, occupation, avatarUrl, timezone, createdAt, updatedAt}` — **nunca 404**, ver §3 | — |
| PUT | `` | mismos campos editables + `timezone` | 204 | 400 validación (rangos, `timezone` no es un IANA válido) |
| GET | `/goals` | — | 200 `[{id, type, targetValue, createdAt}]` | — |
| POST | `/goals` | `{type, targetValue}` | 201 `{id}` | 400 validación |
| DELETE | `/goals/{goalId}` | — | 204 | 404 (no existe, o no es del usuario autenticado) |
| GET | `/onboarding-survey` | — | 200 `{...}` **o 204** si nunca se contestó | — |
| PUT | `/onboarding-survey` | `{usualSchedule?, sleepSchedule?, declaredActivityLevel?, declaredStressLevel, declaredSleepQuality?}` | 204 | 400 `declaredStressLevel` inválido |

`declaredStressLevel` usa el mismo vocabulario que la columna de BD:
`MUY_BAJO`, `BAJO`, `MODERADO`, `ALTO`, `MUY_ALTO` (no los nombres de enum de
C#) — tanto en el request como en el response, para que Web/Android no
tengan que traducir nada.

---

## 3. Decisiones tomadas en este bloque

- **Get-or-create perezoso para `profiles`, NO auto-creación en el
  registro**: `GET /profiles/me` (y cualquier operación que la necesite —
  `PUT`, agregar una meta, contestar la encuesta) crea la fila de `profiles`
  vacía la primera vez que hace falta, en vez de crearla en
  `RegisterCommandHandler` (Bloque 2, ya cerrado y aprobado — no lo toqué) o
  exigir un endpoint explícito de "crear perfil" antes de poder verlo. Con
  esto, Web/Android nunca tienen que manejar un caso especial de "el perfil
  todavía no existe": `GET` siempre devuelve algo (aunque sea casi todo
  `null`, salvo `timezone: "UTC"`), nunca un 404.
- **La encuesta de onboarding SÍ se puede recontestar** (`PUT` = upsert, no
  solo creación): las respuestas declaradas (actividad, sueño, estrés) son
  cosas que cambian con el tiempo — no tiene sentido congelarlas para
  siempre en el primer envío. `GET` devuelve 204 (no 404) si nunca se
  contestó, para que el cliente distinga fácilmente "no hay nada que
  mostrar todavía" de un error real.
- **Borrar la cuenta exige la contraseña actual**, igual que
  cambiarla — un access token robado de vida corta (15 min) no debería
  bastar para destruir una cuenta.
- **Borrado de meta (`goal`) verificado por dueño**: el `DeleteGoalCommandHandler`
  hace un `JOIN` contra `profiles.user_id` — nunca confía en que el
  `goalId` de la URL "obviamente" pertenece al usuario autenticado. Un
  intento de borrar la meta de otro usuario da 404 (no 403) para no revelar
  si ese `goalId` existe.
- **Validación de `timezone` con `TimeZoneInfo.FindSystemTimeZoneById`**:
  .NET en Linux resuelve identificadores IANA nativamente desde .NET 6, así
  que no hizo falta agregar NodaTime ni TimeZoneConverter — un solo
  try/catch en el validador de FluentValidation.
- **Vocabulario de `declaredStressLevel` centralizado en Domain**: agregué
  `DeclaredStressLevelExtensions` (Domain.Profiles) con
  `ToWireString()`/`TryParseWireString()`, independiente de la conversión
  EF↔columna que ya vivía en Infrastructure (`OnboardingSurveyConfiguration`)
  — mismo vocabulario, dos capas que deliberadamente no comparten código
  entre sí, para no acoplar Domain a EF.
- **Renombré `Profiles/OnboardingSurvey/` a `Profiles/Onboarding/` a mitad de
  este bloque**, antes de que se convirtiera en un problema: iba a repetir
  exactamente el bug de namespace de Bloque 2 (`WellSense.Application.Profiles.OnboardingSurvey`
  como namespace, colisionando con la entidad `WellSense.Domain.Profiles.OnboardingSurvey`).
  Lo atrapé con el mismo script de auditoría que usé para cerrar Bloque 2,
  corrido proactivamente esta vez en vez de esperar a que fallara la
  compilación.

---

## 4. Qué pruebas existen

Unitarias (EF InMemory), mismo patrón que Bloques 1-2:
- `Users/GetMeQueryHandlerTests`, `Users/DeleteMeCommandHandlerTests`
  (soft-delete real, revoca tokens activos, contraseña incorrecta no borra
  nada).
- `Profiles/GetMyProfileQueryHandlerTests` (lazy-create en la primera
  llamada, la segunda llamada no duplica).
- `Profiles/UpsertMyProfileCommandHandlerTests` (crea y luego actualiza sin
  duplicar) + `UpsertMyProfileCommandValidatorTests` (zonas horarias
  válidas/inválidas, fecha de nacimiento futura rechazada).
- `Profiles/GoalsTests`: lazy-create de perfil al agregar una meta,
  aislamiento entre usuarios (`ListMyGoals` de un usuario nunca ve las metas
  de otro), y el caso de seguridad explícito — intentar borrar la meta de
  otro usuario lanza `KeyNotFoundException` y la meta sigue existiendo.
- `Profiles/OnboardingSurveyTests`: 204/null antes de contestar, upsert
  crea perfil+encuesta de paso, recontestar actualiza en vez de duplicar.

Integración HTTP end-to-end (mismo `CustomWebApplicationFactory` de Bloque
2, pipeline real): `Integration/ProfileFlowEndpointTests` — registra,
verifica email, hace login real, y contra ese Bearer real ejercita: perfil
antes de tocarlo (siempre 200, nunca 404), upsert+get roundtrip, zona
horaria inválida → 400 real del pipeline, flujo completo de
agregar/listar/borrar meta, encuesta 204→PUT→200, y borrado de cuenta
(contraseña incorrecta → 401, correcta → 204).

---

## 5. Qué necesita Web/Android de este bloque

- **Contratos exactos**: tabla de §2. `declaredStressLevel` usa
  `MUY_BAJO`/`BAJO`/`MODERADO`/`ALTO`/`MUY_ALTO` en ambos sentidos (nunca
  mandar/mostrar los nombres de enum de C#).
- **`GET /profiles/me` nunca da 404** — el cliente puede llamarlo
  inmediatamente después del login sin checar "¿ya existe el perfil?"
  primero. Si todos los campos vienen `null` (salvo `timezone: "UTC"`), es
  la señal de "todavía no llenó su perfil", no un error.
- **`timezone` la debe mandar el cliente**, no el backend la infiere: en
  Android, `TimeZone.getDefault().getID()`; en Web,
  `Intl.DateTimeFormat().resolvedOptions().timeZone`. Ambos dan un
  identificador IANA compatible directo con lo que el backend valida. Si
  el cliente nunca la manda, el usuario queda en `'UTC'` por default —
  no es un error, solo significa que sus scores diarios (cuando el bloque de
  ML los calcule) se alinearán a UTC en vez de a su día local.
- **`GET /profiles/me/onboarding-survey` puede dar 204** — distinto de un
  error; es la señal de "muestra la pantalla de onboarding", no de "algo
  salió mal".
- **Borrar cuenta y borrar meta piden confirmación de contraseña/dueño
  respectivamente** — el cliente debe pedir la contraseña actual antes de
  llamar a `DELETE /users/me` (no hay forma de saltarse esto, el backend la
  exige).

---

## 6. Riesgos abiertos

1. **Migración 014 no forma parte del DDL original aprobado por HANDOFF-DB**
   — extensión propuesta por este bloque, pendiente de que el chat de
   DB/orquestador la dé por buena si en algún momento se vuelve a tocar el
   diseño maestro de `profiles`.
2. **El job real de cálculo de `wellness_scores`/`stress_scores` con la
   zona horaria del usuario no está implementado** — eso es del bloque de
   ML/Dashboard. Este bloque solo deja la decisión tomada y el dato
   disponible.
3. Mismo aviso de siempre: no compilado/probado por mí en mi propio
   entorno — necesito que ustedes corran `dotnet build && dotnet test` antes
   de aprobar.

---

## 7. Checklist de las 10 capas del DoD

- [x] Validador (FluentValidation) en cada comando
- [x] Prueba unitaria de cada handler, incluyendo el caso de seguridad
      (borrar meta de otro usuario)
- [x] Prueba de integración HTTP end-to-end de los 7 endpoints nuevos
- [x] Documentación de API (Swagger, `[ProducesResponseType]` en cada acción)
- [x] Manejo de errores consistente (`AuthDomainException`/`KeyNotFoundException`
      → `ProblemDetails`)
- [x] Seguridad: borrado de cuenta exige contraseña, ownership check en
      metas, `timezone` validada server-side
- [x] Migración real, validada contra Postgres (up/down/re-apply)
- [ ] Compilación y `dotnet test` reales — **pendiente de tu lado**, ver §0

Quedo a la espera de tu luz verde (o correcciones) antes del siguiente
bloque.
