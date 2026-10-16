using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HotKeyManager.Services;

/// <summary>
/// Connection to PadDeck (DisplayPad software) over the named pipe "PadDeck.Bridge".
/// Deliberately without an RPC library: a small JSON-RPC 2.0 client with Content-Length framing is
/// enough for two methods (learnKey, setClaims) and two notifications (keyDown, keyUp).
/// When PadDeck is not installed or not running, it quietly retries every few seconds;
/// HotKeyManager works normally without PadDeck.
/// </summary>
public sealed class PadDeckBridgeService : IDisposable
{
    private const string PipeName = "PadDeck.Bridge";
    private const string Owner = "HotKeyManager";
    private const string ContentLengthHeader = "Content-Length:";
    private const int ConnectTimeoutMs = 2000;

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private NamedPipeClientStream? pipe;
    private int nextId;

    /// <summary>Supplies the current claims (set by the HotkeyManagerService).</summary>
    public Func<IReadOnlyList<PadClaim>>? ClaimsProvider { get; set; }

    /// <summary>True while the pipe to PadDeck is open.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>PadDeck is installed or connected; only then the editor offers pad keys.</summary>
    public bool IsAvailable => this.IsConnected || File.Exists(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PadDeck",
        "current",
        "PadDeck.Service.exe"));

    /// <summary>A claimed key was pressed or released (raised on a background thread).</summary>
    public event EventHandler<PadKeyEventArgs>? PadKey;

    /// <summary>The connection to PadDeck was opened or lost.</summary>
    public event EventHandler? ConnectionChanged;

    /// <summary>Starts the background connect loop.</summary>
    public void Start() => _ = Task.Run(this.ConnectLoopAsync);

    /// <summary>Waits for the next key press on the pad; null when nothing was pressed in time.</summary>
    public async Task<PadKeyInfo?> LearnKeyAsync(TimeSpan timeout)
    {
        // PadDeck answers after the timeout at the latest, so wait a little longer for the reply.
        var result = await this.InvokeAsync("learnKey", [(int)timeout.TotalMilliseconds], timeout + CallTimeout);
        return result.ValueKind == JsonValueKind.Object ? result.Deserialize<PadKeyInfo>(Json) : null;
    }

    /// <summary>Sends all claims again (after connecting and on every hotkey change).</summary>
    public async Task PushClaimsAsync()
    {
        if (!this.IsConnected || this.ClaimsProvider is null)
        {
            return;
        }

        try
        {
            await this.InvokeAsync("setClaims", [Owner, this.ClaimsProvider()], CallTimeout);
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("PadDeck-Belegungen konnten nicht gesendet werden", ex);
        }
    }

    /// <summary>Stops reconnecting and closes the pipe.</summary>
    public void Dispose()
    {
        this.stop.Cancel();
        this.pipe?.Dispose();
        this.stop.Dispose();
        this.writeLock.Dispose();
    }

    private async Task ConnectLoopAsync()
    {
        var token = this.stop.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var connection = new NamedPipeClientStream(
                    ".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await connection.ConnectAsync(ConnectTimeoutMs, token);
                this.pipe = connection;
                this.SetConnected(true);
                App.Current.LogService.Info("PadDeck verbunden");
                _ = this.PushClaimsAsync();
                await this.ReadLoopAsync(connection, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
                when (ex is TimeoutException or IOException or ObjectDisposedException or JsonException)
            {
                // PadDeck is not running (the normal case without PadDeck), so only debug level.
                App.Current.LogService.Debug($"PadDeck nicht erreichbar: {ex.Message}");
            }
            finally
            {
                this.Disconnect();
            }

            try
            {
                await Task.Delay(ReconnectDelay, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void Disconnect()
    {
        if (this.IsConnected)
        {
            App.Current.LogService.Info("PadDeck getrennt");
        }

        this.pipe?.Dispose();
        this.pipe = null;
        foreach (var call in this.pending.Values)
        {
            call.TrySetCanceled();
        }

        this.pending.Clear();
        this.SetConnected(false);
    }

    private void SetConnected(bool connected)
    {
        if (this.IsConnected == connected)
        {
            return;
        }

        this.IsConnected = connected;
        this.ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<JsonElement> InvokeAsync(string method, object?[] args, TimeSpan timeout)
    {
        var connection = this.pipe ?? throw new InvalidOperationException("PadDeck ist nicht verbunden.");
        var id = Interlocked.Increment(ref this.nextId);
        var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.pending[id] = reply;
        try
        {
            await this.WriteAsync(connection, new { jsonrpc = "2.0", id, method, @params = args });
            return await reply.Task.WaitAsync(timeout);
        }
        finally
        {
            this.pending.TryRemove(id, out _);
        }
    }

    private async Task WriteAsync(Stream connection, object message)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        var header = Encoding.ASCII.GetBytes($"{ContentLengthHeader} {body.Length}\r\n\r\n");
        await this.writeLock.WaitAsync();
        try
        {
            await connection.WriteAsync(header);
            await connection.WriteAsync(body);
            await connection.FlushAsync();
        }
        finally
        {
            this.writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(Stream connection, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var length = await ReadHeaderAsync(connection, token);
            var body = new byte[length];
            await connection.ReadExactlyAsync(body, token);
            using var document = JsonDocument.Parse(body);
            this.Dispatch(document.RootElement);
        }
    }

    /// <summary>Reads "Header: value" lines up to the empty line and returns the Content-Length.</summary>
    private static async Task<int> ReadHeaderAsync(Stream connection, CancellationToken token)
    {
        var length = -1;
        var line = new StringBuilder();
        var buffer = new byte[1];
        while (true)
        {
            if (await connection.ReadAsync(buffer, token) == 0)
            {
                throw new IOException("PadDeck hat die Verbindung geschlossen.");
            }

            var character = (char)buffer[0];
            if (character == '\r')
            {
                continue;
            }

            if (character != '\n')
            {
                line.Append(character);
                continue;
            }

            if (line.Length == 0)
            {
                return length >= 0 ? length : throw new IOException("Antwort ohne Content-Length.");
            }

            var text = line.ToString();
            if (text.StartsWith(ContentLengthHeader, StringComparison.OrdinalIgnoreCase))
            {
                length = int.Parse(text[ContentLengthHeader.Length..].Trim());
            }

            line.Clear();
        }
    }

    private void Dispatch(JsonElement message)
    {
        if (message.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number)
        {
            this.CompleteCall(idElement.GetInt32(), message);
            return;
        }

        if (!message.TryGetProperty("method", out var method) || !message.TryGetProperty("params", out var args))
        {
            return;
        }

        bool? pressed = method.GetString() switch
        {
            "keyDown" => true,
            "keyUp" => false,
            _ => null,
        };
        if (pressed is null)
        {
            return;
        }

        // StreamJsonRpc sends notification arguments as a positional array.
        var keyJson = args.ValueKind == JsonValueKind.Array ? args[0] : args;
        if (keyJson.Deserialize<PadKeyInfo>(Json) is { } key)
        {
            this.PadKey?.Invoke(this, new PadKeyEventArgs(key, pressed.Value));
        }
    }

    private void CompleteCall(int id, JsonElement message)
    {
        if (!this.pending.TryGetValue(id, out var reply))
        {
            return;
        }

        if (message.TryGetProperty("error", out var error))
        {
            var text = error.TryGetProperty("message", out var detail) ? detail.GetString() : null;
            reply.TrySetException(new InvalidOperationException(text ?? "PadDeck-Fehler"));
        }
        else
        {
            reply.TrySetResult(message.TryGetProperty("result", out var result) ? result.Clone() : default);
        }
    }
}
