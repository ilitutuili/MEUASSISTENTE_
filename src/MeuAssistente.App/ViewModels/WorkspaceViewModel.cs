using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeuAssistente.Application;
using MeuAssistente.Domain;

namespace MeuAssistente.App.ViewModels;

public partial class WorkspaceViewModel : ObservableObject
{
    private readonly ServicoTarefas _tarefas;
    private readonly ServicoAgenda _agenda;
    private readonly ServicoAnotacoes _anotacoes;
    private readonly IConfirmador _confirmador;
    private readonly InterpretadorLocal _interpretador;
    private readonly DespachanteComandosLocais _despachante;
    private long _usuarioId;
    private long? _tarefaIdEmEdicao;
    private long? _compromissoIdEmEdicao;
    private long? _anotacaoIdEmEdicao;

    public ObservableCollection<Tarefa> Tarefas { get; } = new();
    public ObservableCollection<Compromisso> Compromissos { get; } = new();
    public ObservableCollection<Anotacao> Anotacoes { get; } = new();
    public ObservableCollection<MensagemChatLocal> MensagensAssistente { get; } = new();
    public ChatViewModel Chat { get; }
    public ConfiguracoesViewModel Configuracoes { get; }
    public IReadOnlyList<string> Prioridades { get; } = new[] { "Baixa", "Média", "Alta" };

    [ObservableProperty] private string nomeUsuario = "";
    [ObservableProperty] private string tituloTarefa = "";
    [ObservableProperty] private string prioridadeTarefa = "Média";
    [ObservableProperty] private Tarefa? tarefaSelecionada;
    [ObservableProperty] private bool editandoTarefa;
    [ObservableProperty] private string tituloCompromisso = "";
    [ObservableProperty] private string descricaoCompromisso = "";
    [ObservableProperty] private string dataCompromisso = "";
    [ObservableProperty] private string horarioCompromisso = "";
    [ObservableProperty] private bool editandoCompromisso;
    [ObservableProperty] private string filtroDataInicial = "";
    [ObservableProperty] private string filtroDataFinal = "";
    [ObservableProperty] private Compromisso? compromissoSelecionado;
    [ObservableProperty] private string tituloAnotacao = "";
    [ObservableProperty] private string textoAnotacao = "";
    [ObservableProperty] private Anotacao? anotacaoSelecionada;
    [ObservableProperty] private bool editandoAnotacao;
    [ObservableProperty] private string erro = "";
    [ObservableProperty] private string perguntaAssistente = "";
    [ObservableProperty] private string textoProposta = "";
    [ObservableProperty] private Guid? propostaPendente;
    [ObservableProperty] private bool temProposta;
    [ObservableProperty] private int indiceAbaSelecionada;

    public event Action<Anotacao>? FixarSolicitado;
    public event Action? PostitsEncerrarSolicitado;
    public event Action? SairSolicitado;

    public WorkspaceViewModel(ServicoTarefas tarefas, ServicoAgenda agenda, ServicoAnotacoes anotacoes,
        IConfirmador confirmador, InterpretadorLocal interpretador, DespachanteComandosLocais despachante,
        ChatViewModel chat, ConfiguracoesViewModel configuracoes)
    {
        _tarefas = tarefas;
        _agenda = agenda;
        _anotacoes = anotacoes;
        _confirmador = confirmador;
        _interpretador = interpretador;
        _despachante = despachante;
        Chat = chat;
        Configuracoes = configuracoes;
        Chat.FerramentaSolicitada += PrepararFerramentaIa;
    }

    public void CarregarUsuario(Usuario? usuario)
    {
        var usuarioAnterior = _usuarioId;
        if (_usuarioId != 0 && _usuarioId != (usuario?.Id ?? 0))
            PostitsEncerrarSolicitado?.Invoke();
        _despachante.LimparUsuario(usuarioAnterior);
        _usuarioId = usuario?.Id ?? 0;
        Chat.CarregarUsuario(_usuarioId);
        Configuracoes.CarregarUsuario(_usuarioId);
        CancelarEdicaoTarefa();
        CancelarEdicaoCompromisso();
        CancelarEdicaoAnotacao();
        NomeUsuario = usuario?.Nome ?? "";
        Erro = "";
        Tarefas.Clear();
        Compromissos.Clear();
        Anotacoes.Clear();
        MensagensAssistente.Clear();
        PropostaPendente = null;
        TemProposta = false;
        if (usuario is null) return;
        AtualizarTarefas();
        AtualizarCompromissos();
        AtualizarAnotacoes();
    }

    [RelayCommand]
    private void SalvarTarefa()
    {
        try
        {
            if (EditandoTarefa)
            {
                if (_tarefaIdEmEdicao is not long tarefaId || !_tarefas.Editar(
                        _usuarioId, tarefaId, TituloTarefa, PrioridadeTarefa))
                {
                    Erro = "Não encontrei a tarefa selecionada para editar.";
                    return;
                }
                TarefaSelecionada = Tarefas.FirstOrDefault(item => item.Id == tarefaId);
                AtualizarTarefas();
                CancelarEdicaoTarefa();
            }
            else
            {
                Tarefas.Insert(0, _tarefas.Criar(_usuarioId, TituloTarefa, PrioridadeTarefa));
                TituloTarefa = "";
                Erro = "";
            }
        }
        catch (ArgumentException erro) { Erro = erro.Message; }
    }

    [RelayCommand]
    private void PrepararEdicaoTarefa()
    {
        if (TarefaSelecionada is null) { Erro = "Selecione uma tarefa."; return; }
        TituloTarefa = TarefaSelecionada.Titulo;
        PrioridadeTarefa = TarefaSelecionada.Prioridade;
        _tarefaIdEmEdicao = TarefaSelecionada.Id;
        EditandoTarefa = true;
        Erro = "";
    }

    [RelayCommand]
    private void CancelarEdicaoTarefa()
    {
        EditandoTarefa = false;
        _tarefaIdEmEdicao = null;
        TituloTarefa = "";
        PrioridadeTarefa = "Média";
        Erro = "";
    }

    [RelayCommand]
    private void AlternarConclusaoTarefa()
    {
        if (TarefaSelecionada is null) { Erro = "Selecione uma tarefa."; return; }
        var concluida = !string.Equals(TarefaSelecionada.Status, "Concluída", StringComparison.OrdinalIgnoreCase);
        _tarefas.DefinirConcluida(_usuarioId, TarefaSelecionada.Id, concluida);
        AtualizarTarefas();
        Erro = "";
    }

    [RelayCommand]
    private void ExcluirTarefa()
    {
        if (TarefaSelecionada is null) { Erro = "Selecione uma tarefa."; return; }
        if (!_confirmador.ConfirmarExclusao($"Deseja excluir a tarefa?\n\n{TarefaSelecionada.Titulo}")) return;
        _tarefas.Excluir(_usuarioId, TarefaSelecionada.Id);
        AtualizarTarefas();
        Erro = "";
    }

    [RelayCommand]
    private void SalvarCompromisso()
    {
        if (!DateOnly.TryParseExact(DataCompromisso.Trim(), "dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var data))
        { Erro = "Informe uma data válida no formato DD/MM/AAAA."; return; }
        if (!TimeOnly.TryParseExact(HorarioCompromisso.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var horario))
        { Erro = "Informe um horário válido, por exemplo 14:30."; return; }
        try
        {
            if (EditandoCompromisso)
            {
                if (_compromissoIdEmEdicao is not long compromissoId || !_agenda.Editar(
                        _usuarioId, compromissoId, TituloCompromisso,
                        DescricaoCompromisso, data, horario))
                {
                    Erro = "Não encontrei o compromisso selecionado para editar.";
                    return;
                }
                CompromissoSelecionado = Compromissos.FirstOrDefault(item => item.Id == compromissoId);
                AtualizarCompromissos();
                CancelarEdicaoCompromisso();
            }
            else
            {
                _agenda.Criar(_usuarioId, TituloCompromisso, DescricaoCompromisso, data, horario);
                CancelarEdicaoCompromisso();
                AtualizarCompromissos();
            }
        }
        catch (ArgumentException erro) { Erro = erro.Message; }
    }

    [RelayCommand]
    private void PrepararEdicaoCompromisso()
    {
        if (CompromissoSelecionado is null) { Erro = "Selecione um compromisso."; return; }
        TituloCompromisso = CompromissoSelecionado.Titulo;
        DescricaoCompromisso = CompromissoSelecionado.Descricao ?? "";
        DataCompromisso = CompromissoSelecionado.Data.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        HorarioCompromisso = CompromissoSelecionado.Horario.ToString("HH:mm", CultureInfo.InvariantCulture);
        _compromissoIdEmEdicao = CompromissoSelecionado.Id;
        EditandoCompromisso = true;
        Erro = "";
    }

    [RelayCommand]
    private void CancelarEdicaoCompromisso()
    {
        EditandoCompromisso = false;
        _compromissoIdEmEdicao = null;
        TituloCompromisso = "";
        DescricaoCompromisso = "";
        DataCompromisso = "";
        HorarioCompromisso = "";
        Erro = "";
    }

    [RelayCommand]
    private void FiltrarCompromissos()
    {
        if (!TentarDataFiltro(FiltroDataInicial, out var de) || !TentarDataFiltro(FiltroDataFinal, out var ate))
        { Erro = "Use DD/MM/AAAA nos filtros de data ou deixe o campo vazio."; return; }
        if (de.HasValue && ate.HasValue && de.Value > ate.Value)
        { Erro = "A data inicial deve ser anterior à data final."; return; }
        AtualizarCompromissos(de, ate);
        Erro = "";
    }

    [RelayCommand]
    private void LimparFiltroCompromissos()
    {
        FiltroDataInicial = "";
        FiltroDataFinal = "";
        AtualizarCompromissos();
        Erro = "";
    }

    [RelayCommand]
    private void ExcluirCompromisso()
    {
        if (CompromissoSelecionado is null) { Erro = "Selecione um compromisso."; return; }
        if (!_confirmador.ConfirmarExclusao($"Deseja excluir este compromisso?\n\n{CompromissoSelecionado.Titulo}")) return;
        _agenda.Excluir(_usuarioId, CompromissoSelecionado.Id);
        AtualizarCompromissos();
        Erro = "";
    }

    [RelayCommand]
    private void SalvarAnotacao()
    {
        try
        {
            if (EditandoAnotacao)
            {
                if (_anotacaoIdEmEdicao is not long anotacaoId || !_anotacoes.Editar(
                        _usuarioId, anotacaoId, TituloAnotacao, TextoAnotacao))
                {
                    Erro = "Não encontrei a anotação selecionada para editar.";
                    return;
                }
                AnotacaoSelecionada = Anotacoes.FirstOrDefault(item => item.Id == anotacaoId);
                AtualizarAnotacoes();
                CancelarEdicaoAnotacao();
            }
            else
            {
                _anotacoes.Criar(_usuarioId, TituloAnotacao, TextoAnotacao);
                CancelarEdicaoAnotacao();
                AtualizarAnotacoes();
            }
        }
        catch (ArgumentException erro) { Erro = erro.Message; }
    }

    [RelayCommand]
    private void PrepararEdicaoAnotacao()
    {
        if (AnotacaoSelecionada is null) { Erro = "Selecione uma anotação."; return; }
        TituloAnotacao = AnotacaoSelecionada.Titulo;
        TextoAnotacao = AnotacaoSelecionada.Texto;
        _anotacaoIdEmEdicao = AnotacaoSelecionada.Id;
        EditandoAnotacao = true;
        Erro = "";
    }

    [RelayCommand]
    private void CancelarEdicaoAnotacao()
    {
        EditandoAnotacao = false;
        _anotacaoIdEmEdicao = null;
        TituloAnotacao = "";
        TextoAnotacao = "";
        Erro = "";
    }

    [RelayCommand]
    private void ExcluirAnotacao()
    {
        if (AnotacaoSelecionada is null) { Erro = "Selecione uma anotação."; return; }
        if (!_confirmador.ConfirmarExclusao($"Deseja excluir esta anotação?\n\n{AnotacaoSelecionada.Titulo}")) return;
        _anotacoes.Excluir(_usuarioId, AnotacaoSelecionada.Id);
        AtualizarAnotacoes();
        Erro = "";
    }

    [RelayCommand]
    private void FixarAnotacao()
    {
        if (AnotacaoSelecionada is null) { Erro = "Selecione uma anotação."; return; }
        var anotacao = _anotacoes.Obter(_usuarioId, AnotacaoSelecionada.Id);
        if (anotacao is not null) FixarSolicitado?.Invoke(anotacao);
    }

    [RelayCommand]
    private void SairDaConta() => SairSolicitado?.Invoke();

    [RelayCommand]
    private void EnviarComandoLocal()
    {
        var texto = PerguntaAssistente.Trim();
        if (texto.Length == 0) return;
        if (PropostaPendente is Guid anterior)
        {
            _despachante.Cancelar(_usuarioId, anterior);
            MensagensAssistente.Add(new("assistant", "Ação anterior cancelada para você enviar um novo pedido."));
        }
        PropostaPendente = null;
        TemProposta = false;

        MensagensAssistente.Add(new("user", texto));
        var historico = MensagensAssistente
            .Select(item => new MensagemLocal(item.Papel, item.Texto)).ToArray();
        var interpretacao = _interpretador.Interpretar(
            texto, DateOnly.FromDateTime(DateTime.Now), historico[..^1]);
        var resultado = _despachante.Preparar(_usuarioId, interpretacao, texto);
        MensagensAssistente.Add(new("assistant", resultado.Texto));
        PerguntaAssistente = "";

        if (resultado.PropostaId is Guid proposta)
        {
            PropostaPendente = proposta;
            TextoProposta = resultado.Texto;
            TemProposta = true;
        }
        if (resultado.Secao is SecaoLocal secao)
            IndiceAbaSelecionada = secao switch { SecaoLocal.Tarefas => 2, SecaoLocal.Agenda => 3, _ => 4 };
        Erro = "";
    }

    [RelayCommand]
    private void ConfirmarComandoLocal()
    {
        if (PropostaPendente is not Guid proposta) return;
        var resultado = _despachante.Confirmar(_usuarioId, proposta);
        MensagensAssistente.Add(new("assistant", resultado.Texto));
        PropostaPendente = null;
        TemProposta = false;
        AtualizarTarefas();
        AtualizarCompromissos();
        AtualizarAnotacoes();
    }

    private void PrepararFerramentaIa(ChamadaFerramentaIa ferramenta)
    {
        if (_usuarioId <= 0) return;
        try
        {
            using var json = JsonDocument.Parse(ferramenta.Argumentos);
            var dados = json.RootElement;
            string Texto(string nome) => dados.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String
                ? valor.GetString() ?? "" : "";
            ComandoLocal? comando = ferramenta.Nome switch
            {
                "criar_tarefa" when !string.IsNullOrWhiteSpace(Texto("titulo"))
                    => new CriarTarefaLocal(Texto("titulo")),
                "concluir_tarefa" when !string.IsNullOrWhiteSpace(Texto("titulo"))
                    => new ConcluirTarefaPorNomeLocal(Texto("titulo")),
                "excluir_tarefa" when !string.IsNullOrWhiteSpace(Texto("titulo"))
                    => new ExcluirTarefaPorNomeLocal(Texto("titulo")),
                "excluir_compromisso" when !string.IsNullOrWhiteSpace(Texto("titulo"))
                    => new ExcluirCompromissoPorNomeLocal(Texto("titulo")),
                "criar_anotacao" when !string.IsNullOrWhiteSpace(Texto("texto"))
                    => new CriarAnotacaoLocal(Texto("texto")),
                "criar_compromisso" => CriarCompromissoDaFerramenta(Texto("titulo"), Texto("data"), Texto("horario")),
                _ => null
            };

            if (comando is null)
            {
                MensagensAssistente.Add(new("assistant", "A IA pediu uma ação incompleta ou inválida. Nenhum dado foi alterado."));
                return;
            }

            var resultado = _despachante.Preparar(_usuarioId, ResultadoInterpretacaoLocal.Acao(comando));
            MensagensAssistente.Add(new("assistant", resultado.Texto));
            if (resultado.PropostaId is Guid proposta)
            {
                PropostaPendente = proposta;
                TextoProposta = resultado.Texto;
                TemProposta = true;
            }
            IndiceAbaSelecionada = 0;
        }
        catch (JsonException)
        {
            MensagensAssistente.Add(new("assistant", "A IA enviou dados de ação inválidos. Nada foi executado."));
        }
    }

    private static ComandoLocal? CriarCompromissoDaFerramenta(string titulo, string dataTexto, string horarioTexto)
    {
        if (string.IsNullOrWhiteSpace(titulo) ||
            !DateOnly.TryParseExact(dataTexto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ||
            !TimeOnly.TryParseExact(horarioTexto, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var horario))
            return null;
        return new CriarCompromissoLocal(titulo, data, horario);
    }

    [RelayCommand]
    private void CancelarComandoLocal()
    {
        if (PropostaPendente is Guid proposta) _despachante.Cancelar(_usuarioId, proposta);
        MensagensAssistente.Add(new("assistant", "Ação cancelada. Nenhuma alteração foi feita."));
        PropostaPendente = null;
        TemProposta = false;
    }

    private void AtualizarTarefas()
    {
        var selecionadaId = TarefaSelecionada?.Id;
        Tarefas.Clear();
        foreach (var item in _tarefas.Listar(_usuarioId)) Tarefas.Add(item);
        TarefaSelecionada = Tarefas.FirstOrDefault(item => item.Id == selecionadaId);
    }

    private void AtualizarCompromissos(DateOnly? de = null, DateOnly? ate = null)
    {
        var selecionadoId = CompromissoSelecionado?.Id;
        Compromissos.Clear();
        foreach (var item in _agenda.Listar(_usuarioId, de, ate)) Compromissos.Add(item);
        CompromissoSelecionado = Compromissos.FirstOrDefault(item => item.Id == selecionadoId);
    }

    private void AtualizarAnotacoes()
    {
        var selecionadaId = AnotacaoSelecionada?.Id;
        Anotacoes.Clear();
        foreach (var item in _anotacoes.Listar(_usuarioId)) Anotacoes.Add(item);
        AnotacaoSelecionada = Anotacoes.FirstOrDefault(item => item.Id == selecionadaId);
    }

    private static bool TentarDataFiltro(string texto, out DateOnly? data)
    {
        if (string.IsNullOrWhiteSpace(texto)) { data = null; return true; }
        if (DateOnly.TryParseExact(texto.Trim(), "dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var valor))
        { data = valor; return true; }
        data = null;
        return false;
    }
}

public sealed record MensagemChatLocal(string Papel, string Texto)
{
    public string Rotulo => Papel == "user" ? "Você" : "Assistente local";
}
