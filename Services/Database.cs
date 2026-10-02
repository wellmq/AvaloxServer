using Microsoft.Data.Sqlite;
using Dapper;
using System.Threading.Tasks;

// Инициализация базы данных SQLite и создание таблиц при старте
public static class Database
{
    private const string ConnectionString = "DataSource=Main.db";

    public static async Task InitAsync()
    {
        using SqliteConnection connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        string sql = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Login TEXT NOT NULL UNIQUE,
                Hash TEXT NOT NULL,
                Salt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Sender TEXT NOT NULL,
                Receiver TEXT NOT NULL,
                Text TEXT NOT NULL,
                Date TEXT NOT NULL
            );
        ";

        await connection.ExecuteAsync(sql);
    }
}
