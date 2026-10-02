using System;
using System.Threading.Tasks;

// Server entry point: parses port from command line arguments or falls back to default port 7777
public class Program
{
    static async Task Main(string[] args)
    {
        int defaultPort = 7777;
        int port = defaultPort;

        // Parse startup arguments: supports "7777", "--port 7777", "-p 7777"
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
                Console.WriteLine($"[!] Invalid port number in arguments. Using default port: {defaultPort}");
                port = defaultPort;
            }
        }

        await Database.InitAsync();
        Server server = new Server(port);
        await server.StartListening();
    }
}
