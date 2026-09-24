using Microsoft.Extensions.Logging.Abstractions;
using ScheduleAssistant.Application.Reminders;
using ScheduleAssistant.Presentation.Composition;
using Xunit;

namespace ScheduleAssistant.Presentation.Tests;

public sealed class NotificationActivationRouterTests
{
    [Fact]
    public async Task HandleActivationAsync_WhenWindowIsReady_ShouldActivateAndOpenTheRequestedTaskOnDispatcher()
    {
        var taskId = Guid.NewGuid();
        var dispatcher = new InlineDispatcher();
        var window = new RecordingWindowService();
        var editor = new RecordingTaskEditorService(openResult: true);
        var router = CreateRouter(dispatcher, window, editor);
        await router.AttachMainWindowAsync();

        await router.HandleActivationAsync(new NotificationActivationEventArgs(taskId));

        Assert.Equal(1, dispatcher.AsyncInvokeCount);
        Assert.Equal(1, window.ActivationCount);
        Assert.Equal(taskId, Assert.Single(editor.OpenedTaskIds));
    }

    [Fact]
    public async Task HandleActivationAsync_WhenColdStartRequestArrivesBeforeWindowReady_ShouldRouteAfterAttachment()
    {
        var taskId = Guid.NewGuid();
        var dispatcher = new InlineDispatcher();
        var window = new RecordingWindowService();
        var editor = new RecordingTaskEditorService(openResult: true);
        var router = CreateRouter(dispatcher, window, editor);

        await router.HandleActivationAsync(new NotificationActivationEventArgs(taskId));

        Assert.Equal(0, window.ActivationCount);
        Assert.Empty(editor.OpenedTaskIds);

        await router.AttachMainWindowAsync();

        Assert.Equal(1, window.ActivationCount);
        Assert.Equal(taskId, Assert.Single(editor.OpenedTaskIds));
        Assert.Equal(1, dispatcher.AsyncInvokeCount);
    }

    [Fact]
    public async Task HandleActivationAsync_WhenTaskWasDeleted_ShouldLeaveOnlyTheMainWindowOpen()
    {
        var taskId = Guid.NewGuid();
        var dispatcher = new InlineDispatcher();
        var window = new RecordingWindowService();
        var editor = new RecordingTaskEditorService(openResult: false);
        var router = CreateRouter(dispatcher, window, editor);
        await router.AttachMainWindowAsync();

        await router.HandleActivationAsync(new NotificationActivationEventArgs(taskId));

        Assert.Equal(1, window.ActivationCount);
        Assert.Equal(taskId, Assert.Single(editor.OpenedTaskIds));
        Assert.Equal(0, editor.DisplayedEditorCount);
    }

    [Fact]
    public async Task HandleActivationAsync_WhenTaskIdIsInvalid_ShouldActivateMainWindowWithoutOpeningAnEditor()
    {
        var dispatcher = new InlineDispatcher();
        var window = new RecordingWindowService();
        var editor = new RecordingTaskEditorService(openResult: true);
        var router = CreateRouter(dispatcher, window, editor);
        await router.AttachMainWindowAsync();

        await router.HandleActivationAsync(new NotificationActivationEventArgs(null));

        Assert.Equal(1, window.ActivationCount);
        Assert.Empty(editor.OpenedTaskIds);
    }

    private static NotificationActivationRouter CreateRouter(
        InlineDispatcher dispatcher,
        RecordingWindowService window,
        RecordingTaskEditorService editor)
    {
        return new NotificationActivationRouter(
            dispatcher,
            window,
            editor,
            NullLogger<NotificationActivationRouter>.Instance);
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public int AsyncInvokeCount { get; private set; }

        public bool CheckAccess => true;

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task InvokeAsync(Func<Task> action)
        {
            AsyncInvokeCount++;
            return action();
        }
    }

    private sealed class RecordingWindowService : IWindowService
    {
        public int ActivationCount { get; private set; }

        public void ShowMainWindow(MainWindow mainWindow)
        {
            _ = mainWindow;
        }

        public void ActivateMainWindow() => ActivationCount++;
    }

    private sealed class RecordingTaskEditorService : ITaskEditorService
    {
        private readonly bool _openResult;

        public RecordingTaskEditorService(bool openResult)
        {
            _openResult = openResult;
        }

        public List<Guid> OpenedTaskIds { get; } = [];

        public int DisplayedEditorCount => _openResult ? OpenedTaskIds.Count : 0;

        public event EventHandler<TaskEditorSavedEventArgs>? TaskSaved
        {
            add { }
            remove { }
        }

        public Task OpenCreateAsync(
            DateOnly? prefilledPlannedDate = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenEditAsync(Guid taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> TryOpenEditAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenedTaskIds.Add(taskId);
            return Task.FromResult(_openResult);
        }
    }
}
