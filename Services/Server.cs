using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

// Главный TCP-сервер: принимает подключения, следит за активностью клиентов и онлайном
public class Server
{
    private int port;
    private TcpListener? listener;

    // Список активных подключений (защищен _lock от потоковых гонок)
    private readonly List<Connection> connections = new();
    private readonly object _lock = new();

    public Server(int port)
    {
        this.port = port;
        Connection.IsOnlineCallback = isOnline;
    }

    // Запуск прослушивания порта и цикла принятия клиентов
    public async Task<bool> StartListening()
    {
        IPAddress localIp = IPAddress.Parse("127.0.0.1");
        listener = new TcpListener(new IPEndPoint(localIp, port));
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Ошибка запуска листенера: {ex.Message}");
            return false;
        }

        Console.WriteLine($"Сервер запущен и слушает порт {port}...");

        // Фоновый таймер (раз в 3 сек) для очистки неактивных клиентов
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(3000);
                CheckForDeadConnections();
            }
        });

        // Бесконечный цикл принятия клиентов
        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            Connection connection = new Connection(client);

            // Автоматическое удаление и закрытие сокета при дисконнекте
            connection.OnDisconnected = RemoveConnection;

            lock (_lock)
            {
                connections.Add(connection);
            }

            // Запуск асинхронной обработки клиента
            _ = connection.StartHandling();
            Console.WriteLine($"[+] Новое подключение от {client.Client.RemoteEndPoint}");
        }
    }

    // Немедленное удаление клиента из списка при отключении
    private void RemoveConnection(Connection connection)
    {
        lock (_lock)
        {
            connections.Remove(connection);
        }
        connection.Close();
    }

    // Проверка онлайн-статуса пользователя
    private bool isOnline(string login)
    {
        lock (_lock)
        {
            return connections.Any(c => c.Login == login);
        }
    }

    // Удаление и закрытие зависших/неактивных соединений
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

        // Закрытие сокетов вне lock
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
