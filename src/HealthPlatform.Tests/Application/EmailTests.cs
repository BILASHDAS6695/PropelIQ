using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using HealthPlatform.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class EmailTests
{
    // ── HangfireEmailDispatcher ───────────────────────────────────────────────

    [Fact]
    public async Task Dispatcher_EnqueuesJob_WhenSendAsyncCalled()
    {
        // Arrange
        var mockClient = new Mock<IBackgroundJobClient>();
        mockClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");
        var dispatcher = new HangfireEmailDispatcher(mockClient.Object);

        // Act
        await dispatcher.SendAsync("patient@example.com", "Test Subject", "<p>Hello</p>");

        // Assert — job was enqueued with correct type and method
        mockClient.Verify(
            c => c.Create(
                It.Is<Job>(j => j.Type == typeof(SendEmailJob) &&
                                j.Method.Name == nameof(SendEmailJob.ExecuteAsync)),
                It.IsAny<EnqueuedState>()),
            Times.Once);
    }

    [Fact]
    public async Task Dispatcher_PropagatesException_WhenJobClientThrows()
    {
        // Arrange
        var mockClient = new Mock<IBackgroundJobClient>();
        mockClient
            .Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Throws(new InvalidOperationException("Hangfire unavailable"));

        var dispatcher = new HangfireEmailDispatcher(mockClient.Object);

        // Act & Assert — should propagate (caller decides to handle or let Hangfire retry)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync("p@example.com", "Subject", "Body"));
    }

    // ── MailKitEmailSender — invalid address guard ────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@nodomain")]
    public async Task MailKitSender_SkipsDelivery_ForInvalidAddress(string badAddress)
    {
        // Arrange — SMTP host is unreachable; if the guard were missing, a
        // SocketException would be thrown before the assertion.
        var settings = Options.Create(new SmtpSettings
        {
            Host   = "localhost",
            Port   = 1025,
            UseSsl = false
        });
        var sender = new MailKitEmailSender(settings, NullLogger<MailKitEmailSender>.Instance);

        // Act — invalid address guard must return early; no connection attempted
        var ex = await Record.ExceptionAsync(
            () => sender.SendAsync(badAddress, "Subject", "Body"));

        // Assert
        Assert.Null(ex);
    }

    // ── EmailTemplateService — variable substitution ──────────────────────────

    [Fact]
    public void BookingConfirmation_ContainsExpectedVariables()
    {
        var apptId   = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 6, 15, 14, 30, 0, TimeSpan.Zero);

        var (subject, body) = EmailTemplateService.BookingConfirmation(
            "Alice Smith", "Dr. Johnson", apptTime, apptId);

        Assert.Contains("Alice Smith", body);
        Assert.Contains("Dr. Johnson", body);
        Assert.Contains("June",        body);   // date present
        Assert.Contains("2:30",        body);   // time present
        Assert.Contains(apptId.ToString("N")[..8].ToUpperInvariant(), body);
        Assert.Contains("confirmed",   subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cancellation_ContainsExpectedVariables()
    {
        var apptId   = Guid.NewGuid();
        var apptTime = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

        var (subject, body) = EmailTemplateService.Cancellation(
            "Bob Lee", "Dr. Patel", apptTime, apptId);

        Assert.Contains("Bob Lee",   body);
        Assert.Contains("Dr. Patel", body);
        Assert.Contains("cancelled", subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reminder_ContainsProviderName()
    {
        var (_, body) = EmailTemplateService.Reminder(
            "Carol", "Dr. Rivera",
            DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid());

        Assert.Contains("Dr. Rivera", body);
        Assert.Contains("Carol",      body);
    }

    [Fact]
    public void SwapRequest_ContainsRequesterName()
    {
        var (_, body) = EmailTemplateService.SwapRequest(
            "Target Patient", "Requesting Patient",
            DateTimeOffset.UtcNow.AddDays(2));

        Assert.Contains("Requesting Patient", body);
        Assert.Contains("Target Patient",     body);
    }

    [Fact]
    public void SwapResult_Accepted_ContainsNewSlotTime()
    {
        var newSlot = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);

        var (subject, body) = EmailTemplateService.SwapResult("Dave", accepted: true, newSlot);

        Assert.Contains("Dave",     body);
        Assert.Contains("accepted", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("August",   body);
    }

    [Fact]
    public void SwapResult_Declined_DoesNotContainNewSlotRow()
    {
        var (subject, body) = EmailTemplateService.SwapResult(
            "Eve", accepted: false, DateTimeOffset.UtcNow);

        Assert.Contains("declined", subject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("New slot", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoShowFollowUp_ContainsMissedDate()
    {
        var missed = new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero);

        var (_, body) = EmailTemplateService.NoShowFollowUp(
            "Frank", "Dr. Chen", missed, Guid.NewGuid());

        Assert.Contains("Frank",    body);
        Assert.Contains("Dr. Chen", body);
        Assert.Contains("May",      body);
    }
}
