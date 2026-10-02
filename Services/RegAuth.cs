using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using Dapper;
using System;
using System.Threading.Tasks;

// Регистрация и авторизация пользователей
public class RegAuth
{
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int Iterations = 50000;
    private readonly string connectionString = "DataSource=Main.db";

    // Регистрация нового пользователя
    public async Task<bool> Register(Credentials credentials)
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        string countQuery = "SELECT count(*) FROM Users WHERE Login = @Login";
        int result = Convert.ToInt32(await connection.ExecuteScalarAsync(countQuery, new
        {
            Login = credentials.Login,
        }));

        if (result > 0) return false;

        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            credentials.Password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashLength);

        string insertQuery = "INSERT INTO Users(Login, Hash, Salt) VALUES (@Login, @Hash, @Salt)";
        await connection.ExecuteAsync(insertQuery, new
        {
            Login = credentials.Login,
            Hash = Convert.ToBase64String(hash),
            Salt = Convert.ToBase64String(salt)
        });

        return true;
    }

    // Проверка логина и пароля
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

        byte[] hashFromUserPassword = Rfc2898DeriveBytes.Pbkdf2(
            credentials.Password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashLength
        );

        return CryptographicOperations.FixedTimeEquals(hashFromUserPassword, hashFromDBBytes);
    }
}
