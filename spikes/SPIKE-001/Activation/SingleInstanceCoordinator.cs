using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ScheduleAssistant.Spike001.Notifications.Activation;

internal sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    private const string MutexName = "Local\\ScheduleAssistant.SPIKE001.Notifications";
    private const string PipeName = "ScheduleAssistant.SPIKE001.Notifications";

    private readonly Action<ActivationCommand> _commandReceived;
    private readonly Action<Exception> _serverError;
    private readonly CancellationTokenSource _serverCancellation = new();
    private readonly Mutex _mutex;
    private Task? _serverTask;
    private bool _disposed;

    public SingleInstanceCoordinator(
        Action<ActivationCommand> commandReceived,
        Action<Exception> serverError)
    {
        _commandReceived = commandReceived;
        _serverError = serverError;
        _mutex = new Mutex(initiallyOwned: false, MutexName);

        try
        {
            IsPrimary = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            IsPrimary = true;
        }
    }

    public bool IsPrimary { get; }

    public void StartServer()
    {
        if (IsPrimary && _serverTask is null)
        {
            _serverTask = ServeAsync(_serverCancellation.Token);
        }
    }

    public async Task<bool> SendToPrimaryAsync(
        ActivationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (IsPrimary)
        {
            return false;
        }

        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            await client.ConnectAsync(750, cancellationToken).ConfigureAwait(false);

            await using var writer = new StreamWriter(
                client,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: false)
            {
                AutoFlush = true,
            };

            await writer.WriteLineAsync(JsonSerializer.Serialize(command)).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or TimeoutException or OperationCanceledException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _serverCancellation.Cancel();

        if (_serverTask is not null)
        {
            try
            {
                await _serverTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }
        }

        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex was already abandoned or released during shutdown.
            }
        }

        _mutex.Dispose();
        _serverCancellation.Dispose();
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(
                    server,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: false);

                string? payload = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    continue;
                }

                ActivationCommand? command = JsonSerializer.Deserialize<ActivationCommand>(payload);
                if (command is not null && !string.IsNullOrWhiteSpace(command.Action))
                {
                    _commandReceived(command with { Source = "pipe" });
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (JsonException exception)
            {
                _serverError(exception);
            }
            catch (IOException exception) when (!cancellationToken.IsCancellationRequested)
            {
                _serverError(exception);
            }
        }
    }
}
