using System.Text.Json;
using FluentValidation;
using WellSense.Domain.Devices;

namespace WellSense.Application.Devices.Commands.IssueDeviceCommand;

public class IssueDeviceCommandCommandValidator : AbstractValidator<IssueDeviceCommandCommand>
{
    public IssueDeviceCommandCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => DeviceCommandTypeExtensions.TryParseWireString(t, out _))
            .WithMessage("type debe ser uno de: START_MONITORING, STOP_MONITORING, CHANGE_INTERVAL, SYNC_NOW, REQUEST_STATUS.");

        // CHANGE_INTERVAL es el único tipo que necesita un payload específico
        // (intervalSeconds > 0) — el resto no requiere ninguno.
        RuleFor(x => x.PayloadJson)
            .Must(HaveAPositiveIntervalSeconds)
            .WithMessage("payload de CHANGE_INTERVAL debe incluir \"intervalSeconds\" como entero positivo.")
            .When(x => x.Type == "CHANGE_INTERVAL");
    }

    private static bool HaveAPositiveIntervalSeconds(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return doc.RootElement.TryGetProperty("intervalSeconds", out var prop)
                && prop.TryGetInt32(out var seconds) && seconds > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
