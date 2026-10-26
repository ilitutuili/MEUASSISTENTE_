using MeuAssistente.Application;
using MeuAssistente.Domain;
using Xunit;

namespace MeuAssistente.Tests;

public sealed class ServicoAnotacoesTests
{
    [Fact]
    public void CriarPreservaEspacosEQuebrasDeLinhaDoTexto()
    {
        var repositorio = new RepositorioAnotacoesFalso();
        var servico = new ServicoAnotacoes(repositorio);
        const string texto = "  Reunião com João  \nPróxima linha. \n";

        var anotacao = servico.Criar(4, "  Lembrete  ", texto);

        Assert.Equal("Lembrete", anotacao.Titulo);
        Assert.Equal(texto, anotacao.Texto);
        Assert.Equal(texto, repositorio.TextoSalvo);
    }

    [Fact]
    public void EditarPreservaTextoEUsaIdDaAnotacaoEDoUsuario()
    {
        var repositorio = new RepositorioAnotacoesFalso();
        var servico = new ServicoAnotacoes(repositorio);
        const string texto = "  Conteúdo editado\ncom quebra.  ";

        var editada = servico.Editar(9, 17, "  Título editado  ", texto);

        Assert.True(editada);
        Assert.Equal((9L, 17L, "Título editado", texto), repositorio.UltimaEdicao!.Value);
    }

    private sealed class RepositorioAnotacoesFalso : IRepositorioAnotacoes
    {
        public string? TextoSalvo { get; private set; }
        public (long UsuarioId, long AnotacaoId, string Titulo, string Texto)? UltimaEdicao { get; private set; }

        public IReadOnlyList<Anotacao> Listar(long usuarioId) => Array.Empty<Anotacao>();
        public Anotacao? Obter(long usuarioId, long anotacaoId) => null;
        public Anotacao Criar(long usuarioId, string titulo, string texto)
        {
            TextoSalvo = texto;
            return new(1, usuarioId, titulo, texto);
        }
        public bool Editar(long usuarioId, long anotacaoId, string titulo, string texto)
        {
            UltimaEdicao = (usuarioId, anotacaoId, titulo, texto);
            return true;
        }
        public void Excluir(long usuarioId, long anotacaoId) { }
    }
}
