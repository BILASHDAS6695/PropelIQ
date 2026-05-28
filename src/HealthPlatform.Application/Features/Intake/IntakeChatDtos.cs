namespace HealthPlatform.Application.Features.Intake;

public record IntakeChatProxyRequest(
    string? SessionId,
    string Message,
    string? PatientId,
    string? AppointmentId);

public record IntakeChatProxyResponse(
    string SessionId,
    string Reply,
    bool IsComplete,
    Dictionary<string, string?> Collected,
    bool FallbackRequired);
