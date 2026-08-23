using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Devices.ListMyDevices;

public class ListMyDevicesQueryHandler(IWellSenseDbContext db) : IRequestHandler<ListMyDevicesQuery, IReadOnlyList<DeviceResult>>
{
    public async Task<IReadOnlyList<DeviceResult>> Handle(ListMyDevicesQuery request, CancellationToken ct)
    {
        // Deliberadamente NO se llama .ToString() del enum dentro de un .Select() que EF
        // traduce a SQL: Device.Type/Status usan un HasConversion con lambdas propias
        // (no el <string>() genérico), y EF Core no siempre puede traducir Enum.ToString()
        // sobre una conversión personalizada — puede fallar en tiempo de ejecución contra
        // Postgres real aunque el proveedor InMemory de las pruebas lo tolere sin quejarse
        // (evaluación en cliente más permisiva). Se materializan las entidades completas
        // primero (aquí SÍ es seguro: hidratar el enum desde la columna es exactamente
        // para lo que sirve el value converter) y se proyecta a DTO después, en memoria.
        var devices = await db.Devices
            .Where(d => d.UserId == request.CurrentUserId)
            .OrderByDescending(d => d.PairedAt)
            .ToListAsync(ct);

        return devices
            .Select(d => new DeviceResult(
                d.Id, d.Type.ToString(), d.Model, d.OsVersion, d.AppVersion,
                d.Status.ToString(), d.LastSeenAt, d.PairedAt))
            .ToList();
    }
}
