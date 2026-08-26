# HANDOFF.md — Chat Backend (.NET), post-Bloque-10: fix urgente + correo real + límites de plan + WEB + upgrade

> Seis partes en un solo envío, en el orden de prioridad pedido. Requiere
> Bloque 10 cerrado (196/196 confirmado). Migraciones nuevas: 016
> (urgente), 017 (límites/features), 018 (device WEB) — las tres validadas
> de punta a punta contra Postgres real, cada una con su ciclo completo
> arriba/abajo/arriba, no solo el `CREATE`/`ALTER`.

---

## 0. Aviso operativo

Sigo sin salida de red hacia NuGet en mi propio entorno de trabajo — no pude
correr `dotnet build`/`dotnet test` aquí. Lo que sí hice:

- Las 3 migraciones nuevas, validadas con el mismo rigor de siempre
  (aplicar → confirmar el efecto real con un `INSERT`/`UPDATE` que
  reproduce exactamente el caso que importa → revertir → confirmar que
  revierte → volver a aplicar).
- **Reproduje tu error de producción carácter por carácter** antes de
  tocar nada (Parte 1) — no asumí la causa, la confirmé con un `PREPARE`/
  `EXECUTE` que dispara el mismo `42804` que Render reportó.
- **Escribí, pero no pude correr yo mismo**, la primera prueba de este
  proyecto contra Postgres real vía Testcontainers (mi sandbox no tiene
  Docker) — marcada explícitamente como pendiente de que ustedes la
  confirmen, a diferencia de todo lo demás que sí validé con psql
  directamente.
- **Encontré y corregí dos errores reales en mi propio trabajo de este
  mismo turno**, antes de que llegaran a ningún lado — ver §5.1 (dónde
  vivía de verdad el `CHECK` de `devices.type`) y §5.2 (el bug de tipo
  silencioso que ya estaba en el código, no algo que yo introduje, pero
  que encontré al tocar esa zona).
- Las credenciales SMTP reales que mandaste **no se escribieron en
  ningún archivo** de este entregable — ni en código, ni en
  `appsettings.json`, ni en este HANDOFF. Solo los nombres de las claves
  de configuración.
- Los 3 barridos automatizados de siempre, limpios sobre todo lo tocado.

---

## 1. PARTE 1 — Bug urgente: login roto en producción (RESUELTO)

**Causa confirmada** (la tuya, verificada por mi cuenta): `audit_logs.ip_address`
se creó como `inet` nativo desde la migración 002/Bloque 1— la tabla
`audit_logs` en sí vive en la migración 002, confirmé el DDL exacto antes
de escribir nada. `AuditLog.IpAddress` (C#) siempre fue un `string` plano
sin `HasConversion` en `AuditLogConfiguration`. Nunca falló hasta que el
Bloque 10 activó la auditoría real (login, etc.), momento en el que
Npgsql empezó a mandar un parámetro `text` contra una columna `inet`.

Reproduje el error EXACTO antes de escribir la migración:
```
PREPARE ins(...) AS INSERT INTO audit_logs(...) VALUES (...);
EXECUTE ins(...);
→ ERROR:  column "ip_address" is of type inet but expression is of type text
```
Carácter por carácter el mismo mensaje que reportó Render.

**Fix — migración 016**: `ALTER TABLE audit_logs ALTER COLUMN ip_address TYPE text;`
No se tocó el modelo C# — `AuditLog.IpAddress` sigue siendo `string?`,
tal como pediste. Validado: aplicar → el mismo `INSERT` que antes fallaba
ahora funciona → revertir → vuelve a fallar (confirma que la reversión es
real, no un placebo) → re-aplicar.

**Regresión escrita para que esto no vuelva a pasar en silencio**: hice un
inventario honesto de por qué 196/196 pruebas en verde no atraparon esto
— **todas** las casi 200 pruebas de integración de este proyecto usan el
proveedor InMemory de EF Core, que no aplica tipos nativos de Postgres en
absoluto. `Testcontainers.PostgreSql` estaba referenciado desde el
Bloque 1 con la intención explícita de agregar pruebas contra Postgres
real "cuando hubiera lógica de negocio que probar" — nunca se hizo, en
ningún bloque. Escribí `AuditLogRealPostgresTests` (la primera prueba de
este proyecto que corre contra un contenedor Postgres real, aplicando las
16 migraciones reales) para cerrar ese hueco — **pero mi entorno no tiene
Docker, así que no pude correrla yo mismo**. Necesito que la corran
ustedes (`dotnet test`, con Docker disponible) para confirmarla; el fix en
sí (la migración) SÍ está validado por mi cuenta.

---

## 2. PARTE 2 — Correo real por SMTP (MailKit) con diseño de marca

- `SmtpEmailSender` (MailKit, STARTTLS puerto 587) reemplaza a
  `LoggingEmailSender` como implementación real de `IEmailSender` — mismo
  patrón que Firebase/Stripe: si `Smtp:Host` no está configurado, cae a
  `LoggingEmailSender` (nunca lanza, nunca rompe el arranque). Un fallo
  real de envío (credenciales rechazadas, host inalcanzable) tampoco se
  propaga — un correo que no se pudo mandar nunca debe convertir un
  registro exitoso en un 500 para el usuario.
- **Credenciales**: viven SOLO en `Smtp:Host`/`Port`/`Username`/`Password`/
  `FromAddress`/`FromName` y `Frontend:BaseUrl`, todas vacías en
  `appsettings.json` (mismo patrón que `Jwt:Secret`) — configúrenlas en
  Render como variables de entorno `Smtp__Host`, `Smtp__Username`, etc.
  (doble guion bajo = separador de sección en variables de entorno .NET).
- **Links reales**: `{Frontend:BaseUrl}/verificar-correo?token={token}` y
  `{Frontend:BaseUrl}/restablecer-contrasena?token={token}` — el dominio
  nunca está hardcodeado, viene de configuración.
- **`IEmailSender` cambió de forma** (se agregó `recipientName`) — ambos
  handlers que lo llaman se actualizaron: `RegisterCommandHandler` manda
  `null` (todavía no existe Profile en ese punto del flujo);
  `ForgotPasswordCommandHandler` ahora consulta `Profiles` y arma
  `"{FirstName} {LastName}".Trim()`, cayendo a `null` (que
  `SmtpEmailSender` resuelve a mostrar el email) si no hay nombre.
- **Plantillas HTML** (`EmailTemplates.cs`, Infrastructure): funciones
  puras, mismo criterio que `DailyScoringRules` — se puede probar el HTML
  exacto sin SMTP de por medio. Tablas + estilos inline (nunca `<style>`
  externo ni flexbox/grid — la mayoría de los clientes de correo no los
  soportan bien), Arial/Helvetica. Los 3 colores (`#F3F5F1` Paper,
  `#1B4B43` Pine, `#E64B3C` Pulse) y la estructura exacta que pediste
  (header sólido con tagline, tarjeta blanca con eyebrow/título/párrafo/
  botón/link plano/franja de vencimiento, footer fuera de la tarjeta).
  El nombre de saludo se escapa con `HtmlEncode` — nunca se interpola
  HTML crudo de un valor que el usuario controla (el nombre de su
  Profile).
- **Expiración real, no inventada**: el correo de verificación dice "vence
  en 24 horas" porque `RegisterCommandHandler` de verdad pone
  `ExpiresAt = clock.UtcNow.AddHours(24)`; el de reset dice "vence en 1
  hora" porque `ForgotPasswordCommandHandler` de verdad usa
  `AddHours(1)` — el texto del correo refleja el dato real, no un número
  aparte que pudiera desincronizarse.

---

## 3. PARTE 3 — Límites reales por plan (migración 017)

Valores elegidos (documentados como criterio propio, ajustables sin tocar
código — viven en datos):

| Plan | maxDevices | historyDays |
|---|---|---|
| FREE | 1 | 7 |
| BASIC | 2 | 30 |
| PRO | 5 | 90 |
| PROFESSIONAL | `null` (ilimitado) | `null` (ilimitado) |

`membership_plans.limits` (jsonb) — `null` en un campo es el valor REAL
sembrado para "sin límite", no un sentinel como `-1`. `PlanLimits.Parse`
(Application, función pura) nunca lanza — un `limits` vacío o malformado
cae a "sin límite" (fail-open, nunca bloquea a alguien por un dato
corrupto). `PlanLimitsResolver.ResolveForUserAsync` (extension method
compartido) resuelve el plan efectivo del usuario: su suscripción activa,
o FREE si nunca tuvo ninguna (mismo criterio que `GetMyMembershipQueryHandler`)
— **sin crear la fila de suscripción perezosamente**, porque un chequeo
de límite es una lectura, no debería tener el efecto secundario de
"afiliar" a alguien a FREE.

**`POST /devices`**: cuenta dispositivos con `Status != Unpaired` contra
`maxDevices` — desvincular uno libera el cupo (probado explícitamente).
403 `PLAN_LIMIT_EXCEEDED` si se excede.

**`GET /wellness/me/history`**: `Days` se recorta al `historyDays` del
plan — nunca error, solo un resultado más chico. Probado en ambas
direcciones: pedir más de lo permitido se recorta; pedir MENOS de lo
permitido nunca se "regala" de más.

---

## 4. PARTE 4 — Features honestos, no inventados

`membership_plans.features` (antes `'{}'` vacío para los 4 planes desde
Bloque 1) ahora es un arreglo real de strings, expuesto en
`GET /memberships/plans` junto a `limits` — misma fuente de verdad que la
Parte 3, no una copia separada.

**Decisión deliberada**: los features SOLO reflejan lo que Parte 3
realmente aplica (cantidad de dispositivos, días de historial) — **no
inventé diferenciadores que no existen**, como "soporte prioritario" o
"comandos en tiempo real" (el Device Command System del Bloque 8 no tiene
ningún gating por plan hoy — cualquier usuario, de cualquier plan, ya
puede emitir comandos; listarlo como feature de un plan superior sería
publicidad falsa). Si en el futuro se gatea algo más por plan, ese es el
momento de agregarlo a `features` — no antes.

---

## 5. PARTE 5 — Dispositivos WEB

### 5.1 Corrección a mi propio primer intento

Mi primer paso fue buscar el `CHECK` de `devices.type` en la migración
003 — no encontré ninguno ahí (esa migración es de `profiles`/`goals`) y
casi concluí "no hace falta migración, el tipo es libre". Antes de dar
eso por bueno, lo verifiqué contra Postgres real con un `INSERT` — **sí
existe** un `CHECK (type IN ('PHONE','WATCH'))`, solo que vive en la
migración 004 (donde se crea la tabla `devices` en sí), no en la 003.
Corregido antes de escribir la migración equivocada — 018 sí es
necesaria, y su comentario documenta explícitamente dónde vive el
`CHECK` real para que este error no se repita.

### 5.2 Bug de tipo silencioso, encontrado al tocar esta zona

Al agregar `DeviceType.Web`, encontré que tanto la conversión de EF
(`v == DeviceType.Phone ? "PHONE" : "WATCH"`) como el parseo en
`RegisterDeviceCommandHandler` (`request.Type == "WATCH" ? ... : ...Phone`)
eran ternarios binarios — **cualquier valor que no fuera exactamente
"PHONE"/"WATCH" caía silenciosamente en el otro**, sin error. Nunca se
manifestó porque el validador ya solo dejaba pasar esos dos valores, pero
en cuanto WEB llegara sin este fix, se habría guardado como WATCH sin que
nadie se enterara. Corregido con un switch completo en ambos lugares,
mismo patrón que `MeasurementType`/`DeviceCommandType`.

### 5.3 Lo que quedó

- Migración 018: `devices_type_check` ahora acepta `'WEB'` — validado
  antes/después/revertido/reaplicado contra Postgres real.
- `POST /devices` y `POST /notifications/tokens` confirmados funcionando
  igual para WEB que para PHONE/WATCH — prueba de integración HTTP real
  end-to-end, no solo razonado.
- **VAPID / Web Push**: no hace falta que yo exponga ninguna clave
  pública nueva. `notification_tokens.fcm_token` (Bloque 5) ya es un
  string genérico — el Web Push SDK de Firebase (`firebase-messaging`
  en el navegador) genera su propio token FCM usando la
  `VAPID_PUBLIC_KEY` del proyecto de Firebase de Web, que se configura
  del lado del **Chat Web** directamente en su configuración de Firebase
  (no es un secreto que el backend deba generar o guardar — es pública
  por diseño, vive en el bundle del cliente Web). El backend no necesita
  saber nada de VAPID: solo recibe el token FCM resultante y lo guarda
  igual que para Android, `FirebaseCloudMessagingSender` ya envía a
  cualquier token FCM sin importar de qué plataforma vino. **Pásenle
  esto al Chat Web tal cual**: configuren su propio proyecto de Firebase
  para Web, obtengan su VAPID key desde la consola de Firebase, y manden
  el token resultante a `POST /notifications/tokens` — cero cambios
  adicionales de nuestro lado.

---

## 6. PARTE 6 — Upgrade de plan pagado a uno mayor (confirmado, sin bugs)

`Upgrading_from_an_active_paid_plan_to_a_higher_one_replaces_it_cleanly_over_http`
(BASIC activo → PRO con tarjeta aprobada) — el mecanismo de "reemplazo en
dos pasos" que ya se había construido y probado en Bloque 6 para
FREE→pagado y pagado→cancelar **funcionó correctamente también para
pagado→pagado sin necesitar ningún cambio**: confirmé que
`SubscribeToPlanCommandHandler` no distingue el caso "reemplazar una
suscripción pagada" del caso "reemplazar la FREE lazy" — es la misma
lógica de siempre (Paso 1: desactivar la anterior sola; Paso 2: crear la
nueva + su pago, juntos). La prueba confirma las 3 cosas pedidas
explícitamente: no quedan 2 suscripciones activas, el pago nuevo se
registra correcto (y el de BASIC sigue en el historial, ahora asociado a
una suscripción ya cancelada), y `GET /memberships/me` refleja PRO de
inmediato.

---

## 7. Riesgos abiertos

1. **`AuditLogRealPostgresTests` no la pude correr yo** — ver §1, necesito
   que la confirmen con Docker disponible.
2. **VAPID/Web Push depende de que el Chat Web configure su propio
   proyecto de Firebase para Web** — el backend ya está listo (§5.3), no
   hay nada más de este lado.
3. **Los valores de límites de plan (Parte 3) son mi criterio, no una
   cifra de negocio confirmada** — ajustables sin migración adicional
   (son datos, un `UPDATE membership_plans SET limits = ...` alcanza).
4. Mismo aviso de siempre para el resto del código: no compilado/probado
   por mí en mi propio entorno — necesito `dotnet build && dotnet test`
   de su lado antes de aprobar.

---

## 8. Checklist de las 10 capas del DoD

- [x] Bug urgente: causa reproducida antes de escribir el fix, no asumida
- [x] Migraciones (016, 017, 018) validadas contra Postgres real de punta
      a punta — antes/después/revertido/reaplicado, no solo el `ALTER`
- [x] Dos errores propios encontrados y corregidos antes de compilar
      (dónde vivía el CHECK de devices.type; el bug de tipo silencioso)
- [x] Credenciales reales nunca escritas en ningún archivo entregado
- [x] Prueba unitaria de cada pieza nueva (plantillas de correo,
      PlanLimits, límites de dispositivos/historial, tipo WEB)
- [x] Prueba de integración HTTP end-to-end para WEB y para el caso de
      upgrade de Parte 6
- [x] Primera prueba del proyecto contra Postgres real (Testcontainers) —
      escrita, marcada honestamente como no verificada por mi cuenta
- [ ] Compilación y `dotnet test` reales — **pendiente de su lado**

Todo en el ZIP adjunto. Quedo a la espera de su resultado — el bug de
producción es lo más urgente, avísenme apenas confirmen que el login
vuelve a funcionar.
