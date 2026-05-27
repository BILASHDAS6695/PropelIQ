namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Renders HTML email bodies for all patient-facing notification scenarios.
/// All templates use inline CSS and a single-column responsive layout.
/// </summary>
internal static class EmailTemplateService
{
    // Shared wrapper — responsive single-column, max-width 600 px
    private static string Wrap(string title, string bodyHtml) =>
        $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width,initial-scale=1" />
          <title>{title}</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:32px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0"
                     style="background:#ffffff;border-radius:8px;overflow:hidden;max-width:600px;width:100%;">
                <tr>
                  <td style="background:#1976d2;padding:24px 32px;">
                    <h1 style="margin:0;color:#ffffff;font-size:20px;">{title}</h1>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px;">
                    {bodyHtml}
                  </td>
                </tr>
                <tr>
                  <td style="background:#f4f4f4;padding:16px 32px;text-align:center;
                             font-size:12px;color:#888888;">
                    HealthPlatform — please do not reply to this email.
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string Row(string label, string value) =>
        $"<tr><td style=\"padding:4px 0;color:#555;font-size:14px;\"><strong>{label}:</strong> {value}</td></tr>";

    private static string DetailTable(params (string label, string value)[] rows)
    {
        var cells = string.Join("\n", Array.ConvertAll(rows, r => Row(r.label, r.value)));
        return $"<table cellpadding=\"0\" cellspacing=\"0\" style=\"margin:16px 0;\">{cells}</table>";
    }

    private static string Greeting(string patientName) =>
        $"<p style=\"font-size:16px;color:#333;\">Hi <strong>{patientName}</strong>,</p>";

    // ── 1. Booking Confirmation ───────────────────────────────────────────────

    public static (string Subject, string Body) BookingConfirmation(
        string patientName,
        string providerName,
        DateTimeOffset appointmentTime,
        Guid appointmentId)
    {
        var subject = $"Appointment confirmed — {appointmentTime:ddd, MMM d 'at' h:mm tt}";
        var body = Wrap(
            "Appointment Confirmed ✓",
            $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              Your appointment has been booked successfully.
            </p>
            {DetailTable(
                ("Provider", providerName),
                ("Date",     appointmentTime.ToString("dddd, MMMM d, yyyy")),
                ("Time",     appointmentTime.ToString("h:mm tt zzz")),
                ("Ref #",    appointmentId.ToString("N")[..8].ToUpperInvariant())
            )}
            <p style="font-size:13px;color:#888;">
              Please arrive 10 minutes before your scheduled time.
            </p>
            """);

        return (subject, body);
    }

    // ── 2. Cancellation ───────────────────────────────────────────────────────

    public static (string Subject, string Body) Cancellation(
        string patientName,
        string providerName,
        DateTimeOffset appointmentTime,
        Guid appointmentId)
    {
        var subject = $"Appointment cancelled — {appointmentTime:MMM d}";
        var body = Wrap(
            "Appointment Cancelled",
            $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              Your appointment has been cancelled.
            </p>
            {DetailTable(
                ("Provider", providerName),
                ("Date",     appointmentTime.ToString("dddd, MMMM d, yyyy")),
                ("Time",     appointmentTime.ToString("h:mm tt zzz")),
                ("Ref #",    appointmentId.ToString("N")[..8].ToUpperInvariant())
            )}
            <p style="font-size:13px;color:#888;">
              To book a new appointment, log in to the patient portal.
            </p>
            """);

        return (subject, body);
    }

    // ── 3. Appointment Reminder ───────────────────────────────────────────────

    public static (string Subject, string Body) Reminder(
        string patientName,
        string providerName,
        DateTimeOffset appointmentTime,
        Guid appointmentId)
    {
        var subject = $"Reminder: appointment tomorrow with {providerName}";
        var body = Wrap(
            "Appointment Reminder",
            $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              This is a reminder of your upcoming appointment.
            </p>
            {DetailTable(
                ("Provider", providerName),
                ("Date",     appointmentTime.ToString("dddd, MMMM d, yyyy")),
                ("Time",     appointmentTime.ToString("h:mm tt zzz")),
                ("Ref #",    appointmentId.ToString("N")[..8].ToUpperInvariant())
            )}
            """);

        return (subject, body);
    }

    // ── 4. Slot Swap Request (notify target patient) ──────────────────────────

    public static (string Subject, string Body) SwapRequest(
        string targetPatientName,
        string requesterName,
        DateTimeOffset targetSlotTime)
    {
        var subject = "A patient has requested a slot swap with you";
        var body = Wrap(
            "Slot Swap Request",
            $"""
            {Greeting(targetPatientName)}
            <p style="font-size:14px;color:#555;">
              <strong>{requesterName}</strong> has requested to swap appointment slots with you.
            </p>
            {DetailTable(
                ("Your slot", targetSlotTime.ToString("dddd, MMMM d 'at' h:mm tt zzz"))
            )}
            <p style="font-size:14px;color:#555;">
              Log in to the patient portal to accept or decline this request.
            </p>
            """);

        return (subject, body);
    }

    // ── 5. Slot Swap Result (accepted or declined) ────────────────────────────

    public static (string Subject, string Body) SwapResult(
        string patientName,
        bool accepted,
        DateTimeOffset newSlotTime)
    {
        var outcome    = accepted ? "accepted" : "declined";
        var titleLabel = accepted ? "Accepted" : "Declined";
        var newSlotRow = accepted
            ? DetailTable(("New slot", newSlotTime.ToString("dddd, MMMM d 'at' h:mm tt zzz")))
            : string.Empty;

        var subject = $"Slot swap {outcome}";
        var body = Wrap(
            $"Slot Swap {titleLabel}",
            $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              Your slot swap request has been <strong>{outcome}</strong>.
            </p>
            {newSlotRow}
            """);

        return (subject, body);
    }

    // ── 6. No-Show Follow-Up ──────────────────────────────────────────────────

    public static (string Subject, string Body) NoShowFollowUp(
        string patientName,
        string providerName,
        DateTimeOffset missedTime,
        Guid appointmentId)
    {
        var subject = "We missed you — please reschedule your appointment";
        var body = Wrap(
            "Missed Appointment Follow-Up",
            $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              You missed your scheduled appointment. We hope everything is okay.
            </p>
            {DetailTable(
                ("Provider",  providerName),
                ("Missed on", missedTime.ToString("dddd, MMMM d 'at' h:mm tt zzz")),
                ("Ref #",     appointmentId.ToString("N")[..8].ToUpperInvariant())
            )}
            <p style="font-size:14px;color:#555;">
              Please log in to the patient portal to book a new appointment.
            </p>
            """);

        return (subject, body);
    }
}
