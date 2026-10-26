using MeuAssistente.Application;
using MeuAssistente.Domain;
using Xunit;

namespace MeuAssistente.Tests;

public sealed class ServicoAgendaTests
{
    [Fact]
    public void EditarAtualizaCamposDoCompromissoNoEscopoDoUsuario()
    {
        var repositorio = new RepositorioCompromissosFalso();
        var servico = new ServicoAgenda(repositorio);
        var data = new DateOnly(2026, 10, 12);
        var horario = new TimeOnly(14, 30);

        var editado = servico.Editar(8, 21, "  Revisão mensal  ", "  Sala 2  ", data, horario);

        Assert.True(editado);
        Assert.Equal((8L, 21L, "Revisão mensal", "Sala 2", data, horario), repositorio.UltimaEdicao!.Value);
    }

    [Fact]
    public void EditarRejeitaTituloVazio()
    {
        var servico = new ServicoAgenda(new RepositorioCompromissosFalso());

        Assert.Throws<ArgumentException>(() => servico.Editar(
            1, 2, "  ", "descrição", new DateOnly(2026, 10, 12), new TimeOnly(14, 30)));
    }

    private sealed class RepositorioCompromissosFalso : IRepositorioCompromissos
    {
        public (long UsuarioId, long CompromissoId, string Titulo, string? Descricao, DateOnly Data, TimeOnly Horario)? UltimaEdicao { get; private set; }

        public IReadOnlyList<Compromisso> Listar(long usuarioId, DateOnly? de = null, DateOnly? ate = null)
            => Array.Empty<Compromisso>();
        public Compromisso Criar(long usuarioId, string titulo, string? descricao, DateOnly data, TimeOnly horario)
            => new(1, usuarioId, titulo, descricao, data, horario);
        public bool Editar(long usuarioId, long compromissoId, string titulo, string? descricao, DateOnly data, TimeOnly horario)
        {
            UltimaEdicao = (usuarioId, compromissoId, titulo, descricao, data, horario);
            return true;
        }
        public void Excluir(long usuarioId, long compromissoId) { }
    }
}
