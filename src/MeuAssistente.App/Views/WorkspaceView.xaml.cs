using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using MeuAssistente.App.ViewModels;

namespace MeuAssistente.App.Views;

public partial class WorkspaceView : System.Windows.Controls.UserControl
{
    public WorkspaceView() => InitializeComponent();

    private void ChaveApiPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel viewModel && sender is PasswordBox campo)
            viewModel.Configuracoes.ChaveApiNova = campo.Password;
    }

    private void LimparCampoChaveApi(object sender, System.Windows.RoutedEventArgs e)
    {
        EntradaChaveApi.Clear();
    }
}

public sealed class DateOnlyParaExibicao : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is DateOnly data ? data.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")) : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public sealed class TimeOnlyParaExibicao : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TimeOnly horario ? horario.ToString("HH:mm", CultureInfo.InvariantCulture) : "";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
