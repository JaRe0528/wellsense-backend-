# HANDOFF.md — Chat Backend (.NET), Bloque 1

> Entregable del **Bloque 1: Setup del proyecto**. Cubre estructura de
> solución, Swagger, Serilog, middleware global de excepciones, y las 13
> migraciones EF Core (001→013) aplicadas y revertidas contra Postgres 16
> real. **No incluye Auth** — eso es el Bloque 2, pendiente de luz verde.

---

## 0. Aviso operativo — léelo antes de aprobar este bloque

El entorno en el que trabajé **no tiene salida de red hacia NuGet**
(`api.nuget.org` responde 403 vía el proxy de egreso — solo están permitidos
dominios de paquetes de Python/Node/Rust/apt, no NuGet). Confirmé esto
intentando compilar un proyecto ASP.NET Core vacío sin ninguna dependencia de
terceros: falla igual, porque incluso el framework base se resuelve por NuGet.

Esto significa:

- **No pude correr `dotnet restore` / `dotnet build` / `dotnet ef database
  update`** en este entorno. El código que te entrego no ha sido compilado.
- **Sí pude** instalar PostgreSQL 16 (repos oficiales de Ubuntu, sin
  necesidad de NuGet) y validar el contenido real de las 13 migraciones
  ejecutándolas como SQL puro contra una base de datos real — ver §3.
- Las migraciones EF Core están escritas como `migrationBuilder.Sql(...)`
  (SQL crudo) en vez de `CreateTable`/`CreateIndex` tipados, precisamente
  para que lo que corre en Up()/Down() sea **byte-idéntico** al DDL que
  HANDOFF-DB ya validó (69 sentencias, 0 errores) — no una traducción vía
  Fluent API que yo no podía verificar compilando.
- **Riesgo abierto que te dejo explícito**: no generé los archivos
  `*.Designer.cs` ni `WellSenseDbContextModelSnapshot.cs` que `dotnet ef`
  normalmente autogenera junto a cada migración, porque no tengo el tooling
  para producirlos correctamente a mano sin arriesgar inconsistencias. Antes
  de considerar este bloque 100% cerrado:
  1. Corre `dotnet restore && dotnet build` con acceso real a NuGet y
     corrígeme cualquier error de compilación (esperable: nombres de usings,
     alguna firma de API que haya cambiado de versión).
  2. Corre `dotnet ef migrations add InitialSnapshot` una vez para que EF
     genere su propio snapshot del modelo actual (puede detectar que "no hay
     cambios" si el modelo Fluent ya coincide con las 13 migraciones — en ese
     caso solo te faltará el Designer/Snapshot, no una migración nueva).
  3. Como cross-check, corre `dotnet ef migrations script` y compara contra
     los archivos en `sql-migrations/*.sql` de este entregable (ya validados
     de forma independiente).

Con esta salvedad, todo el resto de este documento describe lo que sí puedo
afirmar con confianza porque lo ejecuté de verdad.

---

## 1. Qué quedó armado

```
wellsense-backend/
  WellSense.sln
  src/
    WellSense.Domain/            → entidades POCO de las 26 tablas, sin dependencias externas
    WellSense.Application/       → csproj listo (FluentValidation, MediatR) — vacío de casos de uso, eso es Bloque 2+
    WellSense.Infrastructure/
      Persistence/
        WellSenseDbContext.cs    → 26 DbSets, snake_case, ApplyConfigurationsFromAssembly
        Configurations/          → IEntityTypeConfiguration por módulo (Identity, Profile, Device, resto)
        Migrations/              → 13 migraciones EF (001_InitUsers → 013_DashboardReadModels)
      DependencyInjection.cs     → AddInfrastructure(), UseSnakeCaseNamingConvention()
    WellSense.Api/
      Program.cs                 → Serilog, Swagger + Bearer scheme, exception middleware, DI
      Middleware/ExceptionHandlingMiddleware.cs
      appsettings.json / appsettings.Development.json
  tests/
    WellSense.Tests/             → smoke test del modelo de EF (26 entidades registran sin excepción)
  sql-migrations/                → los mismos 13 Up/Down como .sql puro — usados para la validación real
  README.md                      → repite el aviso de §0
```

No hay endpoints todavía — Bloque 1 es setup + esquema. Regla de dependencia
respetada: `Api → Application → Domain`, `Infrastructure → Application/Domain`
(csproj con `ProjectReference` en ese sentido; `Domain.csproj` no referencia
ningún paquete NuGet).

---

## 2. Decisiones tomadas en este bloque

- **snake_case + tablas en plural**: `UseSnakeCaseNamingConvention()` de
  EFCore.NamingConventions en `AddInfrastructure`, más `ToTable("...")`
  explícito en cada `IEntityTypeConfiguration` (no confío solo en la
  convención automática para los nombres de tabla, solo para columnas — así
  el mapeo es explícito y auditable).
- **Migraciones como SQL crudo, no Fluent API tipada**: decisión forzada por
  §0, pero la mantendría de todos modos para `006_MeasurementsPartitioned` y
  `013_DashboardReadModels` — EF Core no modela particionado nativo de
  Postgres ni vistas materializadas, así que esos dos SIEMPRE iban a requerir
  `migrationBuilder.Sql(...)`. Extendí el mismo patrón a las 11 migraciones
  restantes por consistencia y para eliminar el riesgo de traducción.
- **Enums de dominio vs. `CHECK` de texto en BD**: cada enum de C# (`UserRole`,
  `DeviceType`, `DeclaredStressLevel`, etc.) se mapea con `HasConversion(...)`
  a exactamente los literales de texto que el `CHECK` de Postgres permite
  (ej. `DeclaredStressLevel.MuyAlto` ↔ `'MUY_ALTO'`). Si HANDOFF-DB cambia un
  `CHECK` en el futuro, hay que actualizar la conversión aquí también — no es
  automático.
- **`measurements` con PK compuesta `(id, recorded_at)`**: reflejado en la
  configuración de EF (`HasKey(x => new { x.Id, x.RecordedAt })`) porque
  Postgres lo exige por ser tabla particionada por rango sobre `recorded_at`.
- **`Metadata`/`factors`/`input`/`output`/etc. como `string` con
  `HasColumnType("jsonb")`**: no usé un tipo fuerte para JSON en el Domain en
  este bloque para no acoplar el modelo de dominio a una librería de
  serialización todavía — cuando el Bloque de ML/Notifications llegue,
  decidiremos si vale la pena tipar esos payloads.
- **Seed de los 4 planes de membresía** (`FREE`, `BASIC`, `PRO`,
  `PROFESSIONAL`) incluido dentro de la migración `012_Billing`, tal como
  pide el DoD de no dejar tablas de catálogo vacías.
- **Partición inicial de `measurements`**: creé
  `measurements_2026_08` (agosto 2026, mes de este entregable) como partición
  de arranque dentro de `006_MeasurementsPartitioned`, para que la tabla
  particionada no quede sin ninguna partición hija al desplegar. Las
  particiones futuras las gestiona `pg_partman` (Chat DevSecOps, según
  HANDOFF-DB riesgo #5) — esto no debe convertirse en una migración EF manual
  mes a mes.

**Pendiente para el Bloque 3 (Users + Profile), no resuelto aquí**: la
decisión de zona horaria para calcular el "día" de `wellness_scores` /
`stress_scores` (riesgo #2 de HANDOFF-DB) se toma explícitamente en ese
bloque, no en este — este bloque solo crea las columnas `date` tal como
están en el DDL.

---

## 3. Validación — qué corrí de verdad y qué resultado dio

Instalé PostgreSQL 16 y .NET SDK 8.0 vía `apt` (únicos paquetes con red
disponible en este entorno; nota: la arquitectura pide .NET 9 — el SDK 9 no
está en los repos de Ubuntu 24.04 todavía, así que no pude siquiera confirmar
que .NET 9 esté disponible por este canal; los `.csproj` apuntan a `net9.0`
como pide la arquitectura, pendiente de que tú confirmes su instalación
donde vayas a compilar).

**Procedimiento**: extraje el DDL de cada una de las 13 migraciones a un
archivo `.sql` (`sql-migrations/NNN_up.sql` y `NNN_down.sql`, idéntico al
contenido embebido en cada clase `Migration` de C#) y:

1. Apliqué `001_up.sql` → `013_up.sql` en orden, con `ON_ERROR_STOP=1`, contra
   una base de datos Postgres 16 vacía (`wellsense_test`).
   **Resultado: las 13 migraciones aplicaron limpio, 0 errores.**
2. Confirmé el recuento: **27 tablas base** (las 26 del modelo + la
   partición hija `measurements_2026_08`, que Postgres cuenta como tabla) y
   **3 vistas materializadas** (`user_daily_summary/weekly/monthly`).
3. Corrí los mismos chequeos funcionales que HANDOFF-DB ya había validado,
   para confirmar que sobrevivieron el viaje a través de las migraciones
   separadas (no solo como un único script): `INSERT` con `role='professional'`
   rechazado, `role='admin'` aceptado, `REFRESH MATERIALIZED VIEW
   user_daily_summary` sin error.
4. Revertí `013_down.sql` → `001_down.sql` **en orden inverso**, también con
   `ON_ERROR_STOP=1`. **Resultado: rollback limpio, 0 errores, 0 tablas
   remanentes en `public`** al terminar — confirma que el `Down()` de cada
   migración es simétrico a su `Up()` y que el orden de dependencias
   (`FKs` primero destruidas en el orden correcto vía `CASCADE`) es correcto.

Esto es una validación real de la parte SQL, ejecutada dos veces (ida y
vuelta) contra un motor real — no es una simulación. Lo que **no** pude
validar es que `dotnet ef database update` (el comando real que usarás en
CI/local) aplique estas mismas migraciones sin fricción, porque esa
herramienta requiere el `WellSenseDbContextModelSnapshot.cs` que no generé
(ver §0, punto de riesgo).

---

## 4. Qué necesita el resto del equipo de este bloque

- **Chat DevSecOps**: la migración `006` asume que `pg_partman` se instala
  como extensión en el contenedor de Postgres (heredado de HANDOFF-DB riesgo
  #5) — no lo instalé ni lo configuré aquí, solo dejé la partición inicial de
  agosto 2026 creada a mano dentro de la migración. Coordinar quién crea las
  particiones de septiembre en adelante.
- **Cualquier chat que dependa de este backend (Web/Android)**: todavía no
  hay contratos de API que consumir — Bloque 1 no expone endpoints. Los
  contratos de Auth (Bloque 2) vendrán en el próximo HANDOFF.
- **Tú (orquestador/usuario)**: correr `dotnet restore && dotnet build` en un
  entorno con acceso a NuGet real antes de aprobar este bloque como cerrado,
  por las razones de §0.

---

## 5. Riesgos abiertos de este bloque

1. **Snapshot/Designer.cs de EF ausentes** (§0) — el riesgo más importante,
   bloquea que `dotnet ef migrations add` funcione normalmente hasta que se
   genere una vez con tooling real.
2. **.NET 9 no confirmado disponible** en el entorno de build real — los
   `.csproj` apuntan a `net9.0` (Npgsql 9, EF Core 9) siguiendo la
   arquitectura aprobada, pero no pude instalar ni probar ese SDK aquí (solo
   8.0 estaba en los repos de Ubuntu accesibles). Si tu pipeline de CI/local
   tampoco tiene el SDK 9 a mano todavía, avísame y bajo el target a `net8.0`
   temporalmente — es un cambio de una línea por `.csproj`, no de arquitectura.
3. **Conversión enum↔texto duplicada respecto al `CHECK` de la BD**: si en
   algún momento se agrega un valor a un `CHECK` (ej. un nuevo `role`), hay
   que recordar actualizar tanto la migración SQL como el `HasConversion` en
   la configuración de EF correspondiente — no hay una única fuente de verdad
   automática entre ambos hoy.
4. **Partición mensual manual**: si el Bloque de DevSecOps no tiene lista la
   automatización de `pg_partman` antes del segundo mes de operación real,
   `measurements` de septiembre 2026 en adelante no tendrá partición hija y
   los `INSERT` fallarán (Postgres no enruta filas fuera de rango a ninguna
   partición por defecto). No es responsabilidad de este bloque resolverlo,
   pero si nadie más lo toma antes de esa fecha, hay que escalarlo.

---

## 6. Checklist de las 10 capas del DoD (capas aplicables a este bloque)

- [x] Estructura Clean Architecture (Api/Application/Domain/Infrastructure/Tests)
- [x] Persistencia (DbContext, snake_case, 26 entidades mapeadas)
- [x] Migraciones reales, aplicadas y revertidas contra Postgres real
- [x] Logging estructurado (Serilog) con enriquecimiento y sin loguear datos sensibles
- [x] Manejo global de errores (middleware → ProblemDetails RFC 7807)
- [x] Documentación de API (Swagger/Swashbuckle configurado, con esquema Bearer)
- [ ] Validadores (FluentValidation) — no aplica todavía, no hay endpoints en este bloque
- [ ] Pruebas de endpoints — no aplica todavía, no hay endpoints en este bloque
- [ ] Auth — Bloque 2, pendiente de luz verde
- [ ] Compilación verificada con `dotnet build` real — **pendiente de tu lado**, ver §0

Quedo a la espera de tu luz verde (o correcciones) antes de empezar el
Bloque 2 (Auth completo, web + móvil).
