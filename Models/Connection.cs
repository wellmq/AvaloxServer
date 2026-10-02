using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// Клиентское подключение на сервере
public class Connection
{
    private const int MaxPacketSize = 5 * 1024 * 1024; // 5 МБ

    private static readonly RegAuth regAuth = new RegAuth();
    private static readonly MessageStorage messageStorage = new MessageStorage();

    public static Func<string, bool> IsOnlineCallback = _ => false;

    public TcpClient Client { get; init; }
    public string? Login { get; set; }
    private NetworkStream stream;
    public DateTime LastRequestTime;
    public Action<Connection>? OnDisconnected { get; set; }

    public Connection(TcpClient client)
    {
        Client = client;
        stream = Client.GetStream();
        LastRequestTime = DateTime.Now;
    }

    // Чтение и обработка запросов
    public async Task StartHandling()
    {
        try
        {
            while (stream != null && Client.Connected)
            {
                // Читаем тип запроса
                byte[] byteType = new byte[1];
                await stream.ReadExactlyAsync(byteType);
                int type = byteType[0];

                // Читаем длину пакета
                byte[] byteLength = new byte[4];
                await stream.ReadExactlyAsync(byteLength);
                int length = BitConverter.ToInt32(byteLength, 0);

                if (length <= 0 || length > MaxPacketSize)
                {
                    Console.WriteLine($"[connection] Недопустимая длина пакета ({length} байт). Отключение {Client.Client.RemoteEndPoint}.");
                    break;
                }

                // Читаем тело запроса
                byte[] byteJson = new byte[length];
                await stream.ReadExactlyAsync(byteJson);
                string json = Encoding.UTF8.GetString(byteJson);

                Response response = new Response();

                // Разрешаем только регистрацию и вход до авторизации
                if (string.IsNullOrWhiteSpace(Login) && type != 0 && type != 1)
                {
                    sendRefusal(response);
                }
                else
                {
                    LastRequestTime = DateTime.Now;
                    try
                    {
                        switch (type)
                        {
                            case 0:
                                await register(response, json);
                                break;
                            case 1:
                                await auth(response, json);
                                break;
                            case 2:
                                await send(response, json);
                                break;
                            case 3:
                                await requestNew(response, json);
                                break;
                            case 4:
                                isOnline(response, json);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[handler] type={type}: {ex}");
                        response.IsSuccessful = false;
                        response.Message = "server error: " + ex.GetType().Name;
                    }
                }

                await sendResponse(response);
            }
        }
        catch (EndOfStreamException)
        {
            // Отключение клиента
        }
        catch (IOException)
        {
            // Разрыв сокета
        }
        catch (SocketException)
        {
            // Ошибка сокета
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[connection] Ошибка: {ex.Message}");
        }
        finally
        {
            Close();
            OnDisconnected?.Invoke(this);
        }
    }

    public void Close()
    {
        try
        {
            stream?.Close();
            stream?.Dispose();
            Client?.Close();
            Client?.Dispose();
        }
        catch { }
    }

    // Отправка ответа клиенту
    private async Task sendResponse(Response response)
    {
        string jsonResponse = JsonSerializer.Serialize(response);
        byte[] byteResponse = Encoding.UTF8.GetBytes(jsonResponse);
        int responseLength = byteResponse.Length;
        byte[] byteResponseLength = BitConverter.GetBytes(responseLength);
        byte[] responseType = [5];
        byte[] finalResponse = responseType.Concat(byteResponseLength).Concat(byteResponse).ToArray();
        await stream.WriteAsync(finalResponse);
    }

    private void sendRefusal(Response response)
    {
        response.IsSuccessful = false;
        response.Message = "auth first";
    }

    private async Task register(Response response, string json)
    {
        Credentials? credentials = JsonSerializer.Deserialize<Credentials>(json);
        if (credentials == null)
        {
            response.IsSuccessful = false;
            response.Message = "invalid credentials";
            return;
        }

        bool isSuccessful = await regAuth.Register(credentials);
        response.IsSuccessful = isSuccessful;
        if (isSuccessful) response.Message = "registered";
        else response.Message = "login already registered";
    }

    private async Task auth(Response response, string json)
    {
        Credentials? credentials = JsonSerializer.Deserialize<Credentials>(json);
        if (credentials == null)
        {
            response.IsSuccessful = false;
            response.Message = "invalid credentials";
            return;
        }

        bool isSuccessful = await regAuth.Auth(credentials);
        response.IsSuccessful = isSuccessful;
        if (isSuccessful)
        {
            response.Message = "authenticated";
            Login = credentials.Login;
        }
        else response.Message = "wrong login or password";
    }

    private async Task send(Response response, string json)
    {
        Message? message = JsonSerializer.Deserialize<Message>(json);
        if (message == null)
        {
            response.IsSuccessful = false;
            response.Message = "invalid message";
            return;
        }

        message.Sender = Login ?? "";
        long id = await messageStorage.Add(message);
        response.IsSuccessful = true;
        response.Message = $"{id}";
    }

    private async Task requestNew(Response response, string json)
    {
        LastMessageInfo? lastMessageInfo = JsonSerializer.Deserialize<LastMessageInfo>(json);
        if (lastMessageInfo == null)
        {
            response.IsSuccessful = false;
            response.Message = "invalid request";
            return;
        }

        Message[] messages = await messageStorage.RequestNew(lastMessageInfo, Login ?? "");
        response.IsSuccessful = true;
        response.Message = $"{messages.Length} new message(-s)";
        response.Obj = messages;
    }

    private void isOnline(Response response, string json)
    {
        TargetUser? targetUser = JsonSerializer.Deserialize<TargetUser>(json);
        if (targetUser == null)
        {
            response.IsSuccessful = false;
            response.Message = "no";
            return;
        }

        bool isOnline = IsOnlineCallback(targetUser.Login);
        response.IsSuccessful = true;
        response.Message = (isOnline) ? "yes" : "no";
    }
}
