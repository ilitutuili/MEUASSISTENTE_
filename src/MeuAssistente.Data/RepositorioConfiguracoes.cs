using MeuAssistente.Application;
using MeuAssistente.Domain;
using System.Runtime.Versioning;

namespace MeuAssistente.Data;

public sealed class RepositorioConfiguracoes(SqliteConnectionFactory conexoes) : IRepositorioConfiguracoes
{
    public PreferenciaIa? ObterIa(long usuarioId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT provedor, endpoint, modelo, pesquisa_web FROM preferencias_ia WHERE usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        using var leitor = comando.ExecuteReader();
        return leitor.Read()
            ? new PreferenciaIa(usuarioId, leitor.GetString(0), leitor.GetString(1), leitor.GetString(2), leitor.GetInt64(3) != 0)
            : null;
    }

    public void SalvarIa(PreferenciaIa preferencia)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO preferencias_ia (usuario_id, provedor, endpoint, modelo, pesquisa_web)
            VALUES ($usuario, $provedor, $endpoint, $modelo, $pesquisa)
            ON CONFLICT(usuario_id) DO UPDATE SET
                provedor = excluded.provedor, endpoint = excluded.endpoint,
                modelo = excluded.modelo, pesquisa_web = excluded.pesquisa_web;
            """;
        comando.Parameters.AddWithValue("$usuario", preferencia.UsuarioId);
        comando.Parameters.AddWithValue("$provedor", preferencia.Provedor);
        comando.Parameters.AddWithValue("$endpoint", preferencia.Endpoint);
        comando.Parameters.AddWithValue("$modelo", preferencia.Modelo);
        comando.Parameters.AddWithValue("$pesquisa", preferencia.PesquisaWebAtiva ? 1 : 0);
        comando.ExecuteNonQuery();
    }

    public byte[]? ObterChaveIaProtegida(long usuarioId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT chave_api_protegida FROM preferencias_ia WHERE usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        var resultado = comando.ExecuteScalar();
        return resultado is byte[] bytes ? bytes : null;
    }

    public void SalvarChaveIaProtegida(long usuarioId, byte[]? chave)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "UPDATE preferencias_ia SET chave_api_protegida = $chave WHERE usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$chave", (object?)chave ?? DBNull.Value);
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        comando.ExecuteNonQuery();
    }

    public PreferenciaVisual ObterVisual(long usuarioId)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT tema, cor_destaque FROM preferencias_visuais WHERE usuario_id = $usuario;";
        comando.Parameters.AddWithValue("$usuario", usuarioId);
        using var leitor = comando.ExecuteReader();
        return leitor.Read()
            ? new PreferenciaVisual(usuarioId, leitor.GetString(0), leitor.GetString(1))
            : new PreferenciaVisual(usuarioId, "Escuro", "#10A37F");
    }

    public void SalvarVisual(PreferenciaVisual preferencia)
    {
        using var conexao = conexoes.Create();
        using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO preferencias_visuais (usuario_id, tema, cor_destaque)
            VALUES ($usuario, $tema, $cor)
            ON CONFLICT(usuario_id) DO UPDATE SET tema = excluded.tema, cor_destaque = excluded.cor_destaque;
            """;
        comando.Parameters.AddWithValue("$usuario", preferencia.UsuarioId);
        comando.Parameters.AddWithValue("$tema", preferencia.Tema);
        comando.Parameters.AddWithValue("$cor", preferencia.CorDestaque);
        comando.ExecuteNonQuery();
    }
}

[SupportedOSPlatform("windows")]
public sealed class ProtecaoSegredoDpapi : IProtecaoSegredo
{
    public byte[] Proteger(string segredo)
        => System.Security.Cryptography.ProtectedData.Protect(
            System.Text.Encoding.UTF8.GetBytes(segredo), null,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);

    public string Desproteger(byte[] segredoProtegido)
        => System.Text.Encoding.UTF8.GetString(
            System.Security.Cryptography.ProtectedData.Unprotect(
                segredoProtegido, null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser));
}
