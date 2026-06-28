using MySqlConnector.MySqlClient;

namespace CybersecurityAwarenessChatbot;

public sealed class MySqlTaskRepository
{
    private const string DefaultDatabase = "cybersecurity_chatbot";
    private readonly string _connectionString;

    public MySqlTaskRepository()
    {
        _connectionString = BuildConnectionString();
        EnsureDatabaseAndTable();
    }

    public List<CyberTask> LoadTasks()
    {
        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, description, reminder_text, is_completed, created_at, completed_at
            FROM cyber_tasks
            ORDER BY created_at;
            """;

        using var reader = command.ExecuteReader();
        var tasks = new List<CyberTask>();

        while (reader.Read())
        {
            tasks.Add(new CyberTask
            {
                Id = reader.GetString("id"),
                Title = reader.GetString("title"),
                Description = reader.GetString("description"),
                ReminderText = reader.IsDBNull(reader.GetOrdinal("reminder_text")) ? null : reader.GetString("reminder_text"),
                IsCompleted = reader.GetBoolean("is_completed"),
                CreatedAt = reader.GetDateTime("created_at"),
                CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : reader.GetDateTime("completed_at")
            });
        }

        return tasks;
    }

    public void SaveTasks(IEnumerable<CyberTask> tasks)
    {
        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM cyber_tasks;";
            delete.ExecuteNonQuery();
        }

        foreach (var task in tasks)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO cyber_tasks
                    (id, title, description, reminder_text, is_completed, created_at, completed_at)
                VALUES
                    (@id, @title, @description, @reminder_text, @is_completed, @created_at, @completed_at);
                """;
            AddTaskParameters(insert, task);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void AddTaskParameters(MySqlCommand command, CyberTask task)
    {
        command.Parameters.AddWithValue("@id", task.Id);
        command.Parameters.AddWithValue("@title", task.Title);
        command.Parameters.AddWithValue("@description", task.Description);
        command.Parameters.AddWithValue("@reminder_text", (object?)task.ReminderText ?? DBNull.Value);
        command.Parameters.AddWithValue("@is_completed", task.IsCompleted);
        command.Parameters.AddWithValue("@created_at", task.CreatedAt);
        command.Parameters.AddWithValue("@completed_at", (object?)task.CompletedAt ?? DBNull.Value);
    }

    private void EnsureDatabaseAndTable()
    {
        var builder = new MySqlConnectionStringBuilder(_connectionString);
        var database = string.IsNullOrWhiteSpace(builder.Database) ? DefaultDatabase : builder.Database;
        var serverBuilder = new MySqlConnectionStringBuilder(_connectionString)
        {
            Database = string.Empty
        };

        using (var serverConnection = new MySqlConnection(serverBuilder.ConnectionString))
        {
            serverConnection.Open();
            using var createDatabase = serverConnection.CreateCommand();
            createDatabase.CommandText = "CREATE DATABASE IF NOT EXISTS " + database + ";";
            createDatabase.ExecuteNonQuery();
        }

        builder.Database = database;
        using var connection = new MySqlConnection(builder.ConnectionString);
        connection.Open();

        using var createTable = connection.CreateCommand();
        createTable.CommandText = """
            CREATE TABLE IF NOT EXISTS cyber_tasks (
                id VARCHAR(64) NOT NULL PRIMARY KEY,
                title VARCHAR(255) NOT NULL,
                description TEXT NOT NULL,
                reminder_text VARCHAR(255) NULL,
                is_completed BOOLEAN NOT NULL DEFAULT FALSE,
                created_at DATETIME NOT NULL,
                completed_at DATETIME NULL
            );
            """;
        createTable.ExecuteNonQuery();
    }

    private static string BuildConnectionString()
    {
        var fullConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(fullConnectionString))
        {
            return fullConnectionString;
        }

        var builder = new MySqlConnectionStringBuilder
        {
            Server = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "127.0.0.1",
            Port = uint.TryParse(Environment.GetEnvironmentVariable("MYSQL_PORT"), out var port) ? port : 3306,
            Database = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? DefaultDatabase,
            UserID = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "root",
            Password = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? string.Empty,
            SslMode = MySqlSslMode.None
        };

        return builder.ConnectionString;
    }
}
