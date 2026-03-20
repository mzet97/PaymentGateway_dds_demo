using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace PaymentGateway.Api.Common.Realtime;

public interface IPaymentUpdatesNotifier
{
    Task HandleClientAsync(WebSocket socket, Guid merchantId, CancellationToken ct);
    Task BroadcastAsync(Guid merchantId, string eventType, object payload, CancellationToken ct = default);
}

public sealed class PaymentUpdatesNotifier : IPaymentUpdatesNotifier
{
    private sealed record ClientConnection(Guid MerchantId, WebSocket Socket);

    private readonly ConcurrentDictionary<string, ClientConnection> _clients = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task HandleClientAsync(WebSocket socket, Guid merchantId, CancellationToken ct)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        _clients[connectionId] = new ClientConnection(merchantId, socket);

        var buffer = new byte[1024];
        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            _clients.TryRemove(connectionId, out _);

            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }

            socket.Dispose();
        }
    }

    public async Task BroadcastAsync(Guid merchantId, string eventType, object payload, CancellationToken ct = default)
    {
        if (merchantId == Guid.Empty)
            return;

        var envelope = JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = eventType,
            payment = payload,
            timestamp = DateTime.UtcNow
        }, JsonOptions);

        foreach (var pair in _clients.ToArray())
        {
            var connectionId = pair.Key;
            var connection = pair.Value;

            if (connection.MerchantId != merchantId)
                continue;

            if (connection.Socket.State != WebSocketState.Open)
            {
                _clients.TryRemove(connectionId, out _);
                continue;
            }

            try
            {
                await connection.Socket.SendAsync(envelope, WebSocketMessageType.Text, true, ct);
            }
            catch
            {
                _clients.TryRemove(connectionId, out _);
                connection.Socket.Abort();
                connection.Socket.Dispose();
            }
        }
    }
}
