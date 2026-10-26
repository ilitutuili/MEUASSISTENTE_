using MeuAssistente.Domain;

namespace MeuAssistente.Application;

public interface IRepositorioConfiguracoes
{
    PreferenciaIa? ObterIa(long usuarioId);
    void SalvarIa(PreferenciaIa preferencia);
    byte[]? ObterChaveIaProtegida(long usuarioId);
    void SalvarChaveIaProtegida(long usuarioId, byte[]? chave);
    PreferenciaVisual ObterVisual(long usuarioId);
    void SalvarVisual(PreferenciaVisual preferencia);
}

public interface IProtecaoSegredo
{
    byte[] Proteger(string segredo);
    string Desproteger(byte[] segredoProtegido);
}

public interface IVozDesktop
{
    Task<string> ReconhecerAsync(CancellationToken cancellationToken);
    Task FalarAsync(string texto, CancellationToken cancellationToken);
}

public sealed class ServicoConfiguracoes(IRepositorioConfiguracoes repositorio, IProtecaoSegredo protecao)
{
    public PreferenciaIa? ObterIa(long usuarioId) => repositorio.ObterIa(usuarioId);

    public string ObterChaveIa(long usuarioId)
    {
        var protegida = repositorio.ObterChaveIaProtegida(usuarioId);
        return protegida is null ? "" : protecao.Desproteger(protegida);
    }

    public bool TemChaveIa(long usuarioId) => repositorio.ObterChaveIaProtegida(usuarioId) is not null;

    public void SalvarIa(PreferenciaIa preferencia, string? novaChave, bool removerChave)
    {
        repositorio.SalvarIa(preferencia);
        if (removerChave)
        {
            repositorio.SalvarChaveIaProtegida(preferencia.UsuarioId, null);
            return;
        }
        if (!string.IsNullOrWhiteSpace(novaChave))
            repositorio.SalvarChaveIaProtegida(preferencia.UsuarioId, protecao.Proteger(novaChave));
    }

    public PreferenciaVisual ObterVisual(long usuarioId) => repositorio.ObterVisual(usuarioId);
    public void SalvarVisual(PreferenciaVisual preferencia) => repositorio.SalvarVisual(preferencia);
}
