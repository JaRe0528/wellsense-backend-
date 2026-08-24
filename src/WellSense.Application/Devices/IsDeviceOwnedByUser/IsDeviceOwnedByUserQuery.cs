using MediatR;

namespace WellSense.Application.Devices.IsDeviceOwnedByUser;

/// <summary>
/// Chequeo mínimo reutilizable — extraído específicamente para que DeviceCommandHub
/// (Api) pueda verificar propiedad de un dispositivo sin tener que inyectar
/// IWellSenseDbContext directamente en el Hub, lo que rompería la capa de por medio
/// (Api → Application vía MediatR, nunca Api → IWellSenseDbContext directo) que el
/// resto del proyecto respeta consistentemente.
/// </summary>
public record IsDeviceOwnedByUserQuery(Guid UserId, Guid DeviceId) : IRequest<bool>;
