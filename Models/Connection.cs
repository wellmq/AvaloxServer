using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Обработчик клиентского подключения на стороне сервера
public class Connection
{
    // Лимит размера входящего пакета (5 МБ) для защиты от OOM
    private const int MaxPacketSize = 5 * 1024 * 1024;

    private static readonly RegAuth regAuth = new RegAuth();
    private static readonly MessageStorage messageStorage = new MessageStorage();

    // Делегат проверки онлайн-статуса
    public static Func<string, bool> IsOnlineCallback = _ => false;

    // Сокет клиента
    public TcpClient Client { get; init; }

    // Логин после успешной авторизации
    public string? Login { get; set; }

    private NetworkStream stream;

    // Время последнего запроса (для обнаружения мертвых клиентов)
    public DateTime LastRequestTime;

    // Уведомление об отключении клиента
    public Action<Connection>? OnDisconnected { get; set; }

    public Connection(TcpClient client)
    {
        Client = client;
        stream = Client.GetStream();
        LastRequestTime = DateTime.Now;
    }

    // Основной цикл чтения и обработки запросов от клиента
    public async Task StartHandling()
    {
        try
        {
            while (stream != null && Client.Connected)
            {
                // Чтение типа запроса (1 байт)
                byte[] byteType = new byte[1];
                await stream.ReadExactlyAsync(byteType);
                int type = byteType[0];

                // Чтение длины пакета (4 байта)
                byte[] byteLength = new byte[4];
                await stream.ReadExactlyAsync(byteLength);
                int length = BitConverter.ToInt32(byteLength, 0);

                // Валидация длины пакета
                if (length <= 0 || length > MaxPacketSize)
                {
                    Console.WriteLine($"[connection] Недопустимая длина пакета ({length} байт). Отключение {Client.Client.RemoteEndPoint}.");
                    break;
                }

                // Чтение тела запроса
                byte[] byteJson = new byte[length];
                await stream.ReadExactlyAsync(byteJson);
                string json = Encoding.UTF8.GetString(byteJson);

                Response response = new Response();

                // Неавторизованным клиентам доступны только регистрация (0) и логин (1)
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
            // Клиент закрыл соединение
        }
        catch (IOException)
        {
            // Разрыв сокета / обрыв сети
        }
        catch (SocketException)
        {
            // Ошибка сокета
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[connection] Непредвиденная ошибка: {ex.Message}");
        }
        finally
        {
            // Гарантированное освобождение сокета и удаление из списка активных
            Close();
            OnDisconnected?.Invoke(this);
        }
    }

    // Закрытие потока и сокета
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

    // Отправка ответа клиенту: [тип 5] + [длина 4 байта] + [JSON]
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
