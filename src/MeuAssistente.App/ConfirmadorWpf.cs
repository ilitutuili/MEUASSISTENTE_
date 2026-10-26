using System.Windows;
using MeuAssistente.Application;

namespace MeuAssistente.App;

public sealed class ConfirmadorWpf : IConfirmador
{
    public bool ConfirmarExclusao(string mensagem)
        => System.Windows.MessageBox.Show(mensagem, "Confirmar exclusão", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
           == System.Windows.MessageBoxResult.Yes;
}
