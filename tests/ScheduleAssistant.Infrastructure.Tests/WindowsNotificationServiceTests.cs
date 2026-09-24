using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Windows.AppNotifications;
using ScheduleAssistant.Application.Reminders;
using ScheduleAssistant.Infrastructure.Notifications;
using Xunit;

namespace ScheduleAssistant.Infrastructure.Tests;

public sealed class WindowsNotificationServiceTests
{
    [Fact]
    public async Task GetCapabilityAsync_WhenRegistrationHasNotSucceeded_ShouldRemainUnavailable()
    {
        var runtime = new FakeWindowsAppNotificationRuntime();
        var service = CreateService(runtime);

        var capability = await service.GetCapabilityAsync();

        Assert.False(capability.IsAvailable);
        Assert.Equal("notification.not-registered", capability.ErrorCode);
        Assert.Equal(0, runtime.RegisterCount);
    }

    [Fact]
    public async Task StartAsync_WhenSupported_ShouldSubscribeBeforeRegisterAndRegisterOnlyOnce()
    {
        var runtime = new FakeWindowsAppNotificationRuntime();
        var service = CreateService(runtime);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        Assert.True(runtime.RegisterObservedSubscriber);
        Assert.Equal(1, runtime.RegisterCount);
        Assert.True((await service.GetCapabilityAsync()).IsAvailable);
    }

    [Fact]
    public async Task StartAsync_WhenRegistrationFails_ShouldReturnSafeUnavailableDiagnosisAndUnsubscribe()
    {
        var runtime = new FakeWindowsAppNotificationRuntime
        {
            RegisterException = new InvalidOperationException("private runtime detail")
        };
        var service = CreateService(runtime);

        await service.StartAsync(CancellationToken.None);

        var capability = await service.GetCapabilityAsync();
        Assert.False(capability.IsAvailable);
        Assert.Equal("notification.registration-failed", capability.ErrorCode);
        Assert.Equal(0, runtime.SubscriberCount);

        var result = await service.ShowAsync(CreateReminder());
        Assert.False(result.IsSuccess);
        Assert.Equal("notification.registration-failed", result.ErrorCode);
        Assert.Empty(runtime.ShownPayloads);
    }

    [Fact]
    public async Task GetCapabilityAsync_WhenWindowsApiIsUnsupported_ShouldReportUnavailableWithoutRegistering()
    {
        var runtime = new FakeWindowsAppNotificationRuntime { Supported = false };
        var service = CreateService(runtime);

        await service.StartAsync(CancellationToken.None);

        var capability = await service.GetCapabilityAsync();
        Assert.False(capability.IsAvailable);
        Assert.Equal("notification.api-unsupported", capability.ErrorCode);
        Assert.Equal(0, runtime.RegisterCount);
    }

    [Theory]
    [InlineData(AppNotificationSetting.DisabledForApplication, "notification.disabled-for-application")]
    [InlineData(AppNotificationSetting.DisabledForUser, "notification.disabled-for-user")]
    [InlineData(AppNotificationSetting.DisabledByGroupPolicy, "notification.disabled-by-policy")]
    [InlineData(AppNotificationSetting.DisabledByManifest, "notification.disabled-by-manifest")]
    [InlineData(AppNotificationSetting.Unsupported, "notification.system-unsupported")]
    public async Task GetCapabilityAsync_WhenWindowsSettingBlocksNotifications_ShouldReportTheSpecificReason(
        AppNotificationSetting setting,
        string expectedErrorCode)
    {
        var runtime = new FakeWindowsAppNotificationRuntime { CurrentSetting = setting };
        var service = CreateService(runtime);
        await service.StartAsync(CancellationToken.None);

        var capability = await service.GetCapabilityAsync();

        Assert.False(capability.IsAvailable);
        Assert.Equal(expectedErrorCode, capability.ErrorCode);
    }

    [Fact]
    public async Task ShowAsync_WhenNotificationsAreDisabled_ShouldNotCallWindowsShow()
    {
        var runtime = new FakeWindowsAppNotificationRuntime
        {
            CurrentSetting = AppNotificationSetting.DisabledForApplication
        };
        var service = CreateService(runtime);
        await service.StartAsync(CancellationToken.None);

        var result = await service.ShowAsync(CreateReminder());

        Assert.False(result.IsSuccess);
        Assert.Equal("notification.disabled-for-application", result.ErrorCode);
        Assert.Empty(runtime.ShownPayloads);
    }

    [Fact]
    public async Task StopAsync_WhenRegistrationSucceeded_ShouldUnsubscribeAndUnregisterOnce()
    {
        var runtime = new FakeWindowsAppNotificationRuntime();
        var service = CreateService(runtime);
        await service.StartAsync(CancellationToken.None);

        await service.StopAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, runtime.UnregisterCount);
        Assert.Equal(0, runtime.SubscriberCount);
        Assert.False((await service.GetCapabilityAsync()).IsAvailable);
    }

    [Fact]
    public async Task NotificationInvoked_WhenArgumentsAreWhitelisted_ShouldPublishOnlyValidatedTaskId()
    {
        var taskId = Guid.NewGuid();
        var runtime = new FakeWindowsAppNotificationRuntime();
        var service = CreateService(runtime);
        NotificationActivationEventArgs? activation = null;
        service.NotificationActivated += (_, args) => activation = args;
        await service.StartAsync(CancellationToken.None);

        runtime.Raise($"action=openTask&taskId={taskId:D}");

        Assert.Equal(taskId, activation?.TaskId);
    }

    [Theory]
    [InlineData("action=openUri&taskId=9fa44832-e238-40ba-9e9e-5858d59aab01")]
    [InlineData("action=openTask&taskId=9fa44832-e238-40ba-9e9e-5858d59aab01&uri=file%3A%2F%2F%2FC%3A%2Fsecret")]
    [InlineData("action=openTask&taskId=not-a-guid")]
    [InlineData("action=openTask&taskId=00000000-0000-0000-0000-000000000000")]
    [InlineData("action=openTask&action=openTask&taskId=9fa44832-e238-40ba-9e9e-5858d59aab01")]
    public async Task NotificationInvoked_WhenArgumentsAreInvalid_ShouldRequestMainWindowOnly(string argument)
    {
        var runtime = new FakeWindowsAppNotificationRuntime();
        var service = CreateService(runtime);
        NotificationActivationEventArgs? activation = null;
        service.NotificationActivated += (_, args) => activation = args;
        await service.StartAsync(CancellationToken.None);

        runtime.Raise(argument);

        Assert.NotNull(activation);
        Assert.Null(activation.TaskId);
    }

    [Fact]
    public void PayloadBuilder_WhenTitleContainsXmlCharacters_ShouldEscapeContentAndIncludeTaskAction()
    {
        const string title = "汇报 <研发 & 规划> \"A\"";
        var taskId = Guid.NewGuid();
        var reminder = CreateReminder(taskId, title, DateTimeOffset.UtcNow.AddMinutes(90));

        var payload = WindowsNotificationPayloadBuilder.Build(
            reminder,
            new FixedTimeProvider(DateTimeOffset.UtcNow));
        var document = XDocument.Parse(payload);
        var displayedText = document.Descendants("text").Select(element => element.Value).ToArray();

        Assert.Equal("ScheduleAssistant", displayedText[0]);
        Assert.Equal(title, displayedText[1]);
        Assert.Contains("Deadline：", displayedText[2], StringComparison.Ordinal);
        Assert.Contains("剩余", displayedText[2], StringComparison.Ordinal);
        Assert.Contains("&lt;研发 &amp; 规划&gt;", payload, StringComparison.Ordinal);
        Assert.Equal("查看任务", document.Descendants("action").Single().Attribute("content")?.Value);
        Assert.Contains($"taskId={taskId:D}", payload, StringComparison.Ordinal);
        Assert.Contains("action=openTask", payload, StringComparison.Ordinal);
    }

    private static WindowsNotificationService CreateService(FakeWindowsAppNotificationRuntime runtime)
    {
        return new WindowsNotificationService(
            runtime,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<WindowsNotificationService>.Instance);
    }

    private static ReminderNotification CreateReminder(
        Guid? taskId = null,
        string title = "Test task",
        DateTimeOffset? deadlineUtc = null)
    {
        return new ReminderNotification(
            taskId ?? Guid.NewGuid(),
            title,
            deadlineUtc ?? new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero),
            "notification-test",
            IsDelayed: false,
            IsDeadlineOverdue: false);
    }

    private sealed class FakeWindowsAppNotificationRuntime : IWindowsAppNotificationRuntime
    {
        public event Action<string?>? NotificationInvoked;

        public bool Supported { get; set; } = true;

        public AppNotificationSetting CurrentSetting { get; set; } = AppNotificationSetting.Enabled;

        public Exception? RegisterException { get; set; }

        public int RegisterCount { get; private set; }

        public int UnregisterCount { get; private set; }

        public int SubscriberCount => NotificationInvoked?.GetInvocationList().Length ?? 0;

        public bool RegisterObservedSubscriber { get; private set; }

        public List<string> ShownPayloads { get; } = [];

        public bool IsSupported() => Supported;

        public AppNotificationSetting Setting => CurrentSetting;

        public void Register()
        {
            RegisterCount++;
            RegisterObservedSubscriber = SubscriberCount > 0;
            if (RegisterException is not null)
            {
                throw RegisterException;
            }
        }

        public void Unregister() => UnregisterCount++;

        public void Show(string payload) => ShownPayloads.Add(payload);

        public void Raise(string? argument) => NotificationInvoked?.Invoke(argument);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
