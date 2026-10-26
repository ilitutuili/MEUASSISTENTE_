using MeuAssistente.Domain;

namespace MeuAssistente.Application;

public interface IConfirmador
{
    bool ConfirmarExclusao(string mensagem);
}

public interface IRepositorioTarefas
{
    IReadOnlyList<Tarefa> Listar(long usuarioId);
    Tarefa Criar(long usuarioId, string titulo, string prioridade);
    bool Editar(long usuarioId, long tarefaId, string titulo, string prioridade);
    void DefinirConcluida(long usuarioId, long tarefaId, bool concluida);
    void Excluir(long usuarioId, long tarefaId);
}

public interface IRepositorioCompromissos
{
    IReadOnlyList<Compromisso> Listar(long usuarioId, DateOnly? de = null, DateOnly? ate = null);
    Compromisso Criar(long usuarioId, string titulo, string? descricao, DateOnly data, TimeOnly horario);
    bool Editar(long usuarioId, long compromissoId, string titulo, string? descricao, DateOnly data, TimeOnly horario);
    void Excluir(long usuarioId, long compromissoId);
}

public interface IRepositorioAnotacoes
{
    IReadOnlyList<Anotacao> Listar(long usuarioId);
    Anotacao? Obter(long usuarioId, long anotacaoId);
    Anotacao Criar(long usuarioId, string titulo, string texto);
    bool Editar(long usuarioId, long anotacaoId, string titulo, string texto);
    void Excluir(long usuarioId, long anotacaoId);
}

public sealed class ServicoTarefas(IRepositorioTarefas repositorio)
{
    private static readonly string[] PrioridadesValidas = ["Baixa", "Média", "Alta"];

    public IReadOnlyList<Tarefa> Listar(long usuarioId) => repositorio.Listar(usuarioId);
    public Tarefa Criar(long usuarioId, string titulo, string prioridade)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("Informe o título da tarefa.");
        return repositorio.Criar(usuarioId, titulo.Trim(), NormalizarPrioridade(prioridade));
    }
    public bool Editar(long usuarioId, long tarefaId, string titulo, string prioridade)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("Informe o título da tarefa.");
        return repositorio.Editar(usuarioId, tarefaId, titulo.Trim(), NormalizarPrioridade(prioridade));
    }
    public void DefinirConcluida(long usuarioId, long tarefaId, bool concluida)
        => repositorio.DefinirConcluida(usuarioId, tarefaId, concluida);
    public void Excluir(long usuarioId, long tarefaId) => repositorio.Excluir(usuarioId, tarefaId);

    private static string NormalizarPrioridade(string prioridade)
        => PrioridadesValidas.FirstOrDefault(item => string.Equals(item, prioridade?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException("Escolha uma prioridade válida.");
}

public sealed class ServicoAgenda(IRepositorioCompromissos repositorio)
{
    public IReadOnlyList<Compromisso> Listar(long usuarioId, DateOnly? de = null, DateOnly? ate = null)
        => repositorio.Listar(usuarioId, de, ate);
    public Compromisso Criar(long usuarioId, string titulo, string? descricao, DateOnly data, TimeOnly horario)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("Informe o título do compromisso.");
        return repositorio.Criar(usuarioId, titulo.Trim(), descricao?.Trim(), data, horario);
    }
    public bool Editar(long usuarioId, long compromissoId, string titulo, string? descricao, DateOnly data, TimeOnly horario)
    {
        if (string.IsNullOrWhiteSpace(titulo)) throw new ArgumentException("Informe o título do compromisso.");
        return repositorio.Editar(usuarioId, compromissoId, titulo.Trim(), descricao?.Trim(), data, horario);
    }
    public void Excluir(long usuarioId, long compromissoId) => repositorio.Excluir(usuarioId, compromissoId);
}

public sealed class ServicoAnotacoes(IRepositorioAnotacoes repositorio)
{
    public IReadOnlyList<Anotacao> Listar(long usuarioId) => repositorio.Listar(usuarioId);
    public Anotacao? Obter(long usuarioId, long anotacaoId) => repositorio.Obter(usuarioId, anotacaoId);
    public Anotacao Criar(long usuarioId, string titulo, string texto)
    {
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Preencha o título e o texto da anotação.");
        return repositorio.Criar(usuarioId, titulo.Trim(), texto);
    }
    public bool Editar(long usuarioId, long anotacaoId, string titulo, string texto)
    {
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Preencha o título e o texto da anotação.");
        return repositorio.Editar(usuarioId, anotacaoId, titulo.Trim(), texto);
    }
    public void Excluir(long usuarioId, long anotacaoId) => repositorio.Excluir(usuarioId, anotacaoId);
}
