using HealthPlatform.Application.Features.NotificationPreferences;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Domain.ValueObjects;
using HealthPlatform.Infrastructure.Notifications;
using Moq;

namespace HealthPlatform.Tests.Application;

public sealed class NotificationPreferencesTests
{
    // ── GetNotificationPreferencesQueryHandler ────────────────────────────────

    [Fact]
    public async Task Get_ReturnsCurrentPreferences()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            NotificationPreferences = new NotificationPreferences
            {
                EmailReminders = false,
                EmailSwap      = true,
                EmailGeneral   = true,
                InAppReminders = true,
                InAppSwap      = false,
                InAppGeneral   = true,
            },
        };

        var mockRepo = new Mock<IRepository<User>>();
        mockRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Repository<User>()).Returns(mockRepo.Object);

        var handler = new GetNotificationPreferencesQueryHandler(mockUow.Object);
        var result  = await handler.Handle(
            new GetNotificationPreferencesQuery(userId), CancellationToken.None);

        Assert.False(result.EmailReminders);
        Assert.True(result.EmailSwap);
        Assert.False(result.InAppSwap);
    }

    // ── UpdateNotificationPreferencesCommandHandler ───────────────────────────

    [Fact]
    public async Task Update_PersistsNewFlags()
    {
        var userId = Guid.NewGuid();
        var user   = new User { Id = userId };

        var mockRepo = new Mock<IRepository<User>>();
        mockRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Repository<User>()).Returns(mockRepo.Object);

        var handler = new UpdateNotificationPreferencesCommandHandler(mockUow.Object);
        await handler.Handle(
            new UpdateNotificationPreferencesCommand(
                userId,
                EmailReminders: false,
                EmailSwap:      true,
                EmailGeneral:   true,
                InAppReminders: true,
                InAppSwap:      false,
                InAppGeneral:   true),
            CancellationToken.None);

        Assert.False(user.NotificationPreferences.EmailReminders);
        Assert.False(user.NotificationPreferences.InAppSwap);
        mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── NotificationPreferenceCheckerService ──────────────────────────────────

    [Theory]
    [InlineData(NotificationChannel.Email, NotificationType.Reminder,    false, true,  true,  true,  true,  true,  false)]
    [InlineData(NotificationChannel.Email, NotificationType.SwapRequest, true,  false, true,  true,  true,  true,  false)]
    [InlineData(NotificationChannel.InApp, NotificationType.Reminder,    true,  true,  true,  false, true,  true,  false)]
    [InlineData(NotificationChannel.InApp, NotificationType.SwapResult,  true,  true,  true,  true,  false, true,  false)]
    [InlineData(NotificationChannel.Email, NotificationType.StatusChange, true, true,  false, true,  true,  true,  false)]
    [InlineData(NotificationChannel.InApp, NotificationType.General,     true,  true,  true,  true,  true,  false, false)]
    [InlineData(NotificationChannel.Email, NotificationType.Reminder,    true,  true,  true,  true,  true,  true,  true)]
    public async Task Checker_ReturnsExpected(
        NotificationChannel channel,
        NotificationType    type,
        bool emailRem, bool emailSwap, bool emailGen,
        bool inAppRem, bool inAppSwap, bool inAppGen,
        bool expected)
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            NotificationPreferences = new NotificationPreferences
            {
                EmailReminders = emailRem,
                EmailSwap      = emailSwap,
                EmailGeneral   = emailGen,
                InAppReminders = inAppRem,
                InAppSwap      = inAppSwap,
                InAppGeneral   = inAppGen,
            },
        };

        var mockRepo = new Mock<IRepository<User>>();
        mockRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Repository<User>()).Returns(mockRepo.Object);

        var checker = new NotificationPreferenceCheckerService(mockUow.Object);
        var result  = await checker.IsAllowedAsync(userId, channel, type);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Checker_DefaultsToAllowed_WhenUserNotFound()
    {
        var mockRepo = new Mock<IRepository<User>>();
        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Repository<User>()).Returns(mockRepo.Object);

        var checker = new NotificationPreferenceCheckerService(mockUow.Object);
        var result  = await checker.IsAllowedAsync(
            Guid.NewGuid(), NotificationChannel.Email, NotificationType.Reminder);

        Assert.True(result);
    }
}
