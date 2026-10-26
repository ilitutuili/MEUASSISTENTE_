using MeuAssistente.Domain;

namespace MeuAssistente.App.Views;

public partial class PostitWindow : System.Windows.Window
{
    public long AnotacaoId { get; }

    public PostitWindow(Anotacao anotacao)
    {
        AnotacaoId = anotacao.Id;
        InitializeComponent();
        DataContext = anotacao;
    }
}
