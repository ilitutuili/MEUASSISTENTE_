using MeuAssistente.App.ViewModels;
using MeuAssistente.App.Views;
using MeuAssistente.Domain;
using System.ComponentModel;

namespace MeuAssistente.App;

public partial class MainWindow : System.Windows.Window
{
    private readonly BandejaWindows _bandeja;
    private readonly MonitorAgendaWindows _monitorAgenda;
    private bool _fechamentoSolicitado;

    public MainWindow(ShellViewModel viewModel, MeuAssistente.Application.ServicoAgenda agenda)
    {
        InitializeComponent();
        DataContext = viewModel;
        var postits = new PostitManager(this);
        viewModel.AreaTrabalho.FixarSolicitado += postits.Fixar;
        viewModel.AreaTrabalho.PostitsEncerrarSolicitado += postits.EncerrarTodos;
        _bandeja = new BandejaWindows(this, viewModel.Autenticacao.SairDaConta, FecharAplicativo);
        _monitorAgenda = new MonitorAgendaWindows(agenda, _bandeja);
        Closing += AoFechar;
        viewModel.Autenticacao.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AuthViewModel.Autenticado)) AtualizarMonitor(viewModel.Autenticacao);
        };
        viewModel.AreaTrabalho.Configuracoes.VisualSalva += AparenciaWindows.Aplicar;
        AparenciaWindows.Aplicar(new PreferenciaVisual(
            viewModel.Autenticacao.UsuarioAtual?.Id ?? 0,
            viewModel.AreaTrabalho.Configuracoes.Tema,
            viewModel.AreaTrabalho.Configuracoes.CorDestaque));
        AtualizarMonitor(viewModel.Autenticacao);
    }


    private void AtualizarMonitor(AuthViewModel autenticacao)
        => _monitorAgenda.Ativar(autenticacao.Autenticado ? autenticacao.UsuarioAtual?.Id ?? 0 : 0);

    private void AoFechar(object? sender, CancelEventArgs evento)
    {
        if (!_fechamentoSolicitado)
        {
            evento.Cancel = true;
            Hide();
            return;
        }
        _monitorAgenda.Dispose();
        _bandeja.Dispose();
    }

    private void FecharAplicativo()
    {
        _fechamentoSolicitado = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }
}
