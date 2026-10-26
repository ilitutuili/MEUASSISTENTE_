using System.Globalization;
using MeuAssistente.Application;
using MeuAssistente.Domain;
using Microsoft.Data.Sqlite;

namespace MeuAssistente.Data;

public sealed class RepositorioTarefas(SqliteConnectionFactory conexoes) : IRepositorioTarefas
{
    public IReadOnlyList<Tarefa> Listar(long usuarioId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT id, usuario_id, tarefa, prioridade, status FROM tarefas WHERE usuario_id = $usuario ORDER BY id DESC;";
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        using var leitor = comando.ExecuteReader();
        var itens = new List<Tarefa>();
        while (leitor.Read())
            itens.Add(new(leitor.GetInt64(0), leitor.GetInt64(1), leitor.GetString(2), leitor.GetString(3), leitor.GetString(4)));
        return itens;
    }

    public Tarefa Criar(long usuarioId, string titulo, string prioridade)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "INSERT INTO tarefas (usuario_id, tarefa, prioridade, status) VALUES ($usuario, $titulo, $prioridade, 'Pendente'); SELECT last_insert_rowid();";
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.Parameters.AddWithValue("$titulo", titulo);
        comando.Parameters.AddWithValue("$prioridade", prioridade);
        var id = (long)comando.ExecuteScalar()!;
        return new(id, usuarioId, titulo, prioridade, "Pendente");
    }

    public bool Editar(long usuarioId, long tarefaId, string titulo, string prioridade)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "UPDATE tarefas SET tarefa = $titulo, prioridade = $prioridade WHERE id = $id AND usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$titulo", titulo);
        comando.Parameters.AddWithValue("$prioridade", prioridade);
        comando.Parameters.AddWithValue("$id", tarefaId);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        return comando.ExecuteNonQuery() > 0;
    }

    public void DefinirConcluida(long usuarioId, long tarefaId, bool concluida)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "UPDATE tarefas SET status = $status WHERE id = $id AND usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$status", concluida ? "Concluída" : "Pendente");
        comando.Parameters.AddWithValue("$id", tarefaId);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.ExecuteNonQuery();
    }

    public void Excluir(long usuarioId, long tarefaId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "DELETE FROM tarefas WHERE id = $id AND usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$id", tarefaId);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.ExecuteNonQuery();
    }
}

public sealed class RepositorioCompromissos(SqliteConnectionFactory conexoes) : IRepositorioCompromissos
{
    public IReadOnlyList<Compromisso> Listar(long usuarioId, DateOnly? de = null, DateOnly? ate = null)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT id, usuario_id, titulo, descricao, data, horario FROM compromissos
            WHERE usuario_id = $usuario
              AND ($de IS NULL OR data >= $de)
              AND ($ate IS NULL OR data <= $ate)
            ORDER BY data, horario, id;
            """;
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.Parameters.AddWithValue("$de", (object?)de?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        comando.Parameters.AddWithValue("$ate", (object?)ate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? DBNull.Value);
        using var leitor = comando.ExecuteReader();
        var itens = new List<Compromisso>();
        while (leitor.Read())
        {
            var data = DateOnly.ParseExact(leitor.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var horario = TimeOnly.ParseExact(leitor.GetString(5), "HH:mm", CultureInfo.InvariantCulture);
            itens.Add(new(leitor.GetInt64(0), leitor.GetInt64(1), leitor.GetString(2),
                leitor.IsDBNull(3) ? null : leitor.GetString(3), data, horario));
        }
        return itens;
    }

    public Compromisso Criar(long usuarioId, string titulo, string? descricao, DateOnly data, TimeOnly horario)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO compromissos (usuario_id, titulo, descricao, data, horario)
            VALUES ($usuario, $titulo, $descricao, $data, $horario);
            SELECT last_insert_rowid();
            """;
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.Parameters.AddWithValue("$titulo", titulo);
        comando.Parameters.AddWithValue("$descricao", (object?)descricao ?? DBNull.Value);
        comando.Parameters.AddWithValue("$data", data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        comando.Parameters.AddWithValue("$horario", horario.ToString("HH:mm", CultureInfo.InvariantCulture));
        var id = (long)comando.ExecuteScalar()!;
        return new(id, usuarioId, titulo, descricao, data, horario);
    }

    public bool Editar(long usuarioId, long compromissoId, string titulo, string? descricao, DateOnly data, TimeOnly horario)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "UPDATE compromissos SET titulo = $titulo, descricao = $descricao, data = $data, horario = $horario WHERE id = $id AND usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$titulo", titulo);
        comando.Parameters.AddWithValue("$descricao", (object?)descricao ?? DBNull.Value);
        comando.Parameters.AddWithValue("$data", data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        comando.Parameters.AddWithValue("$horario", horario.ToString("HH:mm", CultureInfo.InvariantCulture));
        comando.Parameters.AddWithValue("$id", compromissoId);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        return comando.ExecuteNonQuery() > 0;
    }

    public void Excluir(long usuarioId, long compromissoId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "DELETE FROM compromissos WHERE id = $id AND usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$id", compromissoId);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.ExecuteNonQuery();
    }
}

public sealed class RepositorioAnotacoes(SqliteConnectionFactory conexoes) : IRepositorioAnotacoes
{
    public IReadOnlyList<Anotacao> Listar(long usuarioId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT id, usuario_id, titulo, texto FROM anotacoes WHERE usuario_id = $usuario ORDER BY id DESC;";
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        using var leitor = comando.ExecuteReader();
        var itens = new List<Anotacao>();
        while (leitor.Read())
            itens.Add(new(leitor.GetInt64(0), leitor.GetInt64(1), leitor.GetString(2), leitor.GetString(3)));
        return itens;
    }

    public Anotacao? Obter(long usuarioId, long anotacaoId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT id, usuario_id, titulo, texto FROM anotacoes WHERE id = $id AND usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$id", anotacaoId);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        using var leitor = comando.ExecuteReader();
        return leitor.Read() ? new(leitor.GetInt64(0), leitor.GetInt64(1), leitor.GetString(2), leitor.GetString(3)) : null;
    }

    public Anotacao Criar(long usuarioId, string titulo, string texto)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "INSERT INTO anotacoes (usuario_id, titulo, texto) VALUES ($usuario, $titulo, $texto); SELECT last_insert_rowid();";
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.Parameters.AddWithValue("$titulo", titulo);
        comando.Parameters.AddWithValue("$texto", texto);
        var id = (long)comando.ExecuteScalar()!;
        return new(id, usuarioId, titulo, texto);
    }

    public bool Editar(long usuarioId, long anotacaoId, string titulo, string texto)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "UPDATE anotacoes SET titulo = $titulo, texto = $texto WHERE id = $id AND usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$titulo", titulo);
        comando.Parameters.AddWithValue("$texto", texto);
        comando.Parameters.AddWithValue("$id", anotacaoId);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        return comando.ExecuteNonQuery() > 0;
    }

    public void Excluir(long usuarioId, long anotacaoId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "DELETE FROM anotacoes WHERE id = $id AND usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$id", anotacaoId);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.ExecuteNonQuery();
    }
}
