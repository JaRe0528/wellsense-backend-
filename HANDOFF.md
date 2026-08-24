# HANDOFF.md — Chat Backend (.NET), Bloque 8: Device Command System

> Entregable del **Bloque 8**. Requiere Bloque 7 cerrado (ya lo está,
> 133/133 reales, incluyendo el cierre del último `HasConversion<string>()`
> pendiente). Web → API → SignalR → Android → Watch, con
> START_MONITORING/STOP_MONITORING/CHANGE_INTERVAL/SYNC_NOW/REQUEST_STATUS
> y COMMAND_ACK — backlog P1.

---

## 0. Aviso operativo (se mantiene)

Sigo sin salida de red hacia NuGet en mi propio entorno de trabajo — no pude
correr `dotnet build`/`dotnet test` aquí. Lo que sí hice en este bloque:

- **Nueva migración (015) validada contra Postgres real de punta a punta**:
  no solo el `CREATE TABLE`, sino el ciclo de vida completo con `UPDATE`s
  reales — confirmé que ambos `CHECK` de consistencia
  (`delivered_at`/`acknowledged_at` obligatorios según el `status`)
  rechazan las transiciones inválidas y aceptan las válidas, antes de
  escribir una sola línea de C# encima.
- **Encontré y corregí una violación de capas en mi propio primer
  borrador**: `DeviceCommandHub` iba a inyectar `IWellSenseDbContext`
  directamente — rompía la regla que el resto del proyecto respeta sin
  excepción (Api → Application vía MediatR, nunca Api → DbContext directo).
  Lo corregí extrayendo una query dedicada (`IsDeviceOwnedByUserQuery`) en
  vez de dejarlo pasar por conveniencia — ver §2.
- **Apliqué con disciplina completa la lección de Bloque 6/7**:
  `IWellSenseDbContext` y sus 3 implementadores actualizados en el mismo
  cambio, antes de cualquier lógica — incluyendo el caso nuevo de que
  `WellSenseDbContext.cs` (a diferencia de bloques anteriores) SÍ necesitó
  el `DbSet` nuevo agregado a mano, porque esta es la primera tabla que no
  venía ya en el DDL original de Bloque 1.
- **Escribí la conversión de enum de `DeviceCommandType` con el switch
  completo desde el principio** (no el atajo de `ToUpperInvariant()`) —
  porque a diferencia de `DeviceCommandStatus`, sus valores SÍ tienen
  guion bajo (`START_MONITORING`), y ya aprendí de los 4 bugs anteriores a
  verificar eso ANTES de escribir la conversión, no después de que fallara.
- Los 3 barridos automatizados de siempre, limpios sobre las ~35 rutas
  nuevas de este bloque.

---

## 1. La tabla nueva: `device_commands` (migración 015)

No estaba en el DDL original de HANDOFF-DB — extensión de esquema
propuesta por este bloque, mismo criterio que la migración 014 (timezone,
Bloque 3). Pendiente de que el chat de DB/orquestador la confirme si se
retoca el diseño maestro.

```sql
CREATE TABLE device_commands (
    id, device_id, user_id,
    type             CHECK (IN START_MONITORING/STOP_MONITORING/CHANGE_INTERVAL/SYNC_NOW/REQUEST_STATUS),
    payload          jsonb,
    status           CHECK (IN PENDING/DELIVERED/ACKNOWLEDGED/FAILED/EXPIRED),
    ack_payload      jsonb,
    created_at, delivered_at, acknowledged_at, expires_at,
    CHECK (status NOT IN (DELIVERED,ACKNOWLEDGED,FAILED) OR delivered_at IS NOT NULL),
    CHECK ((status IN (ACKNOWLEDGED,FAILED)) = (acknowledged_at IS NOT NULL))
);
```

Ambos `CHECK` de consistencia se probaron contra Postgres real con
`UPDATE`s reales, no solo se razonaron:

```
--- marcar DELIVERED sin delivered_at ---
ERROR: violates check constraint "device_commands_check"
--- marcar ACKNOWLEDGED sin acknowledged_at ---
ERROR: violates check constraint "device_commands_check1"
--- con ambos campos puestos correctamente: ambas transiciones pasan ---
```

`expires_at` existe en el esquema pero **este bloque no implementa el job
que lo hace efectivo** (marcar `EXPIRED` un comando que nadie confirmó a
tiempo) — mismo tipo de decisión que la renovación de suscripciones del
Bloque 6: se deja el dato listo, no se construye el job que nadie pidió
todavía.

---

## 2. El error de capas que encontré en mi propio borrador

Al escribir `DeviceCommandHub`, mi primer instinto fue inyectarle
`IWellSenseDbContext` directamente para verificar que el `deviceId` que
Android manda por `RegisterForDevice` de verdad le pertenece. Antes de
seguir, caí en que eso rompía una regla que el resto del proyecto respeta
sin excepción desde el Bloque 2: **Api nunca toca `IWellSenseDbContext`
directamente, siempre pasa por MediatR hacia Application**. Un Hub de
SignalR es parte de la capa Api tanto como un Controller — la misma regla
aplica.

Lo corregí extrayendo una query mínima, reutilizable:
`Application/Devices/IsDeviceOwnedByUser` — un solo método,
`Task<bool> Handle(...)`, que el Hub llama vía `ISender` exactamente igual
que un Controller. No es una excepción a la regla, es la regla aplicada
con disciplina incluso cuando el atajo directo hubiera sido una línea más
corta.

---

## 3. El flujo completo: por qué el ACK es REST y no SignalR

`Web → API → SignalR → Android → Watch` cubre la ida. Para la vuelta
(`COMMAND_ACK`), decidí **REST, no el mismo canal de SignalR que entregó
el comando** — decisión explícita, documentada en el propio código:

- Android puede tardar en confirmar (relaya el comando al Watch, espera su
  respuesta) — la conexión de SignalR que recibió el comando original
  podría haberse caído y reconectado para cuando Android por fin tiene
  algo que confirmar.
- Un `POST` autenticado normal (mismo Bearer de siempre) es más
  resiliente/reintentable que depender de que la MISMA conexión en vivo
  siga viva en el momento exacto de confirmar.
- El ACK sí se reenvía a Web en vivo por SignalR — pero por el canal ya
  existente (`DashboardHub`, Bloque 5), reutilizando el mismo patrón de
  evento de integración de MediatR que `MeasurementsSyncedEvent`
  (`DeviceCommandAcknowledgedEvent` → `IDashboardNotifier`). Cierra el loop
  sin que Web tenga que hacer polling.

**Hub nuevo, `DeviceCommandHub`** (`/hubs/device-commands`), distinto de
`DashboardHub`: el grupo es por **dispositivo**, no por usuario — un
comando siempre va dirigido a uno específico, y un usuario puede tener
varios dispositivos conectados a la vez. Como el JWT no lleva qué
dispositivo es Android (mismo gap ya resuelto en `/sync`, Bloque 4), el
cliente debe invocar `RegisterForDevice(deviceId)` explícitamente tras
conectarse — verificado contra la propiedad real del dispositivo (§2)
antes de unir la conexión al grupo.

---

## 4. Endpoints listos

**Device Commands** (`api/v1/devices/{deviceId}/commands`, todos
requieren Bearer):

| Método | Ruta | Quién lo llama | Request | Response | Errores |
|---|---|---|---|---|---|
| POST | `` | Web/Admin | `{type, payload?}` | 201 `{commandId, type, status, createdAt}` | 400 validación, 404 `DEVICE_NOT_FOUND` |
| POST | `/{commandId}/ack` | Android | `{status: ACKNOWLEDGED\|FAILED, ackPayload?}` | 204 (idempotente) | 400 validación, 404 `DEVICE_NOT_FOUND`/`COMMAND_NOT_FOUND` |
| GET | `` | Web/Android | — | 200 `[{id, type, payload, status, ackPayload, createdAt, deliveredAt, acknowledgedAt, expiresAt}]` | 404 `DEVICE_NOT_FOUND` |
| GET | `/pending` | Android | — | 200 igual forma, solo PENDING/DELIVERED | 404 `DEVICE_NOT_FOUND` |

**SignalR**: `/hubs/device-commands` (Android) — evento `deviceCommand`
`(commandId, type, payload)`. `/hubs/dashboard` (Web, ya existía) — nuevo
`eventType`: `"device_command_acknowledged"`
`{deviceId, commandId, status}`.

`type` usa el mismo vocabulario que el `CHECK` de la BD:
`START_MONITORING`/`STOP_MONITORING`/`CHANGE_INTERVAL`/`SYNC_NOW`/`REQUEST_STATUS`.
`CHANGE_INTERVAL` exige `payload: {"intervalSeconds": <entero positivo>}`
— es el único tipo que necesita un payload específico, validado antes de
llegar al handler.

---

## 5. Decisiones tomadas en este bloque

- **El comando SIEMPRE se crea, exista o no un cliente Android
  conectado** — el push por SignalR es un mejor-esfuerzo de entrega
  inmediata, nunca la fuente de verdad de si el comando "existe". Un push
  fallido (nadie conectado, error de red) nunca tumba la emisión — el
  comando queda `PENDING`, recuperable después vía `/pending`.
- **`DELIVERED` significa "se intentó empujar por SignalR", NO que Android
  lo recibió** — esa confirmación real es el ACK, un paso completamente
  aparte. Nombré esto explícito para que Web/Android no confundan
  "delivered" con "acknowledged".
- **El ACK es idempotente** — reconocer un comando que ya estaba en un
  estado terminal no lo reprocesa ni lanza error, mismo criterio que
  Logout/MarkNotificationRead en bloques anteriores. Útil si Android
  reintenta el POST por un timeout de red aunque el primero sí haya
  llegado.
- **`SyncDomainException` se reutilizó para `COMMAND_NOT_FOUND`**, en vez
  de crear una cuarta clase de excepción de dominio — mismo criterio que
  ya se usaba para `DEVICE_NOT_FOUND`: un comando pertenece al mismo
  dominio conceptual que un dispositivo (Bloque 4).
- **Se implementó un Hub nuevo (`DeviceCommandHub`), no se sobrecargó
  `DashboardHub`** — semánticas de grupo distintas (por dispositivo vs.
  por usuario) ameritan hubs separados, aunque ambos vivan en el mismo
  proyecto y compartan el mismo patrón de JWT-por-query-string.

---

## 6. Qué pruebas existen

Unitarias con EF InMemory:
- `DeviceCommands/IssueDeviceCommandCommandHandlerTests`: comando se crea
  y pasa a `DELIVERED` cuando el push "funciona"; **el comando se crea
  igual y queda `PENDING` cuando el push falla** (nunca tumba la
  emisión); `CHANGE_INTERVAL` sin payload válido rechazado por el
  validador; emitir a dispositivo ajeno o desvinculado lanza
  `DEVICE_NOT_FOUND`.
- `DeviceCommands/AcknowledgeDeviceCommandCommandHandlerTests`: ACK pone
  `ACKNOWLEDGED`/`FAILED` según corresponda y publica el evento;
  **reconocer un comando ya en estado terminal es idempotente** (no se
  reprocesa); reconocer un comando inexistente lanza `COMMAND_NOT_FOUND`.
- `DeviceCommands/ListDeviceCommandsTests`: historial ordenado
  más-reciente-primero; `/pending` excluye `ACKNOWLEDGED`/`FAILED`;
  listar comandos de un dispositivo ajeno lanza `DEVICE_NOT_FOUND`.
- `DeviceCommands/DeviceCommandTypeExtensionsTests`: roundtrip completo de
  los 5 tipos.
- `DeviceCommands/IsDeviceOwnedByUserQueryHandlerTests`: la query que le
  quité al Hub, probada por separado.

Integración HTTP end-to-end:
- `Integration/DeviceCommandsFlowEndpointTests`: ciclo completo
  emitir→pendientes→confirmar→historial por REST real.
- `Integration/DeviceCommandHubEndpointTests` — **la prueba más
  importante de este bloque**: conecta un cliente SignalR REAL a
  `/hubs/device-commands` (mismo patrón que `DashboardHubEndpointTests`,
  Bloque 5), invoca `RegisterForDevice`, emite un comando real por REST, y
  confirma que el cliente conectado lo recibe en vivo. Segunda prueba
  complementaria: una conexión que NUNCA se registra para el dispositivo
  no recibe sus comandos — confirma que el aislamiento por grupo
  realmente aísla.

---

## 7. Qué necesita Web/Android de este bloque

- **Web**: `POST /devices/{deviceId}/commands` para emitir; escuchar
  `device_command_acknowledged` en el mismo `DashboardHub` que ya usan
  desde Bloque 5 para ver la confirmación en vivo sin hacer polling.
- **Android**: conectarse a `wss://.../hubs/device-commands?access_token={jwt}`
  e invocar `RegisterForDevice(deviceId)` justo después de conectar — sin
  eso, nunca se une al grupo y nunca recibe nada, aunque la conexión esté
  viva. Escuchar el evento `deviceCommand`. **Confirmar SIEMPRE por REST**
  (`POST .../ack`), nunca asumir que el mismo canal de SignalR sigue
  disponible para responder.
- **Android, al reconectar**: llamar `GET .../pending` para recuperar
  cualquier comando que se haya perdido mientras estaba desconectado —
  nunca depender solo del push en vivo.
- **`CHANGE_INTERVAL` exige `payload: {"intervalSeconds": N}`** con N
  entero positivo — el resto de los tipos no necesita payload.

---

## 8. Riesgos abiertos

1. **No hay job que marque `EXPIRED`** los comandos que nadie confirmó
   dentro de `expires_at` — el dato existe, nada actúa sobre él todavía.
   Mismo tipo de alcance que la renovación de suscripciones (Bloque 6).
2. **Un comando duplicado (Web hace doble clic, dos POST) crea dos filas
   distintas** — a diferencia de `/sync` (Bloque 4) o `/subscribe` (Bloque
   6), este endpoint no tiene un concepto de idempotency-key. No parecía
   necesario para "emitir un comando" (a diferencia de "cobrar una
   tarjeta" o "sincronizar mediciones", donde un duplicado tiene
   consecuencias reales) — pero si Web/Android reportan comandos
   duplicados como un problema real, es un patrón ya establecido
   (`requestId`/`idempotencyKey`) que se puede agregar.
3. Mismo aviso de siempre: no compilado/probado por mí en mi propio
   entorno — necesito `dotnet build && dotnet test` de su lado antes de
   aprobar.

---

## 9. Checklist de las 10 capas del DoD

- [x] Validador (FluentValidation) en cada comando, incluyendo la
      validación de forma específica de `CHANGE_INTERVAL`
- [x] Prueba unitaria de cada handler, incluyendo el caso de "push falla
      pero el comando igual se crea" y el ACK idempotente
- [x] Prueba de integración HTTP end-to-end + **una conexión SignalR real
      de punta a punta** para el hub nuevo, no solo la lógica que lo
      dispara
- [x] Documentación de API (Swagger, `[ProducesResponseType]`)
- [x] Manejo de errores consistente (`COMMAND_NOT_FOUND` agregado a
      `SyncDomainException`, ya conectado al middleware desde Bloque 4)
- [x] Migración nueva validada contra Postgres real, ciclo de vida
      completo incluido, no solo el `CREATE TABLE`
- [x] Error de capas en mi propio borrador (Hub con acceso directo a BD)
      encontrado y corregido antes de escribir más código encima
- [ ] Compilación y `dotnet test` reales — **pendiente de tu lado**, ver §0

Quedo a la espera de tu luz verde (o correcciones) antes del siguiente
bloque.
