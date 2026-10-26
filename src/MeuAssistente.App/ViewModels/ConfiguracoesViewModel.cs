using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeuAssistente.Application;
using MeuAssistente.Domain;

namespace MeuAssistente.App.ViewModels;

public partial class ConfiguracoesViewModel(ServicoConfiguracoes configuracoes) : ObservableObject
{
    private long _usuarioId;

    public ObservableCollection<string> Provedores { get; } = ["OpenAI", "OpenRouter", "Gemini", "Groq", "Ollama", "Outro compatível"];
    public IReadOnlyList<string> Temas { get; } = ["Claro", "Escuro"];

    [ObservableProperty] private string provedor = "OpenAI";
    [ObservableProperty] private string endpoint = "https://api.openai.com/v1";
    [ObservableProperty] private string modelo = "";
    [ObservableProperty] private string chaveApiNova = "";
    [ObservableProperty] private bool temChaveSalva;
    [ObservableProperty] private bool removerChave;
    [ObservableProperty] private bool pesquisaWeb;
    [ObservableProperty] private string tema = "Escuro";
    [ObservableProperty] private string corDestaque = "#10A37F";
    [ObservableProperty] private string mensagem = "";

    public event Action<PreferenciaVisual>? VisualSalva;
    public event Action? PreferenciaIaSalva;

    public void CarregarUsuario(long usuarioId)
    {
        _usuarioId = usuarioId;
        var ia = configuracoes.ObterIa(usuarioId);
        Provedor = ia?.Provedor ?? "OpenAI";
        Endpoint = ia?.Endpoint ?? "https://api.openai.com/v1";
        Modelo = ia?.Modelo ?? "";
        PesquisaWeb = ia?.PesquisaWebAtiva ?? false;
        TemChaveSalva = configuracoes.TemChaveIa(usuarioId);
        ChaveApiNova = "";
        RemoverChave = false;

        var visual = configuracoes.ObterVisual(usuarioId);
        Tema = visual.Tema is "Claro" or "Escuro" ? visual.Tema : "Escuro";
        CorDestaque = Regex.IsMatch(visual.CorDestaque, "^#[0-9A-Fa-f]{6}$") ? visual.CorDestaque : "#10A37F";
        Mensagem = "";
        VisualSalva?.Invoke(new PreferenciaVisual(usuarioId, Tema, CorDestaque));
    }

    [RelayCommand]
    private void SalvarIa()
    {
        if (_usuarioId <= 0) return;
        if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        { Mensagem = "Informe um endpoint HTTP ou HTTPS válido."; return; }
        if (string.IsNullOrWhiteSpace(Modelo))
        { Mensagem = "Informe o identificador do modelo fornecido pelo serviço."; return; }
        if (PesquisaWeb && !string.Equals(Provedor, "OpenRouter", StringComparison.OrdinalIgnoreCase))
        { Mensagem = "A pesquisa na web está disponível apenas para OpenRouter."; return; }

        try
        {
            configuracoes.SalvarIa(
                new PreferenciaIa(_usuarioId, Provedor, Endpoint.Trim(), Modelo.Trim(), PesquisaWeb),
                ChaveApiNova, RemoverChave);
            TemChaveSalva = !RemoverChave && (TemChaveSalva || !string.IsNullOrWhiteSpace(ChaveApiNova));
            ChaveApiNova = "";
            RemoverChave = false;
            Mensagem = "Configuração da IA salva. A chave fica protegida pelo Windows e não é exibida.";
            PreferenciaIaSalva?.Invoke();
        }
        catch (Exception erro)
        {
            Mensagem = "Não foi possível salvar a configuração da IA. Verifique o acesso ao banco e tente novamente.";
            System.Diagnostics.Trace.TraceError("Falha ao salvar preferência de IA: {0}", erro.GetType().Name);
        }
    }

    [RelayCommand]
    private void SalvarVisual()
    {
        if (_usuarioId <= 0) return;
        if (Tema is not ("Claro" or "Escuro") || !Regex.IsMatch(CorDestaque, "^#[0-9A-Fa-f]{6}$"))
        { Mensagem = "Escolha um tema e uma cor hexadecimal válida, como #10A37F."; return; }
        var visual = new PreferenciaVisual(_usuarioId, Tema, CorDestaque.ToUpperInvariant());
        configuracoes.SalvarVisual(visual);
        VisualSalva?.Invoke(visual);
        Mensagem = "Aparência salva.";
    }
}
