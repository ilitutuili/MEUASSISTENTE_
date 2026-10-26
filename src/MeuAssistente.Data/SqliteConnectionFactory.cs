using Microsoft.Data.Sqlite;

namespace MeuAssistente.Data;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public SqliteConnectionFactory(string databasePath)
    {
        DatabasePath = databasePath;
        DatabasePaths.PrepararBancoLegadoSeNecessario(databasePath);
        var pasta = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Pooling = true
        }.ToString();
    }

    public SqliteConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
        return connection;
    }
}
