namespace HealthPlatform.Infrastructure.Messaging;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host        { get; init; } = "localhost";
    public int    Port        { get; init; } = 587;
    public bool   UseSsl      { get; init; } = true;
    public string UserName    { get; init; } = string.Empty;
    public string Password    { get; init; } = string.Empty;
    public string FromAddress { get; init; } = "no-reply@healthplatform.local";
    public string FromName    { get; init; } = "HealthPlatform";
}
