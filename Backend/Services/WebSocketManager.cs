using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Text;

namespace Backend.Services
{
    public class WebSocketManager
    {
        private ConcurrentDictionary<string, WebSocket> _sockets = new ConcurrentDictionary<string, WebSocket>();

        public string AddSocket(WebSocket socket)
        {
            var socketId = Guid.NewGuid().ToString();
            _sockets.TryAdd(socketId, socket);
            Console.WriteLine($"WebSocket connected: {socketId}");
            return socketId;
        }

        public async Task ReceiveMessages(string socketId, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }

            _sockets.TryRemove(socketId, out _);
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            Console.WriteLine($"WebSocket disconnected: {socketId}");
        }

        public async Task BroadcastMessage(string message)
        {
            var tasks = _sockets.Values.Select(async socket =>
            {
                if (socket.State == WebSocketState.Open)
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"Broadcast message to WebSocket: {Encoding.UTF8.GetString(buffer)}");
                }
            });

            await Task.WhenAll(tasks);
        }
    }
}
