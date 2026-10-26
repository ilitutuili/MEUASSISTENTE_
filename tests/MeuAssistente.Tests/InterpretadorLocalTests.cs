using MeuAssistente.Application;
using MeuAssistente.Domain;
using Xunit;

namespace MeuAssistente.Tests;

public sealed class InterpretadorLocalTests
{
    private readonly InterpretadorLocal _interpretador = new();
    private readonly DateOnly _hoje = new(2026, 10, 5);

    [Fact]
    public void ReconheceAmanhaHorarioComHETituloOriginal()
    {
        var resultado = _interpretador.Interpretar("Marcar reunião amanhã às 14h30", _hoje);

        var comando = Assert.IsType<CriarCompromissoLocal>(resultado.Comando);
        Assert.Equal("reunião", comando.Titulo);
        Assert.Equal(new DateOnly(2026, 10, 6), comando.Data);
        Assert.Equal(new TimeOnly(14, 30), comando.Horario);
    }

    [Fact]
    public void ReconheceDepoisDeAmanhaComAcentosENaoMudaCaixaDoTitulo()
    {
        var resultado = _interpretador.Interpretar("Agendar revisão Clínica amanhã depois de amanhã às 9:05", _hoje);
        var comando = Assert.IsType<CriarCompromissoLocal>(resultado.Comando);

        Assert.Equal("revisão Clínica", comando.Titulo);
        Assert.Equal(new DateOnly(2026, 10, 7), comando.Data);
        Assert.Equal(new TimeOnly(9, 5), comando.Horario);
    }

    [Fact]
    public void ExtraiComandoSemConverterTituloParaMinusculas()
    {
        var resultado = _interpretador.Interpretar("Crie uma tarefa Revisão do Café", _hoje);
        var comando = Assert.IsType<CriarTarefaLocal>(resultado.Comando);
        Assert.Equal("Revisão do Café", comando.Titulo);
    }

    [Fact]
    public void MantemPontuacaoETituloOriginalNoCompromisso()
    {
        var resultado = _interpretador.Interpretar("Marcar reunião d'Água amanhã às 14:30", _hoje);
        var comando = Assert.IsType<CriarCompromissoLocal>(resultado.Comando);
        Assert.Equal("reunião d'Água", comando.Titulo);
    }

    [Fact]
    public void SolicitaTituloQuandoFalta()
    {
        var resultado = _interpretador.Interpretar("criar compromisso amanhã às 14h30", _hoje);
        Assert.Null(resultado.Comando);
        Assert.Contains("título", resultado.Resposta);
    }

    [Fact]
    public void SolicitaNomeQuandoComandoDeTarefaEstaIncompleto()
    {
        var resultado = _interpretador.Interpretar("concluir tarefa", _hoje);
        Assert.Null(resultado.Comando);
        Assert.Contains("Qual tarefa", resultado.Resposta);
    }

    [Theory]
    [InlineData("marcar reunião 31/02/2026 às 14:30", "data não é válida")]
    [InlineData("marcar reunião amanhã às 25:70", "horário não é válido")]
    public void RejeitaDataEHorarioInvalidos(string texto, string mensagem)
    {
        var resultado = _interpretador.Interpretar(texto, _hoje);
        Assert.Null(resultado.Comando);
        Assert.Contains(mensagem, resultado.Resposta, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("hoje", 5)]
    [InlineData("amanhã", 6)]
    [InlineData("depois de amanhã", 7)]
    public void ResolveDatasRelativas(string expressao, int dia)
    {
        var resultado = _interpretador.Interpretar($"marcar reunião {expressao} às 08:00", _hoje);
        var comando = Assert.IsType<CriarCompromissoLocal>(resultado.Comando);
        Assert.Equal(new DateOnly(2026, 10, dia), comando.Data);
    }
}

public sealed class DespachanteComandosLocaisTests
{
    [Fact]
    public void PedeDesambiguacaoEConfirmaPorIdSemAlterarAntes()
    {
        var tarefasFalsas = new RepositorioTarefasFalso();
        var servico = new ServicoTarefas(tarefasFalsas);
        var despachante = new DespachanteComandosLocais(new IManipuladorComandoLocal[]
        {
            new ManipuladorExcluirTarefa(servico)
        });
        var interpretador = new InterpretadorLocal();

        var primeira = despachante.Preparar(7,
            interpretador.Interpretar("excluir tarefa Cotação", new DateOnly(2026, 10, 5)));
        Assert.Null(primeira.PropostaId);
        Assert.Contains("mais de um", primeira.Texto);
        Assert.Empty(tarefasFalsas.Excluidas);

        var escolha = despachante.Preparar(7,
            interpretador.Interpretar("2", new DateOnly(2026, 10, 5)), "2");
        Assert.NotNull(escolha.PropostaId);
        Assert.Contains("Excluir a tarefa", escolha.Texto);
        Assert.Empty(tarefasFalsas.Excluidas);

        despachante.Confirmar(7, escolha.PropostaId!.Value);
        Assert.Equal(new long[] { 2 }, tarefasFalsas.Excluidas);
    }

    private sealed class RepositorioTarefasFalso : IRepositorioTarefas
    {
        public List<long> Excluidas { get; } = new();
        public IReadOnlyList<Tarefa> Listar(long usuarioId) => new[]
        {
            new Tarefa(1, usuarioId, "Cotação", "Média", "Pendente"),
            new Tarefa(2, usuarioId, "Cotacao", "Alta", "Pendente")
        };
        public Tarefa Criar(long usuarioId, string titulo, string prioridade) => new(3, usuarioId, titulo, prioridade, "Pendente");
        public bool Editar(long usuarioId, long tarefaId, string titulo, string prioridade) => false;
        public void DefinirConcluida(long usuarioId, long tarefaId, bool concluida) { }
        public void Excluir(long usuarioId, long tarefaId) => Excluidas.Add(tarefaId);
    }
}
