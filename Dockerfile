# Etapa 1: compilar
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiamos solo los .csproj primero (aprovecha el cache de Docker si el
# código cambia pero las dependencias no)
COPY WellSense.sln ./
COPY src/WellSense.Api/WellSense.Api.csproj src/WellSense.Api/
COPY src/WellSense.Application/WellSense.Application.csproj src/WellSense.Application/
COPY src/WellSense.Domain/WellSense.Domain.csproj src/WellSense.Domain/
COPY src/WellSense.Infrastructure/WellSense.Infrastructure.csproj src/WellSense.Infrastructure/
RUN dotnet restore src/WellSense.Api/WellSense.Api.csproj

# Ahora sí copiamos todo el código y publicamos
COPY . .
RUN dotnet publish src/WellSense.Api/WellSense.Api.csproj -c Release -o /app/publish --no-restore

# Etapa 2: imagen final, mucho más liviana (sin el SDK completo)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render inyecta la variable PORT en tiempo de ejecución — Kestrel debe
# escuchar en ESE puerto, no en uno fijo
ENTRYPOINT ["/bin/sh", "-c", "dotnet WellSense.Api.dll --urls=http://+:${PORT:-10000}"]
