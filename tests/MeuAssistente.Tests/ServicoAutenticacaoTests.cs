using MeuAssistente.Application;
using MeuAssistente.Domain;
using Xunit;

namespace MeuAssistente.Tests;

public sealed class ServicoAutenticacaoTests
{
    [Fact]
    public void CadastroGuardaHashESenhaCorretaAutentica()
    {
        var repositorio = new RepositorioUsuariosFalso();
        var sessoes = new RepositorioSessaoFalso();
        var servico = new ServicoAutenticacao(repositorio, sessoes);

        Assert.Equal(ResultadoCadastro.Criada, servico.Cadastrar("Ana", "ana@example.com", "segredo", "segredo"));
        Assert.NotNull(repositorio.Credencial!.Hash);
        Assert.NotEqual("segredo", repositorio.Credencial.SenhaLegada);
        Assert.True(servico.Entrar("ana@example.com", "segredo", manterSessao: true).Sucesso);
        Assert.Equal((long?)1, sessoes.UsuarioId);
    }

    [Fact]
    public void PrimeiroLoginLegadoSubstituiSenhaPorHash()
    {
        var repositorio = new RepositorioUsuariosFalso();
        repositorio.AdicionarLegado("legada@example.com", "senha-antiga");
        var servico = new ServicoAutenticacao(repositorio, new RepositorioSessaoFalso());

        Assert.True(servico.Entrar("legada@example.com", "senha-antiga", manterSessao: false).Sucesso);
        Assert.Null(repositorio.Credencial!.SenhaLegada);
        Assert.NotNull(repositorio.Credencial.Hash);
        Assert.False(servico.Entrar("legada@example.com", "errada", manterSessao: false).Sucesso);
    }

    [Fact]
    public void CadastroRejeitaEmailDuplicadoESenhaDiferente()
    {
        var repositorio = new RepositorioUsuariosFalso();
        var servico = new ServicoAutenticacao(repositorio, new RepositorioSessaoFalso());

        Assert.Equal(ResultadoCadastro.Criada, servico.Cadastrar("Ana", "ana@example.com", "uma", "uma"));
        Assert.Equal(ResultadoCadastro.EmailEmUso, servico.Cadastrar("Outra", "ANA@example.com", "duas", "duas"));
        Assert.Equal(ResultadoCadastro.CamposInvalidos, servico.Cadastrar("Bia", "bia@example.com", "uma", "outra"));
    }

    [Fact]
    public void SessaoSalvaDeUsuarioLegadoExigeLoginComSenha()
    {
        var repositorio = new RepositorioUsuariosFalso();
        repositorio.AdicionarLegado("legada@example.com", "senha-antiga");
        var sessoes = new RepositorioSessaoFalso();
        sessoes.Salvar(1);
        var servico = new ServicoAutenticacao(repositorio, sessoes);

        Assert.Null(servico.ObterSessaoSalva());
        Assert.Null(sessoes.UsuarioId);
    }

    private sealed class RepositorioUsuariosFalso : IRepositorioUsuarios
    {
        public CredencialArmazenada? Credencial { get; private set; }
        private long _proximoId = 1;

        public CredencialArmazenada? ObterPorEmail(string email)
            => Credencial is not null && string.Equals(Credencial.Usuario.Email, email, StringComparison.OrdinalIgnoreCase)
                ? Credencial : null;

        public CredencialArmazenada? ObterPorId(long id)
            => Credencial?.Usuario.Id == id ? Credencial : null;

        public bool Criar(string nome, string email, byte[] hash, byte[] salt, int iteracoes)
        {
            if (ObterPorEmail(email) is not null) return false;
            Credencial = new(new Usuario(_proximoId++, nome, email), null, hash, salt) { Iteracoes = iteracoes };
            return true;
        }

        public void AtualizarSenhaLegada(long usuarioId, byte[] hash, byte[] salt, int iteracoes)
        {
            if (Credencial?.Usuario.Id == usuarioId)
                Credencial = Credencial with { SenhaLegada = null, Hash = hash, Salt = salt, Iteracoes = iteracoes };
        }

        public void AdicionarLegado(string email, string senha)
            => Credencial = new(new Usuario(_proximoId++, "Usuário legado", email), senha, null, null);
    }

    private sealed class RepositorioSessaoFalso : IRepositorioSessao
    {
        public long? UsuarioId { get; private set; }
        public long? ObterUsuarioId() => UsuarioId;
        public void Salvar(long usuarioId) => UsuarioId = usuarioId;
        public void Limpar() => UsuarioId = null;
    }
}
