using System.Threading.Tasks;

// Точка входа сервера: слушает входящие TCP-подключения на порту 7777
public class Program
{
    static async Task Main()
    {
        Server server = new Server(7777);
        await server.StartListening();
    }
}
