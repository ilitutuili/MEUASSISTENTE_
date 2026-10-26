using System.Windows;
using System.Windows.Threading;
using MeuAssistente.Application;
using MeuAssistente.Domain;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MeuAssistente.App;

public sealed class BandejaWindows : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icone;
    private readonly MainWindow _janela;
    private readonly Action _sairDaConta;
    private readonly Action _fecharAplicativo;

    public BandejaWindows(MainWindow janela, Action sairDaConta, Action fecharAplicativo)
    {
        _janela = janela;
        _sairDaConta = sairDaConta;
        _fecharAplicativo = fecharAplicativo;
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Abrir Meu Assistente", null, (_, _) => Restaurar());
        menu.Items.Add("Sair da conta", null, (_, _) => _janela.Dispatcher.Invoke(_sairDaConta));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Fechar aplicativo", null, (_, _) => _janela.Dispatcher.Invoke(_fecharAplicativo));
        _icone = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Meu Assistente",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icone.DoubleClick += (_, _) => Restaurar();
    }

    public void Notificar(string titulo, string mensagem)
        => _icone.ShowBalloonTip(5000, titulo, mensagem, System.Windows.Forms.ToolTipIcon.Info);

    private void Restaurar() => _janela.Dispatcher.Invoke(() =>
    {
        _janela.Show();
        _janela.WindowState = WindowState.Normal;
        _janela.Activate();
    });

    public void Dispose()
    {
        _icone.Visible = false;
        _icone.Dispose();
    }
}

public sealed class MonitorAgendaWindows : IDisposable
{
    private readonly ServicoAgenda agenda;
    private readonly BandejaWindows bandeja;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly HashSet<long> _notificados = new();
    private long _usuarioId;

    public MonitorAgendaWindows(ServicoAgenda agenda, BandejaWindows bandeja)
    {
        this.agenda = agenda;
        this.bandeja = bandeja;
        _timer.Tick += Verificar;
    }

    public void Ativar(long usuarioId)
    {
        _usuarioId = usuarioId;
        _notificados.Clear();
        _timer.Stop();
        if (usuarioId > 0)
        {
            _timer.Start();
            Verificar(null, EventArgs.Empty);
        }
    }

    private void Verificar(object? sender, EventArgs e)
    {
        if (_usuarioId <= 0) return;
        try
        {
            var agora = DateTime.Now;
            var limite = agora.AddMinutes(15);
            foreach (var compromisso in agenda.Listar(_usuarioId))
            {
                if (_notificados.Contains(compromisso.Id)) continue;
                var horario = compromisso.Data.ToDateTime(compromisso.Horario);
                if (horario >= agora && horario <= limite)
                {
                    _notificados.Add(compromisso.Id);
                    bandeja.Notificar("Compromisso em até 15 minutos", $"{compromisso.Titulo} · {compromisso.Horario:HH:mm}");
                }
            }
        }
        catch (Exception erro)
        {
            System.Diagnostics.Trace.TraceError("Falha ao verificar lembretes: {0}", erro.GetType().Name);
        }
    }

    public void Dispose() => _timer.Stop();
}

public static class AparenciaWindows
{
    public static void Aplicar(PreferenciaVisual visual)
    {
        var tema = visual.Tema == "Claro" ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(tema);
        if (System.Windows.Media.ColorConverter.ConvertFromString(visual.CorDestaque) is System.Windows.Media.Color cor)
            ApplicationAccentColorManager.Apply(cor, tema);
    }
}
