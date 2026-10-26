using MeuAssistente.Data;
using Microsoft.Data.Sqlite;

if (args.Length != 1) throw new ArgumentException("Passe o caminho da cópia temporária do banco.");
var caminho = args[0];
var antes = Contar(caminho);
new DatabaseMigrator(new SqliteConnectionFactory(caminho)).Migrar();
using var banco = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = caminho }.ToString());
banco.Open();
var versao = Escalar<long>(banco, "PRAGMA user_version;");
var integridade = Escalar<string>(banco, "PRAGMA integrity_check;");
var depois = new[]
{
    Escalar<long>(banco, "SELECT COUNT(*) FROM usuarios;"),
    Escalar<long>(banco, "SELECT COUNT(*) FROM tarefas;"),
    Escalar<long>(banco, "SELECT COUNT(*) FROM compromissos;"),
    Escalar<long>(banco, "SELECT COUNT(*) FROM anotacoes;")
};
if (!antes.SequenceEqual(depois)) throw new InvalidDataException("As contagens mudaram na migração.");
if (versao != 2 || integridade != "ok") throw new InvalidDataException("Versão ou integridade inválida após migração.");
Console.WriteLine($"database_migrated=True users={depois[0]} tasks={depois[1]} appointments={depois[2]} notes={depois[3]} version={versao} integrity={integridade}");

static long[] Contar(string caminho)
{
    using var banco = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = caminho }.ToString());
    banco.Open();
    return new[]
    {
        Escalar<long>(banco, "SELECT COUNT(*) FROM usuarios;"),
        Escalar<long>(banco, "SELECT COUNT(*) FROM tarefas;"),
        Escalar<long>(banco, "SELECT COUNT(*) FROM compromissos;"),
        Escalar<long>(banco, "SELECT COUNT(*) FROM anotacoes;")
    };
}

static T Escalar<T>(SqliteConnection banco, string sql)
{
    using var comando = banco.CreateCommand();
    comando.CommandText = sql;
    return (T)Convert.ChangeType(comando.ExecuteScalar()!, typeof(T));
}
