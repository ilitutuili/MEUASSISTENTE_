using MeuAssistente.Application;
using MeuAssistente.Data;
using MeuAssistente.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Net.Http;

namespace MeuAssistente.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _servicos;
    private string? _caminhoLog;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigurarLog();
        try
        {
            PrepararImportacaoLegada();
            var conexoes = new SqliteConnectionFactory(DatabasePaths.ObterCaminho());
            new DatabaseMigrator(conexoes).Migrar();

            var registro = new ServiceCollection();
            registro.AddSingleton(conexoes);
        registro.AddSingleton<IRepositorioUsuarios, RepositorioUsuarios>();
        registro.AddSingleton<IRepositorioSessao, RepositorioSessao>();
        registro.AddSingleton<IRepositorioTarefas, RepositorioTarefas>();
        registro.AddSingleton<IRepositorioCompromissos, RepositorioCompromissos>();
        registro.AddSingleton<IRepositorioAnotacoes, RepositorioAnotacoes>();
        registro.AddSingleton<IRepositorioConfiguracoes, RepositorioConfiguracoes>();
        registro.AddSingleton<IProtecaoSegredo, ProtecaoSegredoDpapi>();
        registro.AddSingleton<ServicoConfiguracoes>();
        registro.AddSingleton<HttpClient>(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        registro.AddSingleton<IChatProvider, ProvedorOpenAiCompativel>();
        registro.AddSingleton<IVozDesktop, VozWindows>();
        registro.AddSingleton<ChatViewModel>();
        registro.AddSingleton<ConfiguracoesViewModel>();
        registro.AddSingleton<ServicoAutenticacao>();
        registro.AddSingleton<ServicoTarefas>();
        registro.AddSingleton<ServicoAgenda>();
        registro.AddSingleton<ServicoAnotacoes>();
        registro.AddSingleton<IConfirmador, ConfirmadorWpf>();
        registro.AddSingleton<InterpretadorLocal>();
        registro.AddTransient<IManipuladorComandoLocal, ManipuladorCriarTarefa>();
        registro.AddTransient<IManipuladorComandoLocal, ManipuladorConcluirTarefa>();
        registro.AddTransient<IManipuladorComandoLocal, ManipuladorExcluirTarefa>();
        registro.AddTransient<IManipuladorComandoLocal, ManipuladorExcluirCompromisso>();
        registro.AddTransient<IManipuladorComandoLocal, ManipuladorCriarCompromisso>();
        registro.AddTransient<IManipuladorComandoLocal, ManipuladorCriarAnotacao>();
        registro.AddTransient<IManipuladorComandoLocal, ManipuladorAbrirSecao>();
        registro.AddSingleton<DespachanteComandosLocais>();
        registro.AddSingleton<AuthViewModel>();
        registro.AddSingleton<WorkspaceViewModel>();
        registro.AddSingleton<ShellViewModel>();
        registro.AddSingleton<MainWindow>();
            _servicos = registro.BuildServiceProvider();
            _servicos.GetRequiredService<MainWindow>().Show();
        }
        catch (OperationCanceledException)
        {
            Shutdown(0);
        }
        catch (Exception erro)
        {
            Trace.TraceError("Falha na inicialização do aplicativo: {0}", erro.GetType().Name);
            System.Windows.MessageBox.Show(
                $"Não foi possível iniciar o Meu Assistente. O banco original foi mantido. Consulte o registro em {_caminhoLog ?? "%LOCALAPPDATA%\\MeuAssistente\\logs\\app.log"}.",
                "Falha ao iniciar", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _servicos?.Dispose();
        Trace.Flush();
        base.OnExit(e);
    }

    private void ConfigurarLog()
    {
        try
        {
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeuAssistente", "logs");
            Directory.CreateDirectory(pasta);
            _caminhoLog = Path.Combine(pasta, "app.log");
            Trace.Listeners.Add(new TextWriterTraceListener(_caminhoLog));
            Trace.AutoFlush = true;
        }
        catch (Exception erro) { Debug.WriteLine($"Não foi possível iniciar o log local: {erro.GetType().Name}"); }
    }

    private static void PrepararImportacaoLegada()
    {
        var caminhoDestino = DatabasePaths.ObterCaminho();
        if (File.Exists(caminhoDestino)) return;
        if (DatabasePaths.LocalizarBancoLegado() is not null)
        {
            DatabasePaths.PrepararBancoLegadoSeNecessario(caminhoDestino);
            return;
        }
        var escolha = System.Windows.MessageBox.Show(
            "Não encontrei automaticamente o banco do Meu Assistente Python. Deseja selecioná-lo para importar seus dados?",
            "Importar dados existentes", System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);
        if (escolha == System.Windows.MessageBoxResult.Cancel)
            throw new OperationCanceledException("Inicialização cancelada pelo usuário.");
        if (escolha == System.Windows.MessageBoxResult.No) return;

        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecione o banco SQLite do Meu Assistente Python",
            Filter = "Banco SQLite (*.db)|*.db|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialogo.ShowDialog() != true)
            throw new OperationCanceledException("Seleção do banco cancelada pelo usuário.");
        DatabasePaths.ImportarBancoLegado(dialogo.FileName, caminhoDestino);
    }
}
