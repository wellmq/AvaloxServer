using Microsoft.Data.Sqlite;
using Dapper;
using System;
using System.Threading.Tasks;
using System.Linq;

// Message persistence and retrieval service using SQLite and Dapper
public class MessageStorage
{
    private readonly string connectionString = "DataSource=Main.db";

    // Insert message into database and return generated Id
    public async Task<long> Add(Message message)
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        message.Date = DateTime.Now;

        string insertQuery = "INSERT INTO Messages(Sender, Receiver, Text, Date) VALUES (@Sender, @Receiver, @Text, @Date) RETURNING Id";
        long id = Convert.ToInt64(await connection.ExecuteScalarAsync(insertQuery, new
        {
            Sender = message.Sender,
            Receiver = message.Receiver,
            Text = message.Text,
            Date = message.Date
        }));

        return id;
    }

    // Query messages for a user with Id greater than LastMessageId
    public async Task<Message[]> RequestNew(LastMessageInfo lastMessageInfo, string login)
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        string selectQuery = @"SELECT Id, Sender, Receiver, Text, Date FROM Messages 
          WHERE (Sender = @Login OR Receiver = @Login) AND Id > @LastMessageId";

        var messages = await connection.QueryAsync<Message>(selectQuery, new
        {
            Login = login,
            LastMessageId = lastMessageInfo.LastMessageId
        });

        return messages.ToArray();
    }
}
