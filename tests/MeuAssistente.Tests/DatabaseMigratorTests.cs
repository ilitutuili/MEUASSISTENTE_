using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using MeuAssistente.Data;
using Xunit;

namespace MeuAssistente.Tests;

[SupportedOSPlatform("windows")]
public sealed class DatabaseMigratorTests
{
    [Fact]
    public void MigraEsquemaPythonMantendoDadosCriandoBackupProtegidoEValidandoIntegridade()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"MeuAssistenteTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, "assistente.db");
        try
        {
            CriarBancoPython(caminho);
            var conexoes = new SqliteConnectionFactory(caminho);

            new DatabaseMigrator(conexoes).Migrar();

            using var banco = conexoes.Create();
            Assert.Equal(2L, Escalar<long>(banco, "PRAGMA user_version;"));
            Assert.Equal(1L, Escalar<long>(banco, "SELECT COUNT(*) FROM usuarios;"));
            Assert.Equal("senha-legada-fixture", Escalar<string>(banco, "SELECT senha FROM usuarios WHERE id = 1;"));
            Assert.Equal(1L, Escalar<long>(banco, "SELECT COUNT(*) FROM tarefas WHERE tarefa = 'Estudar C#';"));
            Assert.Equal("2060-09-29", Escalar<string>(banco, "SELECT data FROM compromissos WHERE id = 1;"));
            Assert.Equal("14:30", Escalar<string>(banco, "SELECT horario FROM compromissos WHERE id = 1;"));
            Assert.Equal("Reunião com João", Escalar<string>(banco, "SELECT texto FROM anotacoes WHERE id = 1;"));
            Assert.Equal("ok", Escalar<string>(banco, "PRAGMA integrity_check;"));

            var backup = Assert.Single(Directory.GetFiles(pasta, "assistente.db.backup-*.dpapi"));
            var bytes = File.ReadAllBytes(backup);
            var conteudo = System.Security.Cryptography.ProtectedData.Unprotect(
                bytes, "MeuAssistente.DatabaseBackup.v1"u8.ToArray(),
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            try
            {
                Assert.Contains("senha-legada-fixture", System.Text.Encoding.Latin1.GetString(conteudo));
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(conteudo);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    private static void CriarBancoPython(string caminho)
    {
        using var conexao = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = caminho,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = """
            CREATE TABLE usuarios (id INTEGER PRIMARY KEY AUTOINCREMENT, nome TEXT NOT NULL, email TEXT NOT NULL UNIQUE, senha TEXT NOT NULL);
            CREATE TABLE tarefas (id INTEGER PRIMARY KEY AUTOINCREMENT, usuario_id INTEGER NOT NULL, tarefa TEXT NOT NULL, prioridade TEXT NOT NULL, status TEXT NOT NULL);
            CREATE TABLE compromissos (id INTEGER PRIMARY KEY AUTOINCREMENT, usuario_id INTEGER NOT NULL, titulo TEXT NOT NULL, descricao TEXT, data TEXT NOT NULL, horario TEXT NOT NULL);
            CREATE TABLE anotacoes (id INTEGER PRIMARY KEY AUTOINCREMENT, usuario_id INTEGER NOT NULL, titulo TEXT NOT NULL, texto TEXT NOT NULL);
            INSERT INTO usuarios (id, nome, email, senha) VALUES (1, 'Ana', 'ana@example.com', 'senha-legada-fixture');
            INSERT INTO tarefas (id, usuario_id, tarefa, prioridade, status) VALUES (1, 1, 'Estudar C#', 'Alta', 'Pendente');
            INSERT INTO compromissos (id, usuario_id, titulo, descricao, data, horario) VALUES (1, 1, 'Reunião', '', '29/09/2060', '14:30');
            INSERT INTO anotacoes (id, usuario_id, titulo, texto) VALUES (1, 1, 'Lembrete', 'Reunião com João');
            PRAGMA user_version = 0;
            """;
        comando.ExecuteNonQuery();
    }

    private static T Escalar<T>(SqliteConnection conexao, string sql)
    {
        using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return (T)Convert.ChangeType(comando.ExecuteScalar()!, typeof(T));
    }
}
