namespace MeuAssistente.Data;

public static class DatabasePaths
{
    public static string ObterCaminho()
    {
        var pastaLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(pastaLocal, "MeuAssistente", "assistente.db");
    }

    public static void PrepararBancoLegadoSeNecessario(string caminhoDestino)
    {
        if (File.Exists(caminhoDestino))
            return;

        var pastaDestino = Path.GetDirectoryName(caminhoDestino)
            ?? throw new InvalidOperationException("Não foi possível determinar a pasta do banco.");
        Directory.CreateDirectory(pastaDestino);

        // O app Python usava o diretório atual. Copiamos o arquivo sem alterar o original.
        var legado = LocalizarBancoLegado();
        if (legado is not null && !string.Equals(legado, caminhoDestino, StringComparison.OrdinalIgnoreCase))
            File.Copy(legado, caminhoDestino, overwrite: false);
    }

    public static string? LocalizarBancoLegado()
        => new[]
        {
            Path.Combine(Environment.CurrentDirectory, "assistente.db"),
            Path.Combine(AppContext.BaseDirectory, "assistente.db")
        }.FirstOrDefault(File.Exists);

    public static void ImportarBancoLegado(string origem, string caminhoDestino)
    {
        if (!File.Exists(origem)) throw new FileNotFoundException("O banco selecionado não foi encontrado.", origem);
        if (File.Exists(caminhoDestino)) throw new IOException("Já existe um banco neste perfil do aplicativo.");
        var pasta = Path.GetDirectoryName(caminhoDestino)
            ?? throw new InvalidOperationException("Não foi possível determinar a pasta do banco.");
        Directory.CreateDirectory(pasta);
        File.Copy(origem, caminhoDestino, overwrite: false);
    }
}
