namespace MeuAssistente.Domain;

public sealed record Usuario(long Id, string Nome, string Email);

public sealed record Tarefa(
    long Id, long UsuarioId, string Titulo, string Prioridade, string Status);

public sealed record Compromisso(
    long Id, long UsuarioId, string Titulo, string? Descricao,
    DateOnly Data, TimeOnly Horario);

public sealed record Anotacao(long Id, long UsuarioId, string Titulo, string Texto);

public sealed record PreferenciaVisual(long UsuarioId, string Tema, string CorDestaque);

public sealed record PreferenciaIa(
    long UsuarioId, string Provedor, string Endpoint, string Modelo,
    bool PesquisaWebAtiva);
