using System.Globalization;
using Microsoft.Data.Sqlite;
using System.Runtime.Versioning;

namespace MeuAssistente.Data;

/// <summary>Aplica migrações numeradas ao banco Python existente ou a uma instalação nova.</summary>
[SupportedOSPlatform("windows")]
public sealed class DatabaseMigrator(SqliteConnectionFactory connections)
{
    public void Migrar()
    {
        using var connection = connections.Create();
        var versao = ExecutarEscalar<long>(connection, null, "PRAGMA user_version;");
        if (versao > 2)
            throw new InvalidOperationException($"O banco está na versão {versao}, mais nova que esta versão do aplicativo.");

        var contagensOriginais = LerContagens(connection, null);
        if (versao < 2 && new FileInfo(connections.DatabasePath).Length > 0)
            CriarBackupProtegido(connection, connections.DatabasePath);
        if (versao < 2)
        {
            using var transaction = connection.BeginTransaction();
            if (versao < 1)
            {
                CriarEsquemaEAtualizarLegado(connection, transaction);
                Executar(connection, transaction, "PRAGMA user_version = 1;");
            }
            if (versao < 2)
            {
                AdicionarColunaSeAusente(connection, transaction, "usuarios", "senha_iteracoes", "INTEGER NULL");
                Executar(connection, transaction,
                    "UPDATE usuarios SET senha_iteracoes = 600000 WHERE senha_hash IS NOT NULL AND senha_salt IS NOT NULL AND senha_iteracoes IS NULL;");
                Executar(connection, transaction, "PRAGMA user_version = 2;");
            }
            ValidarMigracao(connection, transaction, contagensOriginais, 2);
            transaction.Commit();
        }
        else ValidarIntegridade(connection, null);
    }

    private static void CriarBackupProtegido(SqliteConnection original, string caminhoBanco)
    {
        var pasta = Path.GetDirectoryName(caminhoBanco)!;
        var temporario = Path.Combine(pasta, $"{Path.GetFileName(caminhoBanco)}.{Guid.NewGuid():N}.tmp");
        var caminhoBackup = caminhoBanco + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N") + ".dpapi";
        try
        {
            using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder
                   { DataSource = temporario, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
            {
                backup.Open();
                original.BackupDatabase(backup);
            }
            var dados = File.ReadAllBytes(temporario);
            try
            {
                var protegidos = System.Security.Cryptography.ProtectedData.Protect(
                    dados, "MeuAssistente.DatabaseBackup.v1"u8.ToArray(),
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
                File.WriteAllBytes(caminhoBackup, protegidos);
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(protegidos);
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(dados); }
        }
        finally
        {
            if (File.Exists(temporario)) File.Delete(temporario);
        }
    }

    private static Dictionary<string, long> LerContagens(SqliteConnection c, SqliteTransaction? t)
    {
        var contagens = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var tabela in new[] { "usuarios", "tarefas", "compromissos", "anotacoes" })
        {
            var existe = ExecutarEscalar<long>(c, t,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$nome;", ("$nome", tabela)) > 0;
            if (existe) contagens[tabela] = ExecutarEscalar<long>(c, t, $"SELECT COUNT(*) FROM \"{tabela}\";");
        }
        return contagens;
    }

    private static void ValidarMigracao(SqliteConnection c, SqliteTransaction t,
        Dictionary<string, long> originais, int versaoEsperada)
    {
        foreach (var (tabela, quantidade) in originais)
        {
            var atual = ExecutarEscalar<long>(c, t, $"SELECT COUNT(*) FROM \"{tabela}\";");
            if (atual != quantidade)
                throw new InvalidDataException($"A quantidade de registros de {tabela} mudou durante a migração; a transação foi desfeita.");
        }
        if (ExecutarEscalar<long>(c, t, "PRAGMA user_version;") != versaoEsperada)
            throw new InvalidDataException("A versão do banco não foi atualizada corretamente.");
        ValidarIntegridade(c, t);
    }

    private static void ValidarIntegridade(SqliteConnection c, SqliteTransaction? t)
    {
        using var comando = c.CreateCommand();
        comando.Transaction = t;
        comando.CommandText = "PRAGMA integrity_check;";
        var resultado = Convert.ToString(comando.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (!string.Equals(resultado, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A verificação de integridade do SQLite falhou.");
    }

    private static void CriarEsquemaEAtualizarLegado(SqliteConnection c, SqliteTransaction t)
    {
        Executar(c, t, """
            CREATE TABLE IF NOT EXISTS usuarios (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nome TEXT NOT NULL,
                email TEXT NOT NULL UNIQUE,
                senha TEXT NOT NULL DEFAULT '',
                senha_hash BLOB NULL,
                senha_salt BLOB NULL,
                senha_iteracoes INTEGER NULL
            );
            CREATE TABLE IF NOT EXISTS compromissos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                usuario_id INTEGER NOT NULL,
                titulo TEXT NOT NULL,
                descricao TEXT,
                data TEXT NOT NULL,
                horario TEXT NOT NULL,
                FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS tarefas (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                usuario_id INTEGER NOT NULL,
                tarefa TEXT NOT NULL,
                prioridade TEXT NOT NULL,
                status TEXT NOT NULL,
                FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS anotacoes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                usuario_id INTEGER NOT NULL,
                titulo TEXT NOT NULL,
                texto TEXT NOT NULL,
                FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS sessao_ativa (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                usuario_id INTEGER NOT NULL,
                FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS preferencias_ia (
                usuario_id INTEGER PRIMARY KEY,
                provedor TEXT NOT NULL,
                endpoint TEXT NOT NULL,
                modelo TEXT NOT NULL,
                pesquisa_web INTEGER NOT NULL DEFAULT 0,
                chave_api_protegida BLOB NULL,
                FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS preferencias_visuais (
                usuario_id INTEGER PRIMARY KEY,
                tema TEXT NOT NULL DEFAULT 'Escuro',
                cor_destaque TEXT NOT NULL DEFAULT '#10A37F',
                FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE
            );
            """);

        // Compatibilidade com os esquemas incrementais publicados pelo app Python.
        AdicionarColunaSeAusente(c, t, "usuarios", "senha_hash", "BLOB NULL");
        AdicionarColunaSeAusente(c, t, "usuarios", "senha_salt", "BLOB NULL");
        AdicionarColunaSeAusente(c, t, "usuarios", "senha_iteracoes", "INTEGER NULL");
        AdicionarColunaSeAusente(c, t, "preferencias_ia", "pesquisa_web", "INTEGER NOT NULL DEFAULT 0");
        AdicionarColunaSeAusente(c, t, "preferencias_ia", "chave_api_protegida", "BLOB NULL");

        NormalizarDatas(c, t);
        Executar(c, t, "CREATE INDEX IF NOT EXISTS ix_compromissos_usuario_data_horario ON compromissos(usuario_id, data, horario);");
        Executar(c, t, "CREATE INDEX IF NOT EXISTS ix_tarefas_usuario_status ON tarefas(usuario_id, status);");
    }

    private static void NormalizarDatas(SqliteConnection c, SqliteTransaction t)
    {
        if (!TabelaExiste(c, t, "compromissos")) return;

        using var select = c.CreateCommand();
        select.Transaction = t;
        select.CommandText = "SELECT id, data, horario FROM compromissos;";
        using var reader = select.ExecuteReader();
        var linhas = new List<(long Id, string Data, string Horario)>();
        while (reader.Read())
            linhas.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
        reader.Close();

        foreach (var linha in linhas)
        {
            if (!DateOnly.TryParseExact(linha.Data,
                    new[] { "dd/MM/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var data))
                throw new InvalidDataException($"A data do compromisso {linha.Id} ('{linha.Data}') não pôde ser migrada. O banco original foi preservado.");

            if (!TimeOnly.TryParseExact(linha.Horario,
                    new[] { "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss" }, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var horario))
                throw new InvalidDataException($"O horário do compromisso {linha.Id} ('{linha.Horario}') não pôde ser migrado. O banco original foi preservado.");

            using var update = c.CreateCommand();
            update.Transaction = t;
            update.CommandText = "UPDATE compromissos SET data = $data, horario = $horario WHERE id = $id;";
            update.Parameters.AddWithValue("$data", data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("$horario", horario.ToString("HH:mm", CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("$id", linha.Id);
            update.ExecuteNonQuery();
        }
    }

    private static void AdicionarColunaSeAusente(SqliteConnection c, SqliteTransaction t, string tabela, string coluna, string definicao)
    {
        if (!TabelaExiste(c, t, tabela) || ColunaExiste(c, t, tabela, coluna)) return;
        Executar(c, t, $"ALTER TABLE \"{tabela}\" ADD COLUMN \"{coluna}\" {definicao};");
    }

    private static bool TabelaExiste(SqliteConnection c, SqliteTransaction t, string tabela)
        => ExecutarEscalar<long>(c, t,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $nome;", ("$nome", tabela)) > 0;

    private static bool ColunaExiste(SqliteConnection c, SqliteTransaction t, string tabela, string coluna)
    {
        using var command = c.CreateCommand();
        command.Transaction = t;
        command.CommandText = $"PRAGMA table_info(\"{tabela}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), coluna, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static void Executar(SqliteConnection c, SqliteTransaction t, string sql)
    {
        using var command = c.CreateCommand();
        command.Transaction = t;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static T ExecutarEscalar<T>(SqliteConnection c, SqliteTransaction? t, string sql,
        params (string Nome, object Valor)[] parametros)
    {
        using var command = c.CreateCommand();
        command.Transaction = t;
        command.CommandText = sql;
        foreach (var (nome, valor) in parametros)
            command.Parameters.AddWithValue(nome, valor);
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T), CultureInfo.InvariantCulture);
    }
}
