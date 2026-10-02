using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

// TCP-сервер
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

    // Запуск сервера
    public async Task<bool> StartListening()
    {
        IPAddress localIp = IPAddress.Parse("127.0.0.1");
        int requestedPort = port;
        const int maxPortAttempts = 100;
        bool isBound = false;

        // Поиск свободного порта при конфликте
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
                Console.WriteLine($"[!] Порт {port} уже занят. Пробуем следующий...");
                port++;
                if (port > 65535) port = 1024;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Ошибка запуска листенера на порту {port}: {ex.Message}");
                return false;
            }
        }

        if (!isBound || listener == null)
        {
            Console.WriteLine($"[!] Не удалось найти свободный порт в диапазоне {requestedPort}-{port}.");
            return false;
        }

        if (port != requestedPort)
        {
            Console.WriteLine($"[i] Запрошенный порт {requestedPort} был занят. Выбран свободный порт: {port}");
        }

        Console.WriteLine($"Сервер запущен и слушает порт {port}...");

        // Очистка неактивных клиентов каждые 3 секунды
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(3000);
                CheckForDeadConnections();
            }
        });

        // Прием входящих подключений
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
            Console.WriteLine($"[+] Новое подключение от {client.Client.RemoteEndPoint}");
        }
    }

    private void RemoveConnection(Connection connection)
    {
        lock (_lock)
        {
            connections.Remove(connection);
        }
        connection.Close();
    }

    private bool isOnline(string login)
    {
        lock (_lock)
        {
            return connections.Any(c => c.Login == login);
        }
    }

    // Проверка неактивных клиентов
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
            try
            {
                Console.WriteLine($"[-] Закрыто неактивное соединение: {connection.Client.Client.RemoteEndPoint}");
                connection.Close();
            }
            catch { }
        }
    }
}
