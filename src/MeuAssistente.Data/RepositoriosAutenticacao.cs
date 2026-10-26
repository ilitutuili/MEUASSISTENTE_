using MeuAssistente.Application;
using MeuAssistente.Domain;
using Microsoft.Data.Sqlite;

namespace MeuAssistente.Data;

public sealed class RepositorioUsuarios(SqliteConnectionFactory conexoes) : IRepositorioUsuarios
{
    public CredencialArmazenada? ObterPorEmail(string email)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT id, nome, email, senha, senha_hash, senha_salt, senha_iteracoes
            FROM usuarios WHERE email = $email COLLATE NOCASE LIMIT 1;
            """;
        comando.Parameters.AddWithValue("$email", email);
        using var leitor = comando.ExecuteReader();
        if (!leitor.Read()) return null;

        var hash = leitor.IsDBNull(4) ? null : (byte[])leitor[4];
        var salt = leitor.IsDBNull(5) ? null : (byte[])leitor[5];
        var senhaLegada = hash is null ? leitor.GetString(3) : null;
        return new(new Usuario(leitor.GetInt64(0), leitor.GetString(1), leitor.GetString(2)),
            senhaLegada, hash, salt) { Iteracoes = leitor.IsDBNull(6) ? 0 : leitor.GetInt32(6) };
    }

    public CredencialArmazenada? ObterPorId(long id)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT id, nome, email, senha, senha_hash, senha_salt, senha_iteracoes FROM usuarios WHERE id = $id;";
        comando.Parameters.AddWithValue("$id", id);
        using var leitor = comando.ExecuteReader();
        if (!leitor.Read()) return null;
        var hash = leitor.IsDBNull(4) ? null : (byte[])leitor[4];
        var salt = leitor.IsDBNull(5) ? null : (byte[])leitor[5];
        return new(new Usuario(leitor.GetInt64(0), leitor.GetString(1), leitor.GetString(2)),
            hash is null ? leitor.GetString(3) : null, hash, salt)
            { Iteracoes = leitor.IsDBNull(6) ? 0 : leitor.GetInt32(6) };
    }

    public bool Criar(string nome, string email, byte[] hash, byte[] salt, int iteracoes)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        // A coluna senha é mantida por compatibilidade com o banco Python e fica vazia.
        comando.CommandText = """
            INSERT INTO usuarios (nome, email, senha, senha_hash, senha_salt, senha_iteracoes)
            VALUES ($nome, $email, '', $hash, $salt, $iteracoes);
            """;
        comando.Parameters.AddWithValue("$nome", nome);
        comando.Parameters.AddWithValue("$email", email);
        comando.Parameters.AddWithValue("$hash", hash);
        comando.Parameters.AddWithValue("$salt", salt);
        comando.Parameters.AddWithValue("$iteracoes", iteracoes);
        try { comando.ExecuteNonQuery(); return true; }
        catch (SqliteException erro) when (erro.SqliteErrorCode == 19) { return false; }
    }

    public void AtualizarSenhaLegada(long usuarioId, byte[] hash, byte[] salt, int iteracoes)
    {
        using var conexao = conexoes.Create();
        using var transacao = conexao.BeginTransaction();
        using var comando = conexao.CreateCommand();
        comando.Transaction = transacao;
        comando.CommandText = "UPDATE usuarios SET senha = '', senha_hash = $hash, senha_salt = $salt, senha_iteracoes = $iteracoes WHERE id = $id AND senha_hash IS NULL;";
        comando.Parameters.AddWithValue("$hash", hash);
        comando.Parameters.AddWithValue("$salt", salt);
        comando.Parameters.AddWithValue("$iteracoes", iteracoes);
        comando.Parameters.AddWithValue("$id", usuarioId);
        comando.ExecuteNonQuery();
        transacao.Commit();
    }
}

public sealed class RepositorioSessao(SqliteConnectionFactory conexoes) : IRepositorioSessao
{
    public long? ObterUsuarioId()
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT usuario_id FROM sessao_ativa WHERE id = 1;";
        var valor = comando.ExecuteScalar();
        return valor is null or DBNull ? null : Convert.ToInt64(valor);
    }

    public void Salvar(long usuarioId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "INSERT OR REPLACE INTO sessao_ativa (id, usuario_id) VALUES (1, $id);";
        comando.Parameters.AddWithValue("$id", usuarioId);
        comando.ExecuteNonQuery();
    }

    public void Limpar()
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "DELETE FROM sessao_ativa;";
        comando.ExecuteNonQuery();
    }
}
