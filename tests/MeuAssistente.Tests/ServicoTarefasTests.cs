using MeuAssistente.Application;
using MeuAssistente.Domain;
using Xunit;

namespace MeuAssistente.Tests;

public sealed class ServicoTarefasTests
{
    [Fact]
    public void EditarPreservaTituloEAtualizaPrioridadePeloIdDoUsuario()
    {
        var repositorio = new RepositorioTarefasFalso();
        var servico = new ServicoTarefas(repositorio);

        var editada = servico.Editar(12, 34, "  Revisão do Café  ", "alta");

        Assert.True(editada);
        Assert.Equal((12L, 34L, "Revisão do Café", "Alta"), repositorio.UltimaEdicao!.Value);
    }

    [Fact]
    public void EditarRejeitaTituloVazioEPrioridadeDesconhecida()
    {
        var servico = new ServicoTarefas(new RepositorioTarefasFalso());

        Assert.Throws<ArgumentException>(() => servico.Editar(1, 2, "  ", "Alta"));
        Assert.Throws<ArgumentException>(() => servico.Editar(1, 2, "Tarefa", "Urgente"));
    }

    private sealed class RepositorioTarefasFalso : IRepositorioTarefas
    {
        public (long UsuarioId, long TarefaId, string Titulo, string Prioridade)? UltimaEdicao { get; private set; }

        public IReadOnlyList<Tarefa> Listar(long usuarioId) => Array.Empty<Tarefa>();
        public Tarefa Criar(long usuarioId, string titulo, string prioridade) => new(1, usuarioId, titulo, prioridade, "Pendente");
        public bool Editar(long usuarioId, long tarefaId, string titulo, string prioridade)
        {
            UltimaEdicao = (usuarioId, tarefaId, titulo, prioridade);
            return true;
        }
        public void DefinirConcluida(long usuarioId, long tarefaId, bool concluida) { }
        public void Excluir(long usuarioId, long tarefaId) { }
    }
}
