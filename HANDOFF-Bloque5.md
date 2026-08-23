# HANDOFF.md — Chat Backend (.NET), Bloque 5: SignalR (dashboard en vivo) + FCM (push)

> Entregable del **Bloque 5**. Requiere Bloque 4 cerrado (ya lo está, 65/65
> reales). Cubre el hub de SignalR para el dashboard web en vivo y el envío
> de push por FCM, incluyendo la conexión entre ambos: cuando Android
> sincroniza mediciones, el dashboard web conectado se entera al instante.

---

## 0. Aviso operativo (se mantiene)

Sigo sin salida de red hacia NuGet en mi propio entorno de trabajo — no pude
correr `dotnet build`/`dotnet test` aquí. Lo que sí hice en este bloque:

- Antes de escribir una sola línea de lógica nueva, **actualicé los 3
  implementadores de `IWellSenseDbContext`** (incluyendo los 2 decoradores
  de prueba) en el mismo cambio que agrandó la interfaz —el blindspot que
  quedó documentado al cerrar Bloque 4 no se repitió esta vez.
- **Encontré y corregí dos bugs de plomería yo mismo, antes de que llegaran
  a compilación**, ambos por razonamiento cuidadoso, no por intentar
  compilar: ver §2.
- Validé `notification_tokens`/`notifications` contra Postgres real — no
  hubo migración nueva en este bloque (esas tablas ya existían desde el
  Bloque 1), pero sí demostré con un `INSERT` real por qué el handler de
  registro de token necesita borrar el token anterior del mismo dispositivo
  él mismo, en vez de confiar en el índice único de la BD (ver §2).
- Los 3 barridos automatizados de siempre (colisión de namespace, balance de
  llaves/paréntesis, tipos-vs-`using`) sobre las ~30 rutas nuevas/tocadas de
  este bloque — limpio.

---

## 1. Qué quedó armado

### SignalR — dashboard en vivo
- `DashboardHub` (`/hubs/dashboard`, requiere Bearer): cada conexión se une
  automáticamente a un grupo `user-{userId}`. No expone métodos invocables
  por el cliente — el dashboard solo escucha.
- `IDashboardNotifier` (Application) / `SignalRDashboardNotifier` (Api):
  mismo patrón que `IEmailSender`/`ICurrentUserService` — Application nunca
  referencia `Microsoft.AspNetCore.SignalR` directamente.
- El canal es deliberadamente de **invalidación, no de payload completo**:
  el evento `dashboardUpdate` manda `(eventType, payload liviano)` — el
  cliente decide qué volver a pedir por REST. No intenta adivinar la forma
  del dashboard computado (eso es del bloque de ML/Dashboard).
- **Conectado a Sync (Bloque 4)**: `SyncMeasurementsCommandHandler` ahora
  publica `MeasurementsSyncedEvent` (evento de integración de MediatR, no
  HTTP) cuando un sync trae datos nuevos de verdad. Un handler lo escucha y
  lo reenvía por SignalR al grupo del usuario — ver §2 para el detalle de
  por qué se modificó ese handler ya aprobado.

### FCM — push real, no un stub
- `IPushNotificationSender` (Application) / `FirebaseCloudMessagingSender`
  (Infrastructure, usa el SDK oficial `FirebaseAdmin`): a diferencia de
  `LoggingEmailSender` (Bloque 2, que quedó un stub permanente), aquí SÍ
  implementé el envío real — Android necesita algo que funcione en cuanto
  DevSecOps coloque `Firebase:CredentialsPath`, no un stub para siempre.
  Sin credenciales configuradas, nunca lanza: loguea una advertencia una
  sola vez y devuelve `false` en cada intento — la app no debe fallar por
  esto, ni al arrancar ni en cada request.
- `notification_tokens`: un dispositivo tiene **a lo sumo un token activo**
  (invariante que impone el handler, no la BD — demostrado con un `INSERT`
  real, ver §2).
- `notifications` (centro in-app): se crea **siempre**, exista o no un
  token FCM, y exista o no éxito de push — es la fuente de verdad de "esta
  notificación existe para el usuario", el push es solo mejor-esfuerzo de
  entrega inmediata.

### Endpoints

**Notifications** (`api/v1/notifications`, todos requieren Bearer):

| Método | Ruta | Request | Response | Errores |
|---|---|---|---|---|
| POST | `/tokens` | `{deviceId, fcmToken}` | 204 | 404 `DEVICE_NOT_FOUND` |
| GET | `` | query `?unreadOnly=bool` | 200 `[{id, type, title, body, readAt, createdAt}]` | — |
| PUT | `/{notificationId}/read` | — | 204 (idempotente) | — |
| POST | `/test` | `{title, body}` | 200 `{notificationId, pushedCount, failedPushCount}` | — |

No hay endpoints nuevos de `Devices`/`Sync` — esos ya estaban del Bloque 4;
este bloque solo agrega la publicación del evento (ver §2).

---

## 2. Los dos bugs de plomería que atrapé antes de compilar

**1) `ICurrentUserService` dentro del Hub.** Mi primer borrador de
`DashboardHub` inyectaba `ICurrentUserService` (que depende de
`IHttpContextAccessor`) para saber a qué grupo unir la conexión — el mismo
patrón que uso en todos los controladores. Antes de seguir, caí en que ese
es un patrón conocido como frágil específicamente para SignalR: tras el
upgrade a WebSocket, el `HttpContext` de la request HTTP original no es una
fuente confiable durante toda la vida de la conexión. Lo corregí para leer
`Context.User` directamente (el `ClaimsPrincipal` propio del Hub, que
SignalR repuebla en cada conexión desde el mismo pipeline de
autenticación) — el patrón recomendado por Microsoft para este caso
específico.

**2) El índice único de `notification_tokens` no evita que un dispositivo
acumule tokens viejos.** El único índice único real es
`(device_id, fcm_token)`, no `device_id` solo — así que un dispositivo que
re-registra un token DISTINTO (normal: los tokens de FCM rotan) simplemente
inserta una fila nueva sin que la BD se queje. Lo demostré con un `INSERT`
real antes de dar el handler por bueno:

```
--- mismo device_id + mismo fcm_token → sí falla (el índice único hace su trabajo) ---
ERROR:  duplicate key value violates unique constraint "notification_tokens_device_id_fcm_token_key"
--- mismo device_id + fcm_token DISTINTO → inserta igual, sin quejarse ---
INSERT 0 1
 count
-------
     2
```

Por eso `RegisterNotificationTokenCommandHandler` borra explícitamente
cualquier token previo del mismo `device_id` antes de insertar el nuevo —
la invariante "un dispositivo, un token activo" la impone el handler, no la
BD (documentado también en el propio código, no solo aquí).

---

## 3. Modificación a código ya aprobado (Bloque 4) — flagueada para su revisión

`SyncMeasurementsCommandHandler.cs` (Bloque 4, cerrado) se modificó para
agregar `IPublisher publisher` al constructor y publicar
`MeasurementsSyncedEvent` al final del camino feliz. **La lógica de
idempotencia/clasificación del handler no se tocó en absoluto** — es
exactamente el mismo código que ya aprobaron, con una sola llamada nueva
agregada al final. Reglas de cuándo se publica, para que quede explícito:

- Nunca en el replay idempotente (esos datos ya se habían reportado la
  primera vez que se procesaron de verdad).
- Nunca en la recuperación de carrera (la request que sí ganó ya lo habría
  publicado ella misma).
- Solo si `acceptedCount > 0` — un sync que solo trajo duplicados/rechazos
  no es información nueva para el dashboard.

Esto obligó a actualizar los 8 sitios de prueba que construían
`SyncMeasurementsCommandHandler` directamente (nuevo 4to parámetro) — se
agregó un `NoOpPublisher` reutilizable en `TestFakes.cs` para no repetir un
mock en cada uno, y una prueba nueva (`Publishes_dashboard_event_only_when_new_measurements_were_actually_accepted`)
que verifica explícitamente las reglas de arriba con un `SpyPublisher`.

---

## 4. Decisiones tomadas en este bloque

- **JWT vía query string (`?access_token=`), acotado por path a
  `/hubs/dashboard` exclusivamente** — un WebSocket no puede mandar un
  header `Authorization` normal en el handshake del navegador; este es el
  patrón oficial de ASP.NET Core para SignalR + JWT bearer. El resto de la
  Api sigue exigiendo el header normal — el chequeo de path en
  `OnMessageReceived` es lo que evita que esto debilite la autenticación de
  cualquier otro endpoint.
- **El canal de SignalR no transporta el payload completo del dashboard,
  solo un aviso de "algo cambió"** — evita que este bloque tenga que
  inventar/adivinar la forma de los datos que el bloque de ML/Dashboard
  todavía no ha diseñado.
- **`SendNotificationCommand` es un servicio reutilizable vía MediatR, no
  solo un endpoint HTTP** — cualquier módulo futuro (ML avisando estrés
  alto, un recordatorio) puede mandarlo directo sin pasar por HTTP. El
  endpoint `/notifications/test` existe para que Web/Android puedan validar
  el flujo completo sin depender de que otro bloque ya dispare
  notificaciones reales.
- **Un push fallido nunca tumba nada** — ni el flujo que lo llama
  (`IPushNotificationSender.TrySendAsync` devuelve `bool`, nunca lanza), ni
  el registro in-app (`notifications` se crea siempre, independiente del
  resultado del push).
- **No se implementó limpieza automática de tokens muertos** (un push que
  falla por token inválido/expirado no borra ese `notification_token`) —
  decisión consciente de alcance, ver riesgo #2.

---

## 5. Qué pruebas existen

Unitarias (EF InMemory):
- `Notifications/NotificationTokensTests`: registrar crea la fila,
  re-registrar reemplaza (no duplica), registrar para un dispositivo ajeno
  lanza `DEVICE_NOT_FOUND`.
- `Notifications/ListAndMarkNotificationsTests`: aislamiento entre
  usuarios, orden más-reciente-primero, filtro `unreadOnly`, marcar leída
  es idempotente, marcar leída de otro usuario no hace nada silenciosamente.
- `Notifications/SendNotificationCommandHandlerTests`: el registro in-app
  se crea aunque no haya tokens; empuja a TODOS los tokens del usuario; un
  push fallido no impide que el registro in-app se persista.
- `Notifications/MeasurementsSyncedEventHandlerTests`: el handler del
  evento reenvía al notifier con el `eventType` estable
  `"measurements_synced"`.
- `Sync/SyncMeasurementsCommandHandlerTests` (extendida): nueva prueba que
  confirma que el evento de dashboard se publica solo cuando de verdad se
  aceptó algo nuevo — no en duplicados/rechazos puros.

Integración HTTP end-to-end:
- `Integration/NotificationsFlowEndpointTests`: flujo completo
  registrar-token → enviar-test → listar → marcar-leída, y registrar token
  para el dispositivo de otro usuario da 404 real.
- `Integration/DashboardHubEndpointTests` — **la prueba más importante de
  este bloque**: conecta un cliente SignalR REAL (`Microsoft.AspNetCore.SignalR.Client`,
  transporte forzado a LongPolling porque `TestServer` no soporta upgrade
  real a WebSocket) contra el pipeline HTTP real, con el JWT por query
  string tal como lo mandaría un navegador. Dispara un `/sync/measurements`
  real y confirma que el dashboard conectado recibe el evento
  `dashboardUpdate` con `eventType == "measurements_synced"` dentro de 10
  segundos. Esto cubre la plomería nueva y riesgosa de punta a punta, no
  solo la lógica de negocio que la dispara.

---

## 6. Qué necesita Web/Android de este bloque

- **Web**: conectarse a `wss://.../hubs/dashboard?access_token={jwt}` (o el
  transporte que el cliente de SignalR elija automáticamente) y escuchar el
  evento `"dashboardUpdate"` → `(eventType: string, payload: object)`. Por
  ahora el único `eventType` es `"measurements_synced"` con
  `{ acceptedCount, syncedAt }` — al recibirlo, volver a pedir lo que el
  dashboard necesite mostrar por REST (este canal no manda el dato
  calculado, solo avisa que hay algo nuevo).
- **Android**: registrar el token FCM tras obtenerlo del SDK de Firebase,
  vía `POST /notifications/tokens`, y volver a registrarlo cada vez que
  Firebase entregue un token rotado (`onNewToken` del SDK) — el backend ya
  maneja el reemplazo, Android no necesita "borrar el viejo" primero.
- **Ambos**: `POST /notifications/test` para validar el flujo de punta a
  punta en desarrollo sin depender de otro bloque.

---

## 7. Riesgos abiertos

1. **`Firebase:CredentialsPath` sin configurar en cualquier ambiente real**
   deja el push completamente deshabilitado de forma silenciosa (solo un
   log de advertencia una vez) — es el comportamiento correcto para no
   tumbar la app, pero significa que un despliegue real sin ese archivo
   "funciona" pero nunca manda push, sin que nada grite fuerte sobre eso
   más allá del log. Responsabilidad de DevSecOps, mismo patrón que SMTP.
2. **Tokens FCM muertos no se limpian automáticamente.** Un push que falla
   repetidamente contra el mismo token (típico de una desinstalación de la
   app) deja esa fila en `notification_tokens` indefinidamente — no rompe
   nada, solo desperdicia llamadas a FCM. Se podría resolver interpretando
   el código de error específico que devuelve FCM para tokens
   inválidos/no-registrados y borrando esa fila entonces — no lo hice en
   este bloque, `IPushNotificationSender.TrySendAsync` solo devuelve
   `bool`, no la razón del fallo.
3. **`AspNetCoreRateLimit` no cubre `/notifications/test`** — un usuario
   autenticado podría llamar este endpoint repetidamente para forzar envíos
   de push. No es P0 como `/device-link/redeem` (requiere estar
   autenticado, no es una superficie de fuerza bruta), pero si se vuelve un
   problema real, es una regla más en `appsettings.json:IpRateLimiting`.
4. Mismo aviso de siempre: no compilado/probado por mí en mi propio
   entorno — necesito `dotnet build && dotnet test` de su lado antes de
   aprobar.

---

## 8. Checklist de las 10 capas del DoD

- [x] Validador (FluentValidation) en cada comando
- [x] Prueba unitaria de cada handler, incluyendo el caso de seguridad
      (registrar token para dispositivo ajeno)
- [x] Prueba de integración HTTP end-to-end, incluyendo una conexión
      SignalR real de punta a punta (no solo la lógica que la dispara)
- [x] Documentación de API (Swagger, `[ProducesResponseType]`)
- [x] Manejo de errores consistente (reutiliza `SyncDomainException` para
      "dispositivo no encontrado" — mismo concepto que Bloque 4, no se creó
      una excepción nueva para lo mismo)
- [x] Dos bugs de plomería reales encontrados y corregidos antes de
      compilar — uno de patrón SignalR, uno demostrado contra Postgres real
- [x] Modificación a código aprobado (Bloque 4) flagueada explícitamente,
      con las reglas exactas documentadas y una prueba dedicada
- [ ] Compilación y `dotnet test` reales — **pendiente de tu lado**, ver §0

Quedo a la espera de tu luz verde (o correcciones) antes del siguiente
bloque.
