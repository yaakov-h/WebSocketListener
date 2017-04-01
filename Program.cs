using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketListener
{
    class Program
    {
        static bool cancelled;

        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: WebSocketListener <uri>");
                return -1;
            }

            if (!Uri.TryCreate(args[0], UriKind.Absolute, out var uri))
            {
                Console.Error.WriteLine($"Invalid URI '{args[0]}'.");
                return -2;
            }

            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, e) =>
                {
                    // If the user already pressed Ctrl+C once, just quit.
                    if (cancelled)
                    {
                        return;
                    }

                    // If this is their first time, try to cleanly exit.
                    Console.Error.WriteLine("Ctrl+C pressed. Exiting...");

                    cancelled = true;
                    e.Cancel = true;
                    cts.Cancel();
                };

                try
                {
                    return MainAsync(uri, cts.Token).GetAwaiter().GetResult();
                }
                catch (WebSocketException ex)
                {
                    Console.Error.WriteLine($"WebSocketException: {ex.Message}");
                    return -3;
                }
            }
        }

        static async Task<int> MainAsync(Uri uri, CancellationToken cancellationToken)
        {
            using (var socket = new ClientWebSocket())
            {
                await socket.ConnectAsync(uri, cancellationToken);

                cancellationToken.Register(() => socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by user request", CancellationToken.None).Wait());

                while (socket.State == WebSocketState.Open)
                {
                    var buffer = new byte[4096];
                    var segment = new ArraySegment<byte>(buffer);

                    WebSocketReceiveResult result;

                    try
                    {
                        result = await socket.ReceiveAsync(segment, cancellationToken);
                    }
                    catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                    {
                        return 0;
                    }
                    
                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            Console.WriteLine(text);
                            break;

                        case WebSocketMessageType.Binary:
                            Console.Error.WriteLine("Recieved unsupported binary frame.");
                            return -3;

                        case WebSocketMessageType.Close:
                            Console.Error.WriteLine("Connection closed by server.");
                            return 0;
                    }
                }
            }

            return 0;
        }
    }
}
