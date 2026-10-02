using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using Dapper;
using System;
using System.Threading.Tasks;

// Сервис регистрации и авторизации с PBKDF2 хешированием и солью
public class RegAuth
{
    private const int SaltLength = 16;       // Длина соли (байты)
    private const int HashLength = 32;       // Длина хеша (байты)
    private const int Iterations = 50000;    // Раунды PBKDF2
    private readonly string connectionString = "DataSource=Main.db";

    // Регистрация: проверка уникальности логина, генерация соли и сохранение PBKDF2-хеша
    public async Task<bool> Register(Credentials credentials)
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        // Проверка: занят ли логин
        string countQuery = "SELECT count(*) FROM Users WHERE Login = @Login";
        int result = Convert.ToInt32(await connection.ExecuteScalarAsync(countQuery, new
        {
            Login = credentials.Login,
        }));

        if (result > 0) return false;

        // Генерация случайной соли
        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);

        // PBKDF2-хеш с SHA-256
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            credentials.Password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashLength);

        // Сохранение учетных данных в базу
        string insertQuery = "INSERT INTO Users(Login, Hash, Salt) VALUES (@Login, @Hash, @Salt)";
        await connection.ExecuteAsync(insertQuery, new
        {
            Login = credentials.Login,
            Hash = Convert.ToBase64String(hash),
            Salt = Convert.ToBase64String(salt)
        });

        return true;
    }

    // Аутентификация: проверка пароля за постоянное время (FixedTimeEquals)
    public async Task<bool> Auth(Credentials credentials)
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        string selectQuery = "SELECT Hash, Salt FROM Users WHERE Login = @Login";
        HashSalt? hashSalt = await connection.QueryFirstOrDefaultAsync<HashSalt>(selectQuery, new
        {
            Login = credentials.Login
        });

        if (hashSalt == null) return false;

        byte[] hashFromDBBytes = Convert.FromBase64String(hashSalt.Hash);
        byte[] salt = Convert.FromBase64String(hashSalt.Salt);

        // Хешируем введенный пароль с сохраненной солью
        byte[] hashFromUserPassword = Rfc2898DeriveBytes.Pbkdf2(
            credentials.Password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashLength
        );

        // Безопасное сравнение за постоянное время против timing attacks
        return CryptographicOperations.FixedTimeEquals(hashFromUserPassword, hashFromDBBytes);
    }
}
