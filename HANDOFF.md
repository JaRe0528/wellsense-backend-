# HANDOFF.md — Chat Backend (.NET), Bloque 6: Memberships + Payments

> Entregable del **Bloque 6**. Requiere Bloque 5 cerrado (ya lo está, 80/80
> reales). Cubre el catálogo de planes que la Web ya está esperando,
> contratar/cambiar de plan con cobro real, y el historial de pagos.

---

## 0. Aviso operativo (se mantiene)

Sigo sin salida de red hacia NuGet en mi propio entorno de trabajo — no pude
correr `dotnet build`/`dotnet test` aquí. Lo que sí hice en este bloque:

- **Repetí el mismo bug de Bloque 4 en dos columnas más** (`subscriptions.status`,
  `payments.status`), lo encontré ANTES de escribir lógica de negocio encima,
  y lo demostré con `INSERT`s reales contra Postgres — ver §2.
- **Demostré empíricamente, no solo razoné, por qué el orden de escritura
  de `SubscribeToPlanCommandHandler` tenía que partirse en dos llamadas**:
  corrí la secuencia "insertar antes de cancelar" dentro de una transacción
  real y confirmé que Postgres la rechaza — ver §3.
- **Encontré y corregí yo mismo un bug real en mi primer borrador** antes de
  que llegara a ningún lado: el handler de suscripción llamaba al gateway
  de pago DOS veces (hubiera cobrado la tarjeta dos veces) — ver §3.
- Actualicé `IWellSenseDbContext` y sus 3 implementadores en el mismo cambio,
  antes de escribir cualquier lógica — sin repetir el blindspot que se
  quedó documentado tras Bloque 4.
- Los 3 barridos automatizados de siempre, limpios sobre las ~35 rutas
  nuevas/tocadas de este bloque.

---

## 1. Decisión nueva de este bloque: Stripe como pasarela de pago

`01-ARQUITECTURA-Y-STACK.md` nunca decidió una pasarela de pago — no estaba
en el stack original. Elegí **Stripe**, justificado en el propio código
(`StripePaymentGateway.cs`): soporta MXN nativamente, opera en México, y su
modelo de tokenización (Stripe.js en el cliente, el backend nunca toca el
número de tarjeta) es exactamente el requisito de PCI-DSS que
`IPaymentGateway` ya exige por diseño. **Es una decisión nueva de este
bloque, no del documento maestro — si el chat de arquitectura/orquestador
ya tenía otra pasarela en mente (Conekta, Mercado Pago), avísenme y la
cambio; el único punto de acoplamiento real es `StripePaymentGateway.cs`,
todo lo demás (`IPaymentGateway`, los handlers, los endpoints) es agnóstico
de cuál pasarela hay detrás.**

Implementé la integración real contra el SDK oficial `Stripe.net`, no un
stub — mismo criterio que FCM (Bloque 5). **Aviso de honestidad**: la forma
exacta de la API de Stripe.net (`PaymentIntentCreateOptions`, `Expand`,
`LastPaymentError`) la escribí de memoria con mi mejor confianza, pero no
la pude compilar contra el paquete real — es la pieza de este bloque con
más probabilidad de necesitar un ajuste una vez que corran `dotnet build`
con una cuenta de Stripe sandbox de verdad. Si algo de la forma exacta del
SDK no compila, es un ajuste de esa clase puntual, no del diseño alrededor.

---

## 2. El mismo bug de Bloque 4, ahora en `subscriptions`/`payments`

Ya había flagueado en el HANDOFF de Bloque 4 que `Subscription.Status` y
`Payment.Status` seguían usando `HasConversion<string>()` genérico
(`Enum.ToString()` → `"Active"`, `"Approved"`), mientras el `CHECK` de la BD
exige `'ACTIVE'`, `'APPROVED'` (mayúsculas). Este bloque escribe a esas
columnas por primera vez, así que lo corregí antes de tocar nada más —
mismo patrón que `MembershipPlanConfiguration.Code` (que sí ya estaba bien
desde Bloque 1: `v.ToString().ToUpperInvariant()` / `Enum.Parse(...,
ignoreCase: true)`, más simple que el switch completo que usé para
`Measurement.Type` porque aquí los nombres del enum SÍ coinciden con el
`CHECK` salvo por mayúsculas).

Demostrado contra Postgres real, mismo patrón que Bloque 4:

```
--- WRONG format ('Active') ---
ERROR:  new row for relation "subscriptions" violates check constraint "subscriptions_status_check"
--- CORRECT format ('ACTIVE') ---
INSERT 0 1
--- mismo resultado para payments.status ('Approved' falla, 'APPROVED' pasa) ---
```

---

## 3. El diseño de `SubscribeToPlanCommandHandler` — dos hallazgos propios

**Hallazgo 1 — mi primer borrador cobraba dos veces.** Al escribir el
camino "plan pago aprobado", tenía una segunda llamada a
`paymentGateway.ChargeAsync(...)` más abajo en el mismo método (residuo de
una reestructuración a medio hacer). Lo atrapé releyendo el handler antes
de darlo por terminado — el resultado del ÚNICO cobro real se guarda en una
variable (`charge`) y se reutiliza; el gateway se llama **exactamente una
vez** por invocación, verificado explícitamente en
`SubscribeToPlanCommandHandlerTests` (`gateway.Charges.Should().ContainSingle()`).

**Hallazgo 2 — por qué "cancelar la vieja + crear la nueva" no puede ir en
un solo `SaveChanges`.** `ux_subscriptions_one_active_per_user` es un
índice único **no diferible** (se evalúa por sentencia, no al final de la
transacción). No hay garantía documentada de que EF Core emita el UPDATE de
la fila vieja (Active→Canceled) antes que el INSERT de la fila nueva
(Active) dentro de una misma llamada a `SaveChanges`, cuando son dos filas
del mismo tipo sin relación de FK entre sí. Lo demostré, no solo lo razoné
— corrí la secuencia contraria a mano dentro de una transacción real:

```sql
BEGIN;
INSERT INTO subscriptions(...) VALUES (..., 'ACTIVE');  -- la nueva, ANTES
UPDATE subscriptions SET status='CANCELED' WHERE id=...;  -- la vieja, DESPUÉS
COMMIT;
```
```
ERROR:  duplicate key value violates unique constraint "ux_subscriptions_one_active_per_user"
```

Por eso el handler parte la escritura en dos llamadas secuenciales
explícitas: primero confirma que la vieja ya no está activa (sola), después
crea la nueva (junto con el `Payment`, si hubo cobro — esa combinación SÍ
es segura en una sola llamada porque hay una FK real
`Payment.SubscriptionId → Subscription.Id`, y para relaciones de FK EF Core
sí garantiza el orden). **Costo aceptado**: un crash exactamente entre esas
dos llamadas dejaría al usuario sin suscripción activa por un instante —
se autorrepara solo en el siguiente `GetMyMembership` (lazy-crea FREE), a
costa de perder momentáneamente el plan pago hasta que reintente. Ventana
extremadamente estrecha (dos escrituras casi instantáneas), documentada
como riesgo en vez de resuelta con una transacción distribuida que nadie
pidió para este bloque.

---

## 4. Qué quedó armado

### Endpoints

**Memberships** (`api/v1/memberships`):

| Método | Ruta | Auth | Request | Response | Errores |
|---|---|---|---|---|---|
| GET | `/plans` | No | — | 200 `[{id, code, name, priceCents, currency}]` | — |
| GET | `/me` | Sí | — | 200 `{subscriptionId, planCode, planName, status, startedAt, endsAt}` — nunca 404 | — |
| POST | `/subscribe` | Sí | `{planCode, paymentMethodToken?, idempotencyKey}` | 200 igual forma que `/me` + `paymentId?` | 400 validación/`PAYMENT_METHOD_REQUIRED`, 402 `PAYMENT_DECLINED`, 404 `PLAN_NOT_FOUND`, 503 `PAYMENT_GATEWAY_NOT_CONFIGURED` |
| POST | `/cancel` | Sí | — | 204 | — |

**Payments** (`api/v1/payments`):

| Método | Ruta | Auth | Response |
|---|---|---|---|
| GET | `/me` | Sí | 200 `[{id, planCode, amountCents, currency, status, cardBrand, cardLast4, createdAt}]` |

`planCode`/`status` usan el mismo vocabulario que el `CHECK` de la BD
(`FREE`/`BASIC`/`PRO`/`PROFESSIONAL`, `ACTIVE`/`CANCELED`/`EXPIRED`,
`APPROVED`/`DECLINED`) en ambos sentidos.

### Decisiones tomadas

- **`GET /plans` es público** (`AllowAnonymous`) — es contenido de una
  página de precios normal, no información de cuenta de nadie. La Web
  puede mostrarlo sin sesión.
- **`GET /me` nunca da 404** — mismo patrón get-or-create perezoso que
  `GetMyProfile` (Bloque 3): todo usuario "tiene" una membresía siempre; si
  nunca contrató nada, se le crea una suscripción FREE en el primer
  llamado, sin tocar `RegisterCommandHandler` (Bloque 2, cerrado).
- **"Cancelar" siempre significa "volver a FREE"** — no existe en este
  modelo un estado "sin ninguna suscripción activa". `CancelSubscriptionCommandHandler`
  reutiliza `SubscribeToPlanCommand` vía MediatR en vez de duplicar la
  lógica de reemplazo.
- **El monto a cobrar SIEMPRE lo decide el servidor** a partir de
  `membership_plans.price_cents`/`currency` — el cliente nunca manda un
  monto. Aceptar un monto del cliente permitiría pagar $1 por un plan de
  $399 con un cliente modificado.
- **`idempotencyKey` es del cliente**, mismo idioma que `requestId` en
  `/sync` (Bloque 4) — se pasa directo al `RequestOptions.IdempotencyKey`
  de Stripe, que deduplica del lado de la pasarela ante reintentos de red.
  No se agregó una columna nueva para rastrearlo del lado nuestro —
  suficiente con lo que Stripe ya garantiza.
- **Un plan FREE nunca genera fila en `payments`** — el `CHECK
  (amount_cents > 0)` de la BD literalmente no lo permite; confirmado con
  el mismo diseño (`if (plan.PriceCents > 0)` antes de tocar `payments` en
  absoluto).
- **Una suscripción paga dura 1 mes desde que se activa**
  (`EndsAt = StartedAt.AddMonths(1)`) — decisión explícita, documentada en
  el propio handler. **Este bloque NO implementa el job que renueva o
  degrada a FREE cuando `EndsAt` ya pasó** — mismo tipo de alcance que la
  decisión de zona horaria del Bloque 3: se deja la decisión y el dato
  listo, no se construye el job que nadie pidió todavía.
- **Se preserva el historial de suscripciones** (cada cambio de plan crea
  una fila nueva, nunca sobreescribe la anterior) en vez de reutilizar la
  misma fila — consistente con lo que sugiere el propio índice único
  parcial de la BD (uno ACTIVO a la vez, no uno solo para siempre).

---

## 5. Qué pruebas existen

Unitarias (EF InMemory):
- `Memberships/SubscribeToPlanCommandHandlerTests` (la más importante): FREE
  nunca llama al gateway ni crea `Payment`; plan pago sin token lanza
  `PAYMENT_METHOD_REQUIRED`; aprobado llama al gateway **exactamente una
  vez** y liga el `Payment` a la nueva suscripción; rechazado crea el
  `Payment` sin ligar y nunca toca `subscriptions`; cambiar de plan
  desactiva el anterior y deja **solo uno** activo conservando el
  historial; código de plan inexistente lanza `PLAN_NOT_FOUND`.
- `Memberships/ListPlansAndGetMyMembershipTests`: catálogo ordenado por
  precio; lazy-creación de FREE en el primer llamado; el segundo llamado
  no duplica.
- `Memberships/CancelSubscriptionCommandHandlerTests`: verifica tanto QUÉ
  comando manda (`SubscribeToPlanCommand` apuntando a FREE) como el
  resultado real en BD tras pasar por el handler real, sin volver a cobrar.
- `Payments/ListMyPaymentsQueryHandlerTests`: aislamiento entre usuarios,
  incluye tanto aprobados como rechazados, orden más-reciente-primero.

Integración HTTP end-to-end (`CustomWebApplicationFactory`, extendida en
este bloque):
- Se agregó **semilla de los 4 planes** al store InMemory de pruebas (la
  migración 012 real los inserta en Postgres vía DDL; un InMemory nuevo
  arranca vacío — sin esto, cualquier prueba de `/memberships/*` fallaría).
- Se agregó `FakePaymentGateway` como reemplazo de `IPaymentGateway`
  (expuesto como propiedad pública de la factory, igual que
  `CapturedEmails`) — sin esto, cualquier intento de cobro real recibiría
  503 `PAYMENT_GATEWAY_NOT_CONFIGURED` en pruebas.
- `Integration/MembershipsFlowEndpointTests`: catálogo sin auth, `/me`
  lazy-FREE, contratar plan pago aprobado (activa + registra pago), tarjeta
  rechazada (402, la membresía activa NO cambia), falta el token (400),
  cancelar vuelve a FREE, `/me` y `/subscribe` sí exigen auth pero
  `/plans` no.

---

## 6. Qué necesita Web/Android de este bloque

- **Contratos exactos**: tabla de §4. El monto SIEMPRE lo calcula el
  servidor — el cliente solo manda `planCode`.
- **`paymentMethodToken` viene del SDK de Stripe en el cliente** (Stripe.js
  en Web, el SDK de Stripe para Android/Kotlin) — el backend nunca acepta
  ni espera datos de tarjeta en claro.
- **`idempotencyKey` debe generarse una vez por intento de compra y
  reenviarse igual en reintentos** — mismo patrón que `requestId` en
  `/sync/measurements` (Bloque 4).
- **`GET /memberships/plans` es público** — la página de precios puede
  mostrarse sin que el usuario haya iniciado sesión.
- **`GET /memberships/me` nunca da 404** — un usuario recién registrado ya
  tiene "membresía FREE" desde el primer `GET`, sin ningún paso adicional.

---

## 7. Riesgos abiertos

1. **Stripe.net sin compilar contra el paquete real** — ver §1, la pieza
   con más probabilidad de necesitar ajuste de forma (no de diseño) una vez
   que corran `dotnet build` con acceso real a NuGet.
2. **Ventana de carrera estrecha en `SubscribeToPlanCommandHandler`** entre
   sus dos `SaveChanges` secuenciales — ver §3, documentada y aceptada, no
   resuelta con una transacción distribuida.
3. **No hay job de renovación/expiración de suscripciones pagas** — `EndsAt`
   se calcula y se guarda, pero nada actúa sobre él todavía cuando pasa.
   Candidato natural para un futuro bloque de jobs programados.
4. **Elección de Stripe pendiente de confirmación** del chat de
   arquitectura/orquestador — ver §1.
5. Mismo aviso de siempre: no compilado/probado por mí en mi propio
   entorno — necesito `dotnet build && dotnet test` de su lado antes de
   aprobar.

---

## 8. Checklist de las 10 capas del DoD

- [x] Validador (FluentValidation) en cada comando
- [x] Prueba unitaria de cada handler, incluyendo los 2 hallazgos propios
      (cobro único, orden de escritura) verificados explícitamente
- [x] Prueba de integración HTTP end-to-end de los 5 endpoints nuevos
- [x] Documentación de API (Swagger, `[ProducesResponseType]`, incluyendo
      402/503)
- [x] Manejo de errores consistente (`PaymentDomainException` nueva,
      agregada al middleware global)
- [x] Bug heredado de Bloque 4 encontrado y corregido antes de escribir
      lógica encima — demostrado contra Postgres real
- [x] Decisión de diseño (orden de escritura no atómico) validada
      empíricamente contra Postgres real, no solo razonada
- [ ] Compilación y `dotnet test` reales — **pendiente de tu lado**, ver §0

Quedo a la espera de tu luz verde (o correcciones) antes del siguiente
bloque.

---

## 9. Nota de fix post-entrega — `dotnet test` real corrido, 22 fallos por un solo bug en el fixture de pruebas

Corrí `dotnet build && dotnet test` con acceso real a NuGet (§0/§7.5
quedan resueltos: la solución sí compila y las 78 pruebas que no
dependían de esto ya pasaban). De 100 pruebas, 22 fallaban — todas por la
MISMA causa raíz, no 22 bugs distintos.

**Síntoma**: cualquier endpoint que tocara la base (`/auth/register`,
`/memberships/plans`, etc.) devolvía 500, con este error de EF Core en el
log:

```
InvalidOperationException: A call was made to 'ConfigureWarnings' that
changed an option that must be constant within a service provider, but
Entity Framework is not building its own internal service provider...
```

De ahí en cascada, cualquier prueba que dependiera de un `/register`
exitoso (la enorme mayoría) fallaba con `KeyNotFoundException` al buscar
el email en `CapturedEmails` — el registro nunca llegaba a completarse.

**Causa raíz**: el bloque de siembra de `MembershipPlans` agregado en este
bloque (en `CustomWebApplicationFactory.ConfigureTestServices`) creaba su
propio `WellSenseDbContext` armando un `DbContextOptionsBuilder<WellSenseDbContext>()`
a mano:

```csharp
using (var seedContext = new WellSenseDbContext(new DbContextOptionsBuilder<WellSenseDbContext>()
    .UseInMemoryDatabase(DbName)
    .UseInternalServiceProvider(inMemoryServiceProvider)
    .Options))
```

Aunque apuntaba al mismo `DbName` y al mismo `inMemoryServiceProvider` que
usa la app, este builder se construyó **por fuera** de
`services.AddDbContext<WellSenseDbContext>(...)`. `AddDbContext` le agrega
a las opciones cosas adicionales (en particular, el `ApplicationServiceProvider`
real de la app) que un builder armado a mano nunca recibe. Como ambos
`DbContextOptions` (el de la app y el del seed) comparten el MISMO
`ServiceProvider` interno de InMemory, EF exige que ciertas opciones sean
idénticas en todo uso de ese proveedor compartido — la discrepancia
disparaba la excepción de arriba en la primera consulta real a la base
(`db.Users` dentro de `RegisterCommandHandler`, `db.MembershipPlans`
dentro de `ListPlansQueryHandler`, etc.), tumbando el endpoint completo.

**Fix aplicado**: en vez de armar un `DbContextOptionsBuilder` paralelo,
mover la siembra a un override de `CreateHost(IHostBuilder builder)` —
punto de extensión estándar de `WebApplicationFactory` pensado exactamente
para esto. Ahí se resuelve un `WellSenseDbContext` real desde
`host.Services.CreateScope()` (el contenedor de DI ya construido), la
misma tubería que usa cualquier request real, así que comparte
exactamente la misma configuración sin builders duplicados:

```csharp
protected override IHost CreateHost(IHostBuilder builder)
{
    var host = base.CreateHost(builder);

    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WellSenseDbContext>();
    if (!db.MembershipPlans.Any())
    {
        db.MembershipPlans.AddRange(/* los mismos 4 planes de antes */);
        db.SaveChanges();
    }

    return host;
}
```

Solo se tocó `tests/WellSense.Tests/Integration/CustomWebApplicationFactory.cs`
— nada de `Program.cs`, de los handlers, ni de las pruebas mismas. §7.5 y
§8 (última fila del checklist) quedan resueltos con este fix; el resto de
los riesgos abiertos (§7.1–§7.4) siguen en pie tal como se documentaron
arriba.
