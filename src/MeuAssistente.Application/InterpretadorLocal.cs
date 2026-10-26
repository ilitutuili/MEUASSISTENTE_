using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MeuAssistente.Application;

public sealed record MensagemLocal(string Papel, string Conteudo);

public abstract record ComandoLocal;
public sealed record CriarTarefaLocal(string Titulo) : ComandoLocal;
public sealed record ConcluirTarefaPorNomeLocal(string Nome) : ComandoLocal;
public sealed record ExcluirTarefaPorNomeLocal(string Nome) : ComandoLocal;
public sealed record ExcluirCompromissoPorNomeLocal(string Nome) : ComandoLocal;
public sealed record CriarCompromissoLocal(string Titulo, DateOnly Data, TimeOnly Horario) : ComandoLocal;
public sealed record CriarAnotacaoLocal(string Texto) : ComandoLocal;
public sealed record AbrirSecaoLocal(SecaoLocal Secao) : ComandoLocal;

public enum SecaoLocal { Tarefas, Agenda, Anotacoes }

public sealed record ResultadoInterpretacaoLocal(ComandoLocal? Comando, string? Resposta)
{
    public static ResultadoInterpretacaoLocal Acao(ComandoLocal comando) => new(comando, null);
    public static ResultadoInterpretacaoLocal Texto(string resposta) => new(null, resposta);
}

/// <summary>Interpreta comandos sem banco, relógio global, interface ou rede.</summary>
public sealed class InterpretadorLocal
{
    private static readonly Regex DataNumerica = new(@"\b(?<dia>\d{1,2})/(?<mes>\d{1,2})/(?<ano>\d{4})\b", RegexOptions.Compiled);
    private static readonly Regex HorarioNumerico = new(@"\b(?:[aà]s?\s*)?(?<hora>\d{1,2})(?::(?<minuto>\d{2})|h(?:oras?)?(?:\s*(?<minutoH>\d{1,2}))?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> PalavrasDeAgenda = new(StringComparer.Ordinal)
    {
        "hoje", "amanha", "depois", "na", "agenda", "compromisso", "marcar", "marque",
        "agendar", "agende", "criar", "crie", "cria", "colocar", "coloque", "coloca",
        "adicionar", "adicione", "adiciona", "por", "favor", "quero", "minha", "meu",
        "para", "as", "a", "dia", "em", "um", "uma", "o", "sim", "ok", "isso", "pode", "claro"
    };

    public ResultadoInterpretacaoLocal Interpretar(
        string? entrada, DateOnly hoje, IReadOnlyList<MensagemLocal>? historico = null)
    {
        var texto = entrada?.Trim() ?? "";
        var normalizado = Normalizar(texto);
        if (normalizado.Length == 0)
            return ResultadoInterpretacaoLocal.Texto("Diga o que você quer organizar.");

        var abrir = InterpretarAbertura(normalizado);
        if (abrir is not null) return ResultadoInterpretacaoLocal.Acao(abrir);

        var agenda = InterpretarAgenda(texto, hoje, historico);
        if (agenda is not null) return agenda;

        var tarefa = MatchTexto(texto,
            @"^(?:por favor,?\s*)?(?:(?:eu\s+)?quero\s+)?(?:crie|criar|cria|adicione|adicionar|adiciona|coloque|colocar|coloca)\s+(?:uma\s+)?tarefa\s*(.*)$");
        if (tarefa is not null)
        {
            var titulo = LimparBordas(tarefa.Groups[1].Value);
            return titulo.Length == 0
                ? ResultadoInterpretacaoLocal.Texto("Qual tarefa você quer criar?")
                : ResultadoInterpretacaoLocal.Acao(new CriarTarefaLocal(titulo));
        }

        var anotacao = MatchTexto(texto,
            @"^(?:por favor,?\s*)?(?:(?:eu\s+)?quero\s+)?(?:crie|criar|cria|faça|faca|salve|salvar|salva|anote|anotar|anota)\s+(?:uma\s+)?anota[cç][aã]o\s*(.*)$");
        if (anotacao is not null)
        {
            var conteudo = LimparBordas(anotacao.Groups[1].Value);
            return conteudo.Length == 0
                ? ResultadoInterpretacaoLocal.Texto("O que você quer guardar na anotação?")
                : ResultadoInterpretacaoLocal.Acao(new CriarAnotacaoLocal(conteudo));
        }

        var anota = MatchTexto(texto, @"^(?:anote|anota|salve|salva)\s*(.*)$");
        if (anota is not null)
        {
            var conteudo = LimparBordas(anota.Groups[1].Value);
            return conteudo.Length == 0
                ? ResultadoInterpretacaoLocal.Texto("O que você quer guardar na anotação?")
                : ResultadoInterpretacaoLocal.Acao(new CriarAnotacaoLocal(conteudo));
        }

        var concluir = MatchTexto(texto, @"^(?:conclua|concluir|finalize|finalizar)\s+(?:a\s+)?tarefa\s*(.*)$");
        if (concluir is not null)
        {
            var nome = LimparBordas(concluir.Groups[1].Value);
            return nome.Length == 0 ? ResultadoInterpretacaoLocal.Texto("Qual tarefa você quer concluir?")
                : ResultadoInterpretacaoLocal.Acao(new ConcluirTarefaPorNomeLocal(nome));
        }

        var excluirTarefa = MatchTexto(texto, @"^(?:exclua|excluir|apague|apagar)\s+(?:a\s+)?tarefa\s*(.*)$");
        if (excluirTarefa is not null)
        {
            var nome = LimparBordas(excluirTarefa.Groups[1].Value);
            return nome.Length == 0 ? ResultadoInterpretacaoLocal.Texto("Qual tarefa você quer excluir?")
                : ResultadoInterpretacaoLocal.Acao(new ExcluirTarefaPorNomeLocal(nome));
        }

        var excluirCompromisso = MatchTexto(texto, @"^(?:exclua|excluir|apague|apagar)\s+(?:o\s+)?compromisso\s*(.*)$");
        if (excluirCompromisso is not null)
        {
            var nome = LimparBordas(excluirCompromisso.Groups[1].Value);
            return nome.Length == 0 ? ResultadoInterpretacaoLocal.Texto("Qual é o título do compromisso?")
                : ResultadoInterpretacaoLocal.Acao(new ExcluirCompromissoPorNomeLocal(nome));
        }

        if (new[] { "ola", "oi", "bom dia", "boa tarde", "boa noite" }.Contains(normalizado, StringComparer.Ordinal))
            return ResultadoInterpretacaoLocal.Texto("Olá! Como posso ajudar?");

        return ResultadoInterpretacaoLocal.Texto(
            "Sou o assistente local. Posso criar, concluir ou excluir tarefas; criar anotações; e abrir ou criar compromissos na agenda.");
    }

    public static string Normalizar(string texto)
    {
        var decomposicao = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(decomposicao.Length);
        foreach (var caractere in decomposicao)
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                resultado.Append(char.ToLowerInvariant(caractere));
        return resultado.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    private static ComandoLocal? InterpretarAbertura(string texto)
    {
        if (new[] { "abrir agenda", "mostra a agenda", "mostrar agenda", "ver agenda", "minha agenda" }.Any(texto.Contains))
            return new AbrirSecaoLocal(SecaoLocal.Agenda);
        if (new[] { "abrir tarefa", "abrir tarefas", "minhas tarefas", "mostrar tarefas", "ver tarefas" }.Any(texto.Contains))
            return new AbrirSecaoLocal(SecaoLocal.Tarefas);
        if (new[] { "abrir anotacao", "abrir anotacoes", "minhas anotacoes", "mostrar anotacoes", "ver anotacoes" }.Any(texto.Contains))
            return new AbrirSecaoLocal(SecaoLocal.Anotacoes);
        return null;
    }

    private static ResultadoInterpretacaoLocal? InterpretarAgenda(string texto, DateOnly hoje, IReadOnlyList<MensagemLocal>? historico)
    {
        var falasRecentes = (historico ?? Array.Empty<MensagemLocal>())
            .Where(m => string.Equals(m.Papel, "user", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.Conteudo))
            .TakeLast(6).Select(m => m.Conteudo).ToList();
        var indicePedido = falasRecentes.FindLastIndex(fala => TemIntencaoAgenda(Normalizar(fala)));
        if (indicePedido >= 0) falasRecentes = falasRecentes.Skip(indicePedido).ToList();
        var textos = falasRecentes.Append(texto).ToList();
        var normalizado = Normalizar(string.Join(" ", textos));
        if (!TemIntencaoAgenda(normalizado)) return null;

        DateOnly? data = null;
        var dataTexto = DataNumerica.Match(string.Join(" ", textos));
        if (dataTexto.Success)
        {
            var dia = int.Parse(dataTexto.Groups["dia"].Value, CultureInfo.InvariantCulture);
            var mes = int.Parse(dataTexto.Groups["mes"].Value, CultureInfo.InvariantCulture);
            var ano = int.Parse(dataTexto.Groups["ano"].Value, CultureInfo.InvariantCulture);
            try { data = new DateOnly(ano, mes, dia); }
            catch (ArgumentOutOfRangeException)
            {
                return ResultadoInterpretacaoLocal.Texto("Essa data não é válida. Informe a data no formato dia/mês/ano.");
            }
        }
        else if (Regex.IsMatch(normalizado, @"\bdepois de amanha\b")) data = hoje.AddDays(2);
        else if (Regex.IsMatch(normalizado, @"\bamanha\b")) data = hoje.AddDays(1);
        else if (Regex.IsMatch(normalizado, @"\bhoje\b")) data = hoje;

        TimeOnly? horario = null;
        var horaTexto = HorarioNumerico.Match(normalizado);
        if (horaTexto.Success)
        {
            var hora = int.Parse(horaTexto.Groups["hora"].Value, CultureInfo.InvariantCulture);
            var minutoGrupo = horaTexto.Groups["minuto"].Success ? horaTexto.Groups["minuto"] : horaTexto.Groups["minutoH"];
            var minuto = minutoGrupo.Success ? int.Parse(minutoGrupo.Value, CultureInfo.InvariantCulture) : 0;
            if (hora > 23 || minuto > 59)
                return ResultadoInterpretacaoLocal.Texto("Esse horário não é válido. Informe, por exemplo, 14:30.");
            horario = new TimeOnly(hora, minuto);
        }

        var titulo = ExtrairTituloAgenda(textos);
        var faltam = new List<string>();
        if (string.IsNullOrWhiteSpace(titulo)) faltam.Add("o título");
        if (data is null) faltam.Add("a data");
        if (horario is null) faltam.Add("o horário");
        if (faltam.Count > 0)
        {
            if (faltam.Count == 1) return ResultadoInterpretacaoLocal.Texto($"Qual é {faltam[0]}?");
            return ResultadoInterpretacaoLocal.Texto($"Para criar o compromisso, informe {string.Join(", ", faltam.Take(faltam.Count - 1))} e {faltam[^1]}.");
        }

        return ResultadoInterpretacaoLocal.Acao(new CriarCompromissoLocal(titulo!, data!.Value, horario!.Value));
    }

    private static bool TemIntencaoAgenda(string texto)
        => new[] { "agenda", "compromisso", "agendar", "marcar reuniao", "marcar reuniao" }.Any(texto.Contains)
           && new[] { "criar", "marcar", "agendar", "colocar", "adicionar", "anota", "salva", "agenda", "poe", "por na" }.Any(texto.Contains);

    private static string ExtrairTituloAgenda(IEnumerable<string> textos)
    {
        foreach (var origem in textos.Reverse())
        {
            var limpo = DataNumerica.Replace(origem, " ");
            limpo = HorarioNumerico.Replace(limpo, " ");
            limpo = Regex.Replace(limpo, @"\bdepois\s+de\s+amanhã\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            limpo = Regex.Replace(limpo, @"[\p{L}\p{N}_-]+", match =>
                PalavrasDeAgenda.Contains(Normalizar(match.Value)) ? " " : match.Value);
            var titulo = Regex.Replace(limpo, @"\s+", " ")
                .Trim(' ', '-', '.', ',', '!', '?', ':', ';', '"', '\'');
            if (titulo.Length > 1) return titulo;
        }
        return "";
    }

    private static Match? MatchTexto(string entrada, string padrao)
    {
        var match = Regex.Match(entrada, padrao, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match : null;
    }

    private static string LimparBordas(string texto) => texto.Trim().Trim(' ', '.', ',', '!', '?', ';', ':');
}
