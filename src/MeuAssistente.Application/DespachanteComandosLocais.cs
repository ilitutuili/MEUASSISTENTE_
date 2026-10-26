using MeuAssistente.Domain;

namespace MeuAssistente.Application;

public sealed record ConcluirTarefaPorIdLocal(long Id) : ComandoLocal;
public sealed record ExcluirTarefaPorIdLocal(long Id) : ComandoLocal;
public sealed record ExcluirCompromissoPorIdLocal(long Id) : ComandoLocal;

public sealed record OpcaoAmbigua(long Id, string Titulo);
public sealed record PreparacaoHandler(string Texto, ComandoLocal? Proposta = null,
    IReadOnlyList<OpcaoAmbigua>? Opcoes = null, SecaoLocal? Secao = null);
public sealed record ResultadoDespacho(string Texto, Guid? PropostaId = null, SecaoLocal? Secao = null);

public interface IManipuladorComandoLocal
{
    IReadOnlyList<Type> TiposComando { get; }
    PreparacaoHandler Preparar(long usuarioId, ComandoLocal comando);
    string Executar(long usuarioId, ComandoLocal comando);
}

/// <summary>Direciona comandos para handlers independentes e exige confirmação antes de alterar dados.</summary>
public sealed class DespachanteComandosLocais
{
    private readonly Dictionary<Type, IManipuladorComandoLocal> _manipuladores;
    private readonly Dictionary<Guid, (long UsuarioId, ComandoLocal Comando)> _propostas = new();
    private readonly Dictionary<long, (ComandoLocal Comando, IReadOnlyList<OpcaoAmbigua> Opcoes)> _selecaoPendente = new();

    public DespachanteComandosLocais(IEnumerable<IManipuladorComandoLocal> manipuladores)
        => _manipuladores = manipuladores.SelectMany(item => item.TiposComando.Select(tipo => (tipo, item)))
            .ToDictionary(par => par.tipo, par => par.item);

    public ResultadoDespacho Preparar(long usuarioId, ResultadoInterpretacaoLocal interpretacao, string? respostaUsuario = null)
    {
        if (_selecaoPendente.TryGetValue(usuarioId, out var selecao))
        {
            var resposta = InterpretadorLocal.Normalizar(respostaUsuario ?? "");
            if (resposta is "cancelar" or "cancela" or "deixa")
            {
                _selecaoPendente.Remove(usuarioId);
                return new("Seleção cancelada. Nenhuma alteração foi feita.");
            }

            if (int.TryParse(resposta, out var indice) && indice > 0 && indice <= selecao.Opcoes.Count)
            {
                _selecaoPendente.Remove(usuarioId);
                var escolhido = selecao.Opcoes[indice - 1];
                var comando = ConverterParaId(selecao.Comando, escolhido.Id);
                return PrepararAcao(usuarioId, comando);
            }

            // Um comando novo substitui a pergunta de desambiguação anterior.
            if (interpretacao.Comando is null)
                return new($"Escolha um número de 1 a {selecao.Opcoes.Count} ou escreva “cancelar”.");
            _selecaoPendente.Remove(usuarioId);
        }

        if (interpretacao.Comando is null)
            return new(interpretacao.Resposta ?? "Não entendi esse comando.");
        return PrepararAcao(usuarioId, interpretacao.Comando);
    }

    public ResultadoDespacho Confirmar(long usuarioId, Guid propostaId)
    {
        if (!_propostas.Remove(propostaId, out var proposta) || proposta.UsuarioId != usuarioId)
            return new("Essa ação expirou ou pertence a outra sessão. Envie o comando novamente.");
        if (!_manipuladores.TryGetValue(proposta.Comando.GetType(), out var manipulador))
            return new("Não encontrei um manipulador para essa ação.");
        return new(manipulador.Executar(usuarioId, proposta.Comando));
    }

    public void Cancelar(long usuarioId, Guid propostaId)
    {
        if (_propostas.TryGetValue(propostaId, out var proposta) && proposta.UsuarioId == usuarioId)
            _propostas.Remove(propostaId);
    }

    public void LimparUsuario(long usuarioId)
    {
        _selecaoPendente.Remove(usuarioId);
        foreach (var id in _propostas.Where(item => item.Value.UsuarioId == usuarioId).Select(item => item.Key).ToArray())
            _propostas.Remove(id);
    }

    private ResultadoDespacho PrepararAcao(long usuarioId, ComandoLocal comando)
    {
        if (!_manipuladores.TryGetValue(comando.GetType(), out var manipulador))
            return new("Não entendi esse comando.");

        var preparacao = manipulador.Preparar(usuarioId, comando);
        if (preparacao.Opcoes is { Count: > 1 } opcoes)
        {
            _selecaoPendente[usuarioId] = (comando, opcoes);
            var lista = string.Join("\n", opcoes.Select((item, indice) => $"{indice + 1}. {item.Titulo} (ID {item.Id})"));
            return new($"Encontrei mais de um item com esse nome. Qual deles?\n{lista}\nResponda com o número.");
        }
        if (preparacao.Proposta is not null)
        {
            var id = Guid.NewGuid();
            _propostas[id] = (usuarioId, preparacao.Proposta);
            return new(preparacao.Texto, id);
        }
        return new(preparacao.Texto, Secao: preparacao.Secao);
    }

    private static ComandoLocal ConverterParaId(ComandoLocal comando, long id) => comando switch
    {
        ConcluirTarefaPorNomeLocal => new ConcluirTarefaPorIdLocal(id),
        ExcluirTarefaPorNomeLocal => new ExcluirTarefaPorIdLocal(id),
        ExcluirCompromissoPorNomeLocal => new ExcluirCompromissoPorIdLocal(id),
        _ => comando
    };
}

public sealed class ManipuladorCriarTarefa(ServicoTarefas tarefas) : IManipuladorComandoLocal
{
    public IReadOnlyList<Type> TiposComando { get; } = new[] { typeof(CriarTarefaLocal) };
    public PreparacaoHandler Preparar(long usuarioId, ComandoLocal comando)
    {
        var tarefa = (CriarTarefaLocal)comando;
        return new($"Criar tarefa “{tarefa.Titulo}” com prioridade média?", tarefa);
    }
    public string Executar(long usuarioId, ComandoLocal comando)
    {
        var tarefa = (CriarTarefaLocal)comando;
        tarefas.Criar(usuarioId, tarefa.Titulo, "Média");
        return $"Tarefa criada: {tarefa.Titulo}";
    }
}

public sealed class ManipuladorConcluirTarefa(ServicoTarefas tarefas) : IManipuladorComandoLocal
{
    public IReadOnlyList<Type> TiposComando { get; } = new[] { typeof(ConcluirTarefaPorNomeLocal), typeof(ConcluirTarefaPorIdLocal) };
    public PreparacaoHandler Preparar(long usuarioId, ComandoLocal comando)
    {
        if (comando is ConcluirTarefaPorIdLocal porId)
        {
            var item = tarefas.Listar(usuarioId).FirstOrDefault(tarefa => tarefa.Id == porId.Id);
            return item is null ? new("Não encontrei essa tarefa.") : new($"Concluir a tarefa “{item.Titulo}”?", porId);
        }
        var nome = ((ConcluirTarefaPorNomeLocal)comando).Nome;
        var encontradas = tarefas.Listar(usuarioId).Where(item => InterpretadorLocal.Normalizar(item.Titulo) == InterpretadorLocal.Normalizar(nome)).ToArray();
        return encontradas.Length switch
        {
            0 => new("Não encontrei essa tarefa."),
            1 => new($"Concluir a tarefa “{encontradas[0].Titulo}”?", new ConcluirTarefaPorIdLocal(encontradas[0].Id)),
            _ => new("", Opcoes: encontradas.Select(item => new OpcaoAmbigua(item.Id, item.Titulo)).ToArray())
        };
    }
    public string Executar(long usuarioId, ComandoLocal comando)
    {
        var id = ((ConcluirTarefaPorIdLocal)comando).Id;
        tarefas.DefinirConcluida(usuarioId, id, true);
        return "Tarefa concluída.";
    }
}

public sealed class ManipuladorExcluirTarefa(ServicoTarefas tarefas) : IManipuladorComandoLocal
{
    public IReadOnlyList<Type> TiposComando { get; } = new[] { typeof(ExcluirTarefaPorNomeLocal), typeof(ExcluirTarefaPorIdLocal) };
    public PreparacaoHandler Preparar(long usuarioId, ComandoLocal comando)
    {
        if (comando is ExcluirTarefaPorIdLocal porId)
        {
            var item = tarefas.Listar(usuarioId).FirstOrDefault(tarefa => tarefa.Id == porId.Id);
            return item is null ? new("Não encontrei essa tarefa.") : new($"Excluir a tarefa “{item.Titulo}”?", porId);
        }
        var nome = ((ExcluirTarefaPorNomeLocal)comando).Nome;
        var encontradas = tarefas.Listar(usuarioId).Where(item => InterpretadorLocal.Normalizar(item.Titulo) == InterpretadorLocal.Normalizar(nome)).ToArray();
        return encontradas.Length switch
        {
            0 => new("Não encontrei essa tarefa."),
            1 => new($"Excluir a tarefa “{encontradas[0].Titulo}”?", new ExcluirTarefaPorIdLocal(encontradas[0].Id)),
            _ => new("", Opcoes: encontradas.Select(item => new OpcaoAmbigua(item.Id, item.Titulo)).ToArray())
        };
    }
    public string Executar(long usuarioId, ComandoLocal comando)
    {
        tarefas.Excluir(usuarioId, ((ExcluirTarefaPorIdLocal)comando).Id);
        return "Tarefa excluída.";
    }
}

public sealed class ManipuladorExcluirCompromisso(ServicoAgenda agenda) : IManipuladorComandoLocal
{
    public IReadOnlyList<Type> TiposComando { get; } = new[] { typeof(ExcluirCompromissoPorNomeLocal), typeof(ExcluirCompromissoPorIdLocal) };
    public PreparacaoHandler Preparar(long usuarioId, ComandoLocal comando)
    {
        if (comando is ExcluirCompromissoPorIdLocal porId)
        {
            var item = agenda.Listar(usuarioId).FirstOrDefault(compromisso => compromisso.Id == porId.Id);
            return item is null ? new("Não encontrei esse compromisso.")
                : new($"Excluir “{item.Titulo}” em {item.Data:dd/MM/yyyy} às {item.Horario:HH:mm}?", porId);
        }
        var nome = ((ExcluirCompromissoPorNomeLocal)comando).Nome;
        var encontradas = agenda.Listar(usuarioId).Where(item => InterpretadorLocal.Normalizar(item.Titulo) == InterpretadorLocal.Normalizar(nome)).ToArray();
        return encontradas.Length switch
        {
            0 => new("Não encontrei esse compromisso."),
            1 => new($"Excluir “{encontradas[0].Titulo}” em {encontradas[0].Data:dd/MM/yyyy} às {encontradas[0].Horario:HH:mm}?", new ExcluirCompromissoPorIdLocal(encontradas[0].Id)),
            _ => new("", Opcoes: encontradas.Select(item => new OpcaoAmbigua(item.Id, $"{item.Titulo} — {item.Data:dd/MM/yyyy} às {item.Horario:HH:mm}")).ToArray())
        };
    }
    public string Executar(long usuarioId, ComandoLocal comando)
    {
        agenda.Excluir(usuarioId, ((ExcluirCompromissoPorIdLocal)comando).Id);
        return "Compromisso excluído.";
    }
}

public sealed class ManipuladorCriarCompromisso(ServicoAgenda agenda) : IManipuladorComandoLocal
{
    public IReadOnlyList<Type> TiposComando { get; } = new[] { typeof(CriarCompromissoLocal) };
    public PreparacaoHandler Preparar(long usuarioId, ComandoLocal comando)
    {
        var item = (CriarCompromissoLocal)comando;
        return new($"Criar “{item.Titulo}” em {item.Data:dd/MM/yyyy} às {item.Horario:HH:mm}?", item);
    }
    public string Executar(long usuarioId, ComandoLocal comando)
    {
        var item = (CriarCompromissoLocal)comando;
        agenda.Criar(usuarioId, item.Titulo, "", item.Data, item.Horario);
        return $"Compromisso criado: {item.Titulo}, {item.Data:dd/MM/yyyy} às {item.Horario:HH:mm}.";
    }
}

public sealed class ManipuladorCriarAnotacao(ServicoAnotacoes anotacoes) : IManipuladorComandoLocal
{
    public IReadOnlyList<Type> TiposComando { get; } = new[] { typeof(CriarAnotacaoLocal) };
    public PreparacaoHandler Preparar(long usuarioId, ComandoLocal comando)
    {
        var item = (CriarAnotacaoLocal)comando;
        return new($"Criar uma anotação com este texto?\n{item.Texto}", item);
    }
    public string Executar(long usuarioId, ComandoLocal comando)
    {
        var item = (CriarAnotacaoLocal)comando;
        anotacoes.Criar(usuarioId, "Anotação criada pelo assistente", item.Texto);
        return "Anotação criada.";
    }
}

public sealed class ManipuladorAbrirSecao : IManipuladorComandoLocal
{
    public IReadOnlyList<Type> TiposComando { get; } = new[] { typeof(AbrirSecaoLocal) };
    public PreparacaoHandler Preparar(long usuarioId, ComandoLocal comando)
    {
        var secao = ((AbrirSecaoLocal)comando).Secao;
        return new($"Abrindo {secao switch { SecaoLocal.Tarefas => "tarefas", SecaoLocal.Agenda => "a agenda", _ => "anotações" }}.", Secao: secao);
    }
    public string Executar(long usuarioId, ComandoLocal comando) => Preparar(usuarioId, comando).Texto;
}
