using System;
using System.Threading.Tasks;

// Точка входа сервера: принимает порт из аргументов командной строки или использует 7777 по умолчанию
public class Program
{
    static async Task Main(string[] args)
    {
        int defaultPort = 7777;
        int port = defaultPort;

        // Парсинг аргументов запуска: поддерживаются форматы "7777", "--port 7777", "-p 7777"
        if (args.Length > 0)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--port" || args[i] == "-p") && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out int parsedPort))
                    {
                        port = parsedPort;
                        break;
                    }
                }
                else if (int.TryParse(args[i], out int parsedPort))
                {
                    port = parsedPort;
                    break;
                }
            }

            if (port <= 0 || port > 65535)
            {
                Console.WriteLine($"[!] Некорректный номер порта в аргументах. Используется порт по умолчанию: {defaultPort}");
                port = defaultPort;
            }
        }

        Server server = new Server(port);
        await server.StartListening();
    }
}
