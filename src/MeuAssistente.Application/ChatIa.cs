using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MeuAssistente.Application;

public sealed record MensagemIa(string Papel, string Conteudo);
public sealed record ChamadaFerramentaIa(string Id, string Nome, string Argumentos);
public sealed record RespostaIa(string Conteudo, IReadOnlyList<ChamadaFerramentaIa> Ferramentas);

public interface IChatProvider
{
    Task<RespostaIa> ResponderAsync(
        string endpoint, string modelo, string chave,
        IReadOnlyList<MensagemIa> mensagens, bool pesquisaWeb,
        IProgress<string> progresso, CancellationToken cancellationToken);
}

public sealed class ProvedorOpenAiCompativel(HttpClient http) : IChatProvider
{
    public async Task<RespostaIa> ResponderAsync(
        string endpoint, string modelo, string chave,
        IReadOnlyList<MensagemIa> mensagens, bool pesquisaWeb,
        IProgress<string> progresso, CancellationToken cancellationToken)
    {
        var baseUrl = endpoint.Trim().TrimEnd('/');
        var url = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? baseUrl : baseUrl + "/chat/completions";
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, url);
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (!string.IsNullOrWhiteSpace(chave))
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave);

        var ferramentas = CriarFerramentas().ToList<object>();
        if (pesquisaWeb)
            ferramentas.Add(new { type = "openrouter:web_search" });

        var corpo = JsonSerializer.Serialize(new
        {
            model = modelo,
            stream = true,
            parallel_tool_calls = false,
            messages = mensagens.Select(item => new { role = item.Papel, content = item.Conteudo }),
            tools = ferramentas,
            tool_choice = "auto"
        });
        requisicao.Content = new StringContent(corpo, System.Text.Encoding.UTF8, "application/json");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var resposta = await http.SendAsync(requisicao, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!resposta.IsSuccessStatusCode)
            throw CriarErroHttp(resposta.StatusCode);

        await using var fluxo = await resposta.Content.ReadAsStreamAsync(timeout.Token);
        using var leitor = new StreamReader(fluxo);
        var conteudo = new System.Text.StringBuilder();
        var chamadas = new SortedDictionary<int, FerramentaParcial>();
        while (await leitor.ReadLineAsync(timeout.Token) is { } linha)
        {
            if (!linha.StartsWith("data:", StringComparison.Ordinal)) continue;
            var dados = linha[5..].Trim();
            if (dados.Length == 0 || dados == "[DONE]") continue;
            using var documento = JsonDocument.Parse(dados);
            if (!documento.RootElement.TryGetProperty("choices", out var escolhas) || escolhas.GetArrayLength() == 0)
                continue;
            var delta = escolhas[0].TryGetProperty("delta", out var valorDelta) ? valorDelta : default;
            if (delta.ValueKind != JsonValueKind.Object) continue;

            if (delta.TryGetProperty("content", out var parte) && parte.ValueKind == JsonValueKind.String)
            {
                var texto = parte.GetString() ?? "";
                if (texto.Length > 0)
                {
                    conteudo.Append(texto);
                    progresso.Report(texto);
                }
            }

            if (delta.TryGetProperty("tool_calls", out var tools) && tools.ValueKind == JsonValueKind.Array)
            {
                foreach (var tool in tools.EnumerateArray())
                {
                    if (!tool.TryGetProperty("index", out var indexJson) || !indexJson.TryGetInt32(out var index)) continue;
                    if (!chamadas.TryGetValue(index, out var chamada))
                        chamadas[index] = chamada = new FerramentaParcial();
                    if (tool.TryGetProperty("id", out var id)) chamada.Id = id.GetString() ?? chamada.Id;
                    if (!tool.TryGetProperty("function", out var funcao)) continue;
                    if (funcao.TryGetProperty("name", out var nome)) chamada.Nome += nome.GetString();
                    if (funcao.TryGetProperty("arguments", out var argumentos)) chamada.Argumentos.Append(argumentos.GetString());
                }
            }
        }

        var resultadoFerramentas = chamadas.Values
            .Where(item => item.Nome.Length > 0)
            .Select(item => new ChamadaFerramentaIa(item.Id, item.Nome, item.Argumentos.ToString()))
            .ToArray();
        if (conteudo.Length == 0 && resultadoFerramentas.Length == 0)
            throw new InvalidDataException("O provedor respondeu sem texto ou ação reconhecível.");
        return new(conteudo.ToString(), resultadoFerramentas);
    }

    private static IEnumerable<object> CriarFerramentas()
    {
        static object Definir(string nome, string descricao, object propriedades, string[] obrigatorios)
            => new { type = "function", function = new {
                name = nome, description = descricao,
                parameters = new { type = "object", properties = propriedades, required = obrigatorios, additionalProperties = false }
            } };

        yield return Definir("criar_tarefa", "Propor a criação de uma tarefa local.",
            new { titulo = new { type = "string", description = "Título original da tarefa" } }, ["titulo"]);
        yield return Definir("concluir_tarefa", "Propor conclusão de uma tarefa pelo título; o app pedirá desambiguação se necessário.",
            new { titulo = new { type = "string" } }, ["titulo"]);
        yield return Definir("excluir_tarefa", "Propor exclusão de uma tarefa pelo título; o app pedirá desambiguação se necessário.",
            new { titulo = new { type = "string" } }, ["titulo"]);
        yield return Definir("criar_compromisso", "Propor compromisso. Só use horário e data fornecidos pelo usuário.",
            new { titulo = new { type = "string" }, data = new { type = "string", description = "AAAA-MM-DD" }, horario = new { type = "string", description = "HH:mm" } }, ["titulo", "data", "horario"]);
        yield return Definir("excluir_compromisso", "Propor exclusão de compromisso pelo título; o app pedirá desambiguação se necessário.",
            new { titulo = new { type = "string" } }, ["titulo"]);
        yield return Definir("criar_anotacao", "Propor a criação de anotação com o conteúdo original do usuário.",
            new { texto = new { type = "string" } }, ["texto"]);
    }

    private static Exception CriarErroHttp(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new HttpRequestException("A chave de API foi recusada pelo provedor.", null, status),
        HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => new TimeoutException("O provedor demorou demais para responder."),
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway => new HttpRequestException("O serviço de IA está temporariamente indisponível.", null, status),
        _ => new HttpRequestException($"O provedor de IA respondeu com HTTP {(int)status}.", null, status)
    };

    private sealed class FerramentaParcial
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";
        public System.Text.StringBuilder Argumentos { get; } = new();
    }
}
