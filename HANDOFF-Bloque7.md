# HANDOFF.md — Chat Backend (.NET), Bloque 7: ML V1 (reglas)

> Entregable del **Bloque 7**. Requiere Bloque 6 cerrado (ya lo está,
> 100/100 reales). El endpoint que consume Measurements/Sleep/Activity y
> produce WellnessScore/StressScore, usando por primera vez de verdad la
> decisión de zona horaria del Bloque 3.

---

## 0. Aviso operativo (se mantiene)

Sigo sin salida de red hacia NuGet en mi propio entorno de trabajo — no pude
correr `dotnet build`/`dotnet test` aquí. Lo que sí hice en este bloque:

- **Cerré el último de los 4 bugs de `HasConversion<string>()` que había
  quedado flagueado desde el HANDOFF de Bloque 4** (`StressScore.Level`) —
  encontrado y corregido antes de escribir lógica encima, demostrado contra
  Postgres real, mismo patrón que las 3 veces anteriores.
- **Apliqué la regla general del Bloque 6** (`IWellSenseDbContext` y sus 3
  implementadores actualizados en el mismo cambio, antes de cualquier
  lógica) y la regla de siembra de pruebas ya corregida (no fue necesario
  sembrar nada nuevo en este bloque, pero confirmé que no rompí el patrón
  existente).
- **Encontré y corregí una inconsistencia real yo mismo, en mi propio
  primer borrador**, antes de que llegara a ningún lado: `GET
  /wellness/me` sin fecha explícita iba a usar "hoy" en UTC como default,
  mientras que `POST /wellness/compute` sin fecha ya usaba "hoy" en la
  zona horaria del usuario — exactamente la inconsistencia que este bloque
  existe para evitar. Corregido moviendo la resolución de "hoy" al handler
  (no al controller), ver §3.
- **Diseñé el motor de puntuación como funciones puras**, separadas del
  handler que hace I/O — permite probar la lógica de reglas exhaustivamente
  sin EF ni mocks, y deja un punto de reemplazo limpio para cuando el
  servicio de ML real (Python/FastAPI) entre en un bloque futuro.
- Validé el flujo completo contra Postgres real: inserciones válidas y el
  `UNIQUE(user_id, date)` rechazando un segundo insert directo — confirma
  por qué el handler tiene que ser buscar-y-actualizar, no insertar a
  ciegas.
- Los 3 barridos automatizados de siempre, limpios sobre las ~25 rutas
  nuevas de este bloque.

---

## 1. El motor de reglas — qué calcula y por qué

**Decisión de alcance explícita**: wellness score usa **sueño + pasos**,
NO frecuencia cardíaca — una "frecuencia cardíaca en reposo" confiable
necesita aislar períodos de baja actividad, un problema en sí mismo que
este bloque no resuelve. El stress score sí usa frecuencia cardíaca (banda
de referencia genérica, sin personalización todavía) porque ahí es donde
más aporta como señal simple para un "V1 de reglas".

- **Componente de sueño** (0-100): ideal 7-9h → 100, se degrada
  gradualmente fuera de ese rango, más pronunciado hacia abajo (dormir
  poco pesa más que dormir de más).
- **Componente de actividad** (0-100): 10,000 pasos/día → 100, escala
  lineal.
- **Wellness score** = promedio de los componentes CON datos — si falta
  uno, no se penaliza como si fuera cero, simplemente se excluye del
  promedio.
- **Componente de estrés por frecuencia cardíaca**: banda genérica
  (≤60bpm → 0, ≥100bpm → 100), interpolación lineal entre bandas.
- **Componente de estrés por sueño**: inverso del componente de sueño del
  wellness (dormir bien reduce estrés).
- **Stress score** = promedio de los componentes de estrés disponibles.
  **Nivel** (`LOW`/`MEDIUM`/`HIGH`) = corte simple en tercios (0-33 /
  34-66 / 67-100) — transparente y fácil de explicarle al usuario.
  **Confianza** = proporción de componentes con datos reales (1.0 si
  ambos, 0.5 si solo uno).
- **Sin ningún dato de ningún tipo ese día** → no se inserta nada, 400
  `INSUFFICIENT_DATA` — nunca se inventa un puntaje "neutral" de relleno.

Cada cálculo se registra en `ml_predictions` (`model_version: "rules-v1"`,
`type: "daily_scores"`) con el input crudo y el output calculado — para
poder explicar "por qué mi puntaje de hoy es X" y, cuando llegue el
servicio de ML real, tener trazabilidad de qué generó cada puntaje
histórico (reglas vs. modelo entrenado).

---

## 2. La zona horaria del Bloque 3, aplicada por primera vez

`LocalDayRange` (nuevo, `Application/Common/`) es la implementación
directa de esa decisión: cualquier código que agrupe measurements/sleep/
activity "por día" pasa por acá, nunca trunca `recorded_at`/`start_at` a
fecha UTC directamente.

- `ForLocalDate(fecha, timezone)` → rango UTC `[inicio, fin)` que
  corresponde a la medianoche-a-medianoche LOCAL de esa fecha.
- `TodayInTimezone(utcNow, timezone)` → la fecha calendario local
  correspondiente a un instante UTC, para resolver "hoy" por defecto.
- `TimeZoneInfo.ConvertTimeToUtc` resuelve DST automáticamente contra la
  tzdata (misma base que usa Postgres) — no hay cálculo de horario de
  verano a mano.
- Si `profiles.timezone` es inválido (no debería pasar, `UpsertMyProfile`
  ya lo valida desde Bloque 3, pero perfiles viejos o datos corruptos son
  un caso borde real), cae a UTC en vez de tumbar el cálculo completo.

Probado explícitamente con el caso que motivó la decisión: una medición
tomada a las 22:30 hora Ciudad de México del día 23, que en UTC ya es
04:30 del día 24 — sin la conversión de zona horaria, un cálculo ingenuo
por fecha UTC la habría atribuido al día equivocado
(`ComputeDailyScoresCommandHandlerTests.Uses_the_users_local_timezone_not_utc...`).

---

## 3. La inconsistencia que encontré en mi propio primer borrador

Al escribir `GET /wellness/me`, mi primer borrador del controller resolvía
el default de fecha así:

```csharp
var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow); // ¡mal!
```

Esto habría hecho que `GET /wellness/me` (sin fecha) y `POST
/wellness/compute` (sin fecha) usaran "hoy" calculado de formas
DISTINTAS para el mismo usuario — exactamente el tipo de bug silencioso
que la decisión del Bloque 3 existe para evitar, y en el peor lugar
posible: el propio endpoint de lectura de los puntajes.

Lo corregí moviendo la resolución de "hoy" al **handler**
(`GetMyDailyScoresQueryHandler`), no al controller — ahí sí tiene acceso al
perfil del usuario y usa el mismo `LocalDayRange.TodayInTimezone` que
`ComputeDailyScoresCommandHandler`. El controller ahora solo pasa
`date: DateOnly?` tal cual, nunca decide un default por su cuenta. Deja
como regla implícita para bloques futuros: la resolución de "hoy" nunca
debe vivir en la capa de Api.

---

## 4. Endpoints listos

**Wellness** (`api/v1/wellness`, todos requieren Bearer):

| Método | Ruta | Request | Response | Errores |
|---|---|---|---|---|
| POST | `/compute` | `{date?}` | 200 `{date, wellness?: {score}, stress?: {score, level, confidence}}` | 400 `INSUFFICIENT_DATA` / validación (fecha futura) |
| GET | `/me` | query `?date=` (opcional) | 200 `{date, wellnessScore?, stressScore?, stressLevel?, stressConfidence?}` — nunca 404 | — |
| GET | `/me/history` | query `?days=` (default 7, tope 90) | 200 `[{date, wellnessScore?, stressScore?, stressLevel?}]` | — |

`stressLevel` usa `LOW`/`MEDIUM`/`HIGH`, mismo vocabulario que el `CHECK`
de la BD.

---

## 5. Decisiones tomadas en este bloque

- **`/compute` es explícito, no automático** — el encargo pedía "el
  endpoint que consuma...produzca...", así que este bloque entrega
  exactamente eso. Auto-disparar el cálculo después de cada `/sync`
  (parecido al evento de SignalR del Bloque 5) es un paso natural
  siguiente, pero no lo hice sin que se pida explícito — hubiera sido
  otra modificación a `SyncMeasurementsCommandHandler` (ya van dos
  bloques tocándolo) sin encargo directo para ésta.
- **Recalculable, no de una sola vez**: si ya existe un puntaje para
  `(usuario, fecha)`, `/compute` lo actualiza en vez de fallar o duplicar
  — necesario porque el sync puede traer datos tardíos de un día ya
  procesado.
- **Sueño se atribuye al día en que TERMINA, no en el que empieza** — una
  sesión de sueño de las 23:00 del día N a las 07:00 del día N+1 cuenta
  para el día N+1 (el día que la persona vivió con ese descanso), no para
  el N.
- **Un componente sin datos se excluye del promedio, nunca se trata como
  cero** — dormir 0 horas registradas de verdad es distinto de "no hay
  ninguna sesión de sueño sincronizada todavía"; solo el primer caso debe
  bajar el puntaje.
- **`ml_predictions` se llena en cada cálculo exitoso** (parcial o
  completo) — decisión de aprovechar una tabla que ya existía desde
  Bloque 1 exactamente para esto, dando trazabilidad de qué datos
  produjeron cada puntaje.

---

## 6. Qué pruebas existen

Unitarias, sin EF (funciones puras):
- `Wellness/DailyScoringRulesTests`: cada regla por separado — rango ideal
  de sueño, penalización asimétrica (dormir poco pesa más que dormir de
  más), tope de actividad en 10,000 pasos, promedio que excluye
  componentes ausentes, bandas de frecuencia cardíaca, corte de nivel en
  tercios, cálculo de confianza.
- `Wellness/LocalDayRangeTests`: conversión medianoche-a-medianoche local
  a rango UTC; **el caso específico de "UTC ya cambió de día, la zona
  local todavía no"**; fallback seguro a UTC ante una zona horaria
  inválida.

Unitarias con EF InMemory:
- `Wellness/ComputeDailyScoresCommandHandlerTests`: sin datos lanza
  `INSUFFICIENT_DATA`; solo pasos calcula wellness pero no stress; pasos +
  sueño + frecuencia cardíaca calculan ambos con confianza 1.0;
  recalcular el mismo día actualiza en vez de duplicar; **atribución
  correcta de una medición a la zona horaria local del usuario, no UTC**.
- `Wellness/GetMyDailyScoresAndHistoryTests`: nulls cuando no hay nada
  calculado; sin fecha resuelve "hoy" en la zona horaria del usuario
  (la misma prueba que hubiera fallado con mi borrador con el bug de §3);
  historial solo trae días con datos reales, aislado por usuario.

Integración HTTP end-to-end (`CustomWebApplicationFactory`):
`Integration/WellnessFlowEndpointTests` — sincroniza mediciones REALES vía
`/sync/measurements` (Bloque 4) y confirma que `/wellness/compute` (este
bloque) las encuentra y calcula un puntaje real — cubre la integración
entre bloques, no solo este en aislamiento. Más: `/me` antes de tener
datos no es un error; calcular sin datos da 400; requiere autenticación.

---

## 7. Qué necesita Web/Android de este bloque

- **Llamar a `POST /wellness/compute` explícitamente** para que un día
  tenga puntaje — no sucede solo. El momento natural: después de un sync
  exitoso, o cuando el usuario abre el dashboard.
- **`GET /wellness/me` nunca da 404** — si los campos vienen `null`, es la
  señal de "todavía no se ha calculado (o no hay suficientes datos)", no
  un error.
- **`POST /wellness/compute` puede dar 400 `INSUFFICIENT_DATA`** — el
  cliente debe mostrar algo como "sincroniza más datos para ver tu
  puntaje de hoy", no tratarlo como un error de red.
- **El "día" siempre se calcula en la zona horaria del perfil del
  usuario** (Bloque 3) — si el cliente muestra "puntaje de hoy" en su
  propia UI, debe coincidir con lo que el backend considera "hoy" para
  ese usuario, no con la fecha del dispositivo si el usuario cambió de
  huso horario recientemente.

---

## 8. Riesgos abiertos

1. **Es un motor de reglas, no un modelo entrenado** — las bandas de
   referencia (pasos, frecuencia cardíaca, sueño) son genéricas, iguales
   para todos los usuarios, no personalizadas. Es exactamente lo que pediste
   ("ML V1 reglas"), documentado explícitamente como punto de partida, no
   como resultado final.
2. **No hay disparo automático tras `/sync`** — ver §5, decisión consciente
   de no tocar `SyncMeasurementsCommandHandler` una tercera vez sin
   encargo explícito.
3. **Los componentes de wellness NUNCA usan frecuencia cardíaca** — decisión
   de alcance explícita (ver §1), no un descuido.
4. **`activity_sessions` no participa en el cálculo de pasos** — solo se
   cuenta (para el log de `ml_predictions`, no para el puntaje) para evitar
   el riesgo de contar los mismos pasos dos veces si un `activity_session`
   y measurements de tipo STEPS se solaparan en el tiempo. Si en el futuro
   se necesita que las sesiones de ejercicio pesen en el wellness score,
   hay que decidir explícitamente cómo evitar ese doble conteo.
5. Mismo aviso de siempre: no compilado/probado por mí en mi propio
   entorno — necesito `dotnet build && dotnet test` de su lado antes de
   aprobar.

---

## 9. Checklist de las 10 capas del DoD

- [x] Validador (FluentValidation) en el comando de cómputo
- [x] Prueba unitaria de cada regla de puntuación por separado (funciones
      puras, sin EF) + de cada handler con EF InMemory
- [x] Prueba de integración HTTP end-to-end que cruza Sync (Bloque 4) → ML
      (este bloque), no solo este bloque aislado
- [x] Documentación de API (Swagger, `[ProducesResponseType]`)
- [x] Manejo de errores consistente (`MlDomainException` nueva, agregada
      al middleware global)
- [x] Bug heredado (el último de los 4 de `HasConversion<string>()`)
      encontrado y corregido antes de escribir lógica encima — demostrado
      contra Postgres real
- [x] Inconsistencia real en mi propio borrador encontrada y corregida
      antes de compilar — documentada con la misma honestidad que los
      bugs de bloques anteriores
- [ ] Compilación y `dotnet test` reales — **pendiente de tu lado**, ver §0

Quedo a la espera de tu luz verde (o correcciones) antes del siguiente
bloque.
