using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

// Asynchronous TCP Server
public class Server
{
    private int port;
    private TcpListener? listener;

    private readonly List<Connection> connections = new();
    private readonly object _lock = new();

    public Server(int port)
    {
        this.port = port;
        Connection.IsOnlineCallback = isOnline;
    }

    // Start listening for incoming TCP connections
    public async Task<bool> StartListening()
    {
        IPAddress localIp = IPAddress.Parse("127.0.0.1");
        int requestedPort = port;
        const int maxPortAttempts = 100;
        bool isBound = false;

        // Find next available port on conflict
        for (int attempt = 0; attempt < maxPortAttempts; attempt++)
        {
            try
            {
                listener = new TcpListener(new IPEndPoint(localIp, port));
                listener.Start();
                isBound = true;
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Console.WriteLine($"[!] Port {port} is already in use. Trying next...");
                port++;
                if (port > 65535) port = 1024;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Error starting listener on port {port}: {ex.Message}");
                return false;
            }
        }

        if (!isBound || listener == null)
        {
            Console.WriteLine($"[!] Could not find an available port in range {requestedPort}-{port}.");
            return false;
        }

        if (port != requestedPort)
        {
            Console.WriteLine($"[i] Requested port {requestedPort} was busy. Bound to port: {port}");
        }

        Console.WriteLine($"Server started and listening on port {port}...");

        // Clean up inactive connections every 3 seconds
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(3000);
                CheckForDeadConnections();
            }
        });

        // Accept incoming client connections
        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            Connection connection = new Connection(client);

            connection.OnDisconnected = RemoveConnection;

            lock (_lock)
            {
                connections.Add(connection);
            }

            _ = connection.StartHandling();
            Console.WriteLine($"[+] New connection from {client.Client.RemoteEndPoint}");
        }
    }

    private void RemoveConnection(Connection connection)
    {
        lock (_lock)
        {
            connections.Remove(connection);
        }

        string info = connection.Login ?? "guest";
        try
        {
            if (connection.Client.Client.RemoteEndPoint != null)
            {
                info += $" ({connection.Client.Client.RemoteEndPoint})";
            }
        }
        catch { }

        Console.WriteLine($"[-] Disconnected: {info}");
        connection.Close();
    }

    private bool isOnline(string login)
    {
        lock (_lock)
        {
            return connections.Any(c => c.Login == login);
        }
    }

    // Detect and remove dead or timed-out connections
    private void CheckForDeadConnections()
    {
        List<Connection> deadConnections;
        DateTime now = DateTime.Now;

        lock (_lock)
        {
            deadConnections = connections.Where(c =>
                (!string.IsNullOrWhiteSpace(c.Login) && ((now - c.LastRequestTime) > TimeSpan.FromSeconds(5))) ||
                (string.IsNullOrWhiteSpace(c.Login) && ((now - c.LastRequestTime) > TimeSpan.FromSeconds(300)))
            ).ToList();

            foreach (Connection connection in deadConnections)
            {
                connections.Remove(connection);
            }
        }

        foreach (Connection connection in deadConnections)
        {
            string info = connection.Login ?? "guest";
            try
            {
                if (connection.Client.Client.RemoteEndPoint != null)
                {
                    info += $" ({connection.Client.Client.RemoteEndPoint})";
                }
            }
            catch { }

            Console.WriteLine($"[-] Closed inactive connection: {info}");
            connection.Close();
        }
    }
}
