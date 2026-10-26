using System.Security.Cryptography;
using System.Text;
using MeuAssistente.Domain;

namespace MeuAssistente.Application;

public sealed record CredencialArmazenada(
    Usuario Usuario, string? SenhaLegada, byte[]? Hash, byte[]? Salt)
{
    public int Iteracoes { get; init; }
}

public interface IRepositorioUsuarios
{
    CredencialArmazenada? ObterPorEmail(string email);
    CredencialArmazenada? ObterPorId(long id);
    bool Criar(string nome, string email, byte[] hash, byte[] salt, int iteracoes);
    void AtualizarSenhaLegada(long usuarioId, byte[] hash, byte[] salt, int iteracoes);
}

public interface IRepositorioSessao
{
    long? ObterUsuarioId();
    void Salvar(long usuarioId);
    void Limpar();
}

public enum ResultadoCadastro { Criada, CamposInvalidos, EmailEmUso }
public sealed record ResultadoLogin(bool Sucesso, Usuario? Usuario, string Mensagem);

public sealed class ServicoAutenticacao(IRepositorioUsuarios usuarios, IRepositorioSessao sessoes)
{
    private const int Iteracoes = 600_000;
    private const int TamanhoSalt = 16;
    private const int TamanhoHash = 32;

    public ResultadoCadastro Cadastrar(string nome, string email, string senha, string confirmacao)
    {
        nome = nome.Trim();
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(nome) || !EmailPareceValido(email) ||
            string.IsNullOrWhiteSpace(senha) || string.IsNullOrWhiteSpace(confirmacao))
            return ResultadoCadastro.CamposInvalidos;
        var senhaHashComparacao = Sha256(senha);
        var confirmacaoHashComparacao = Sha256(confirmacao);
        var senhasIguais = CryptographicOperations.FixedTimeEquals(senhaHashComparacao, confirmacaoHashComparacao);
        CryptographicOperations.ZeroMemory(senhaHashComparacao);
        CryptographicOperations.ZeroMemory(confirmacaoHashComparacao);
        if (!senhasIguais) return ResultadoCadastro.CamposInvalidos;

        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = CriarHash(senha, salt);
        return usuarios.Criar(nome, email, hash, salt, Iteracoes)
            ? ResultadoCadastro.Criada
            : ResultadoCadastro.EmailEmUso;
    }

    public ResultadoLogin Entrar(string email, string senha, bool manterSessao)
    {
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha))
            return new(false, null, "Informe seu e-mail e sua senha.");

        var credencial = usuarios.ObterPorEmail(email);
        if (credencial is null || !ValidarSenha(credencial, senha))
            return new(false, null, "Confira o e-mail e a senha e tente novamente.");

        // Converte a senha antiga apenas depois da validação bem-sucedida.
        if (credencial.SenhaLegada is not null)
        {
            var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
            usuarios.AtualizarSenhaLegada(credencial.Usuario.Id, CriarHash(senha, salt), salt, Iteracoes);
        }

        sessoes.Limpar();
        if (manterSessao)
            sessoes.Salvar(credencial.Usuario.Id);

        return new(true, credencial.Usuario, "Login realizado.");
    }

    public Usuario? ObterSessaoSalva()
    {
        var id = sessoes.ObterUsuarioId();
        if (id is null) return null;
        var credencial = usuarios.ObterPorId(id.Value);
        // Conta legada precisa digitar a senha para que ela possa ser substituída por hash.
        if (credencial is null || credencial.SenhaLegada is not null)
        {
            sessoes.Limpar();
            return null;
        }
        return credencial.Usuario;
    }

    public void Sair() => sessoes.Limpar();

    private static bool ValidarSenha(CredencialArmazenada credencial, string senha)
    {
        if (credencial.Hash is { Length: TamanhoHash } hash &&
            credencial.Salt is { Length: TamanhoSalt } salt)
        {
            var iteracoes = credencial.Iteracoes is >= 100_000 and <= 5_000_000
                ? credencial.Iteracoes : Iteracoes;
            var calculado = CriarHash(senha, salt, iteracoes);
            try { return CryptographicOperations.FixedTimeEquals(calculado, hash); }
            finally { CryptographicOperations.ZeroMemory(calculado); }
        }

        if (credencial.SenhaLegada is null) return false;
        var tentativa = Sha256(senha);
        var legado = Sha256(credencial.SenhaLegada);
        try { return CryptographicOperations.FixedTimeEquals(tentativa, legado); }
        finally
        {
            CryptographicOperations.ZeroMemory(tentativa);
            CryptographicOperations.ZeroMemory(legado);
        }
    }

    private static byte[] CriarHash(string senha, byte[] salt, int iteracoes = Iteracoes)
        => Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, TamanhoHash);

    private static byte[] Sha256(string texto) => SHA256.HashData(Encoding.UTF8.GetBytes(texto));

    private static bool EmailPareceValido(string email)
    {
        var separador = email.LastIndexOf('@');
        return separador > 0 && separador < email.Length - 1 && email[(separador + 1)..].Contains('.');
    }
}
