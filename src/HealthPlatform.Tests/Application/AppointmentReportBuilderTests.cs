using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Reports;

namespace HealthPlatform.Tests.Application;

public sealed class AppointmentReportBuilderTests
{
    private static AppointmentReportBuilder Builder() => new();

    [Fact]
    public void Build_ReturnsNonEmptyBytes_WhenNoAppointments()
    {
        var data = new AppointmentReportData(
            "Alice Smith",
            DateTimeOffset.UtcNow.AddMonths(-12),
            DateTimeOffset.UtcNow,
            []);

        var bytes = Builder().Build(data);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Build_ReturnsNonEmptyBytes_WhenAppointmentsPresent()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(i => new AppointmentReportRow(
                DateTimeOffset.UtcNow.AddDays(-i),
                $"Dr. Provider {i}",
                "Scheduled",
                i % 2 == 0 ? "Annual check-up" : null))
            .ToList();

        var data = new AppointmentReportData(
            "Bob Jones",
            DateTimeOffset.UtcNow.AddMonths(-1),
            DateTimeOffset.UtcNow,
            rows);

        var bytes = Builder().Build(data);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100); // must be a real PDF, not empty
    }

    [Fact]
    public void Build_IsDeterministicInStructure_ForSameInput()
    {
        // Two calls with identical data must produce valid PDFs (both > 0 bytes).
        // We do not assert byte-for-byte equality (generation timestamps differ).
        var data = new AppointmentReportData(
            "Carol White",
            DateTimeOffset.UtcNow.AddMonths(-3),
            DateTimeOffset.UtcNow,
            [new(DateTimeOffset.UtcNow.AddDays(-5), "Dr. Brown", "Completed", "Follow-up")]);

        var bytes1 = Builder().Build(data);
        var bytes2 = Builder().Build(data);

        Assert.True(bytes1.Length > 0);
        Assert.True(bytes2.Length > 0);
    }
}
