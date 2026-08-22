# wellsense-backend

Backend de WellSense (.NET) — Chat Backend. Ver `HANDOFF.md` para el
entregable completo del Bloque 1 (setup + migraciones EF Core).

## Nota importante sobre este entorno

Este repositorio fue generado en un entorno de ejecución que **no tiene salida
de red hacia NuGet** (`api.nuget.org` devuelve 403 desde el proxy de egreso).
Por lo tanto:

- No fue posible correr `dotnet restore` / `dotnet build` / `dotnet ef
  database update` en este entorno, ni siquiera para un proyecto ASP.NET Core
  vacío sin dependencias de terceros (el propio framework se resuelve vía
  NuGet).
- Sí fue posible instalar PostgreSQL 16 (repos de Ubuntu) y validar el DDL de
  las 13 migraciones de forma independiente, aplicándolas en orden 001→013 y
  revirtiéndolas en orden 013→001 contra una base de datos real. Ver
  `HANDOFF.md §Validación`.
- El código de este repo (Domain, Infrastructure, Api) está escrito para
  compilar contra .NET 9 / EF Core 9 / Npgsql 9, pero **no ha sido compilado
  en este entorno** — no tengo forma de garantizar que compile sin errores de
  sintaxis menores hasta que corras `dotnet build` con acceso real a NuGet.
  Antes de dar por cerrado este bloque, corre `dotnet restore && dotnet build`
  en tu máquina/CI y corrígeme cualquier error de compilación que aparezca.

## Cómo levantar localmente (una vez tengas acceso a NuGet)

```bash
dotnet restore
dotnet build
dotnet ef database update --project src/WellSense.Infrastructure --startup-project src/WellSense.Api
dotnet run --project src/WellSense.Api
```
