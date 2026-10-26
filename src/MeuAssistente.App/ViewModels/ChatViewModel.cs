using System.Collections.ObjectModel;
using System.Text.Json;
using System.Net.Http;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeuAssistente.Application;
using MeuAssistente.Domain;

namespace MeuAssistente.App.ViewModels;

public partial class ChatViewModel(IChatProvider provedor, ServicoConfiguracoes configuracoes, IVozDesktop voz) : ObservableObject
{
    private long _usuarioId;
    private CancellationTokenSource? _cancelamento;
    private static readonly string InstrucaoSistema = """
        Você é o assistente do Meu Assistente. Responda em português do Brasil.
        As mensagens e o histórico deste chat são enviados ao endpoint de IA configurado pelo usuário.
        Dados locais de tarefas, agenda e anotações não são enviados automaticamente.
        Para uma ação local, use uma única ferramenta disponível com dados explícitos do usuário. Nunca afirme que a ação foi realizada.
        A aplicação mostrará a ação proposta e só a executará após confirmação explícita.
        Nunca invente datas ou horários. Para compromissos, use a data AAAA-MM-DD e hora HH:mm.
        Para pesquisa atual, use a ferramenta de pesquisa na web quando ela estiver habilitada.
        """;

    public ObservableCollection<MensagemChatLocal> Mensagens { get; } = new();

    [ObservableProperty] private string pergunta = "";
    [ObservableProperty] private string erro = "";
    [ObservableProperty] private bool ocupado;
    [ObservableProperty] private string provedorAtual = "";
    [ObservableProperty] private bool transcrevendo;
    [ObservableProperty] private bool lerRespostasEmVoz;

    public event Action<ChamadaFerramentaIa>? FerramentaSolicitada;

    public void CarregarUsuario(long usuarioId)
    {
        Cancelar();
        _usuarioId = usuarioId;
        Mensagens.Clear();
        Erro = "";
        var preferencia = configuracoes.ObterIa(usuarioId);
        ProvedorAtual = preferencia?.Provedor ?? "Nenhum provedor configurado";
    }

    [RelayCommand]
    private async Task EnviarAsync()
    {
        var texto = Pergunta.Trim();
        if (texto.Length == 0 || Ocupado || _usuarioId <= 0) return;
        var preferencia = configuracoes.ObterIa(_usuarioId);
        if (preferencia is null || string.IsNullOrWhiteSpace(preferencia.Endpoint) || string.IsNullOrWhiteSpace(preferencia.Modelo))
        { Erro = "Configure provedor, endpoint e modelo na aba Configurações."; return; }

        string chave;
        try { chave = configuracoes.ObterChaveIa(_usuarioId); }
        catch
        { Erro = "O Windows não conseguiu abrir a chave protegida. Informe uma chave nova nas configurações."; return; }
        if (preferencia.Provedor != "Ollama" && string.IsNullOrWhiteSpace(chave))
        { Erro = "Informe a chave de API do provedor nas configurações."; return; }

        Pergunta = "";
        Erro = "";
        Mensagens.Add(new("user", texto));
        Mensagens.Add(new("assistant", ""));
        var indiceResposta = Mensagens.Count - 1;
        var conteudoResposta = new System.Text.StringBuilder();
        Ocupado = true;
        _cancelamento = new CancellationTokenSource();
        var progresso = new Progress<string>(trecho =>
        {
            conteudoResposta.Append(trecho);
            Mensagens[indiceResposta] = new("assistant", conteudoResposta.ToString());
        });

        try
        {
            var historico = Mensagens.Take(Math.Max(0, Mensagens.Count - 2)).TakeLast(20)
                .Select(item => new MensagemIa(item.Papel, item.Texto)).ToList();
            historico.Insert(0, new MensagemIa("system", InstrucaoSistema));
            historico.Add(new MensagemIa("user", texto));
            var resposta = await provedor.ResponderAsync(
                preferencia.Endpoint, preferencia.Modelo, chave, historico,
                preferencia.PesquisaWebAtiva, progresso, _cancelamento.Token);
            if (resposta.Ferramentas.Count > 0)
            {
                if (conteudoResposta.Length == 0)
                {
                    conteudoResposta.Append("Preparei uma ação local. Confira a proposta e confirme antes de salvar.");
                    Mensagens[indiceResposta] = new("assistant", conteudoResposta.ToString());
                }
                foreach (var ferramenta in resposta.Ferramentas)
                    FerramentaSolicitada?.Invoke(ferramenta);
            }
            if (conteudoResposta.Length == 0 && resposta.Conteudo.Length > 0)
            {
                conteudoResposta.Append(resposta.Conteudo);
                Mensagens[indiceResposta] = new("assistant", resposta.Conteudo);
            }
            if (LerRespostasEmVoz && conteudoResposta.Length > 0)
                await voz.FalarAsync(conteudoResposta.ToString(), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            if (conteudoResposta.Length == 0) conteudoResposta.Append("Geração interrompida.");
            else conteudoResposta.Append("\n\nGeração interrompida.");
            Mensagens[indiceResposta] = new("assistant", conteudoResposta.ToString());
        }
        catch (TimeoutException ex) { Erro = ex.Message; }
        catch (HttpRequestException ex) { Erro = ex.Message; }
        catch (InvalidDataException ex) { Erro = ex.Message; }
        catch (Exception ex)
        {
            Erro = "Não foi possível concluir a conversa. Confira endpoint, modelo e conexão.";
            System.Diagnostics.Trace.TraceError("Falha no chat: {0}", ex.GetType().Name);
        }
        finally
        {
            Ocupado = false;
            _cancelamento.Dispose();
            _cancelamento = null;
        }
    }

    [RelayCommand]
    private void Parar() => Cancelar();

    [RelayCommand]
    private async Task CapturarVozAsync()
    {
        if (Transcrevendo) return;
        Transcrevendo = true;
        Erro = "O reconhecimento é local; o texto só será enviado quando você pressionar Enviar.";
        try
        {
            var transcricao = await voz.ReconhecerAsync(CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(transcricao)) Pergunta = transcricao;
            Erro = "";
        }
        catch (Exception ex)
        {
            Erro = ex is InvalidOperationException ? ex.Message : "Não foi possível reconhecer a fala. Verifique o microfone e o pacote de idioma pt-BR do Windows.";
        }
        finally { Transcrevendo = false; }
    }

    [RelayCommand]
    private async Task LerUltimaRespostaAsync()
    {
        var texto = Mensagens.LastOrDefault(item => item.Papel == "assistant")?.Texto;
        if (string.IsNullOrWhiteSpace(texto)) return;
        try { await voz.FalarAsync(texto, CancellationToken.None); }
        catch (Exception ex)
        {
            Erro = "Não foi possível reproduzir a voz instalada no Windows.";
            System.Diagnostics.Trace.TraceError("Falha na síntese de voz: {0}", ex.GetType().Name);
        }
    }

    private void Cancelar()
    {
        try { _cancelamento?.Cancel(); }
        catch (ObjectDisposedException) { }
    }
}
