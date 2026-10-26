using System.Windows;
using System.Windows.Controls;
using MeuAssistente.App.ViewModels;

namespace MeuAssistente.App.Views;

public partial class AuthView : System.Windows.Controls.UserControl
{
    public AuthView() => InitializeComponent();

    private void AuthView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AuthViewModel anterior) anterior.PropertyChanged -= ViewModel_PropertyChanged;
        if (e.NewValue is AuthViewModel atual) atual.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not AuthViewModel viewModel) return;
        if (e.PropertyName == nameof(AuthViewModel.Senha) && viewModel.Senha.Length == 0) SenhaInput.Clear();
        if (e.PropertyName == nameof(AuthViewModel.ConfirmacaoSenha) && viewModel.ConfirmacaoSenha.Length == 0) ConfirmacaoInput.Clear();
    }

    private void SenhaInput_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AuthViewModel viewModel) viewModel.Senha = SenhaInput.Password;
    }

    private void ConfirmacaoInput_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AuthViewModel viewModel) viewModel.ConfirmacaoSenha = ConfirmacaoInput.Password;
    }
}
