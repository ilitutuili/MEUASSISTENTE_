using System.Windows;
using MeuAssistente.Domain;

namespace MeuAssistente.App.Views;

public sealed class PostitManager(Window proprietaria)
{
    private readonly Dictionary<long, PostitWindow> _janelas = new();

    public void Fixar(Anotacao anotacao)
    {
        if (_janelas.TryGetValue(anotacao.Id, out var aberta))
        {
            aberta.Activate();
            return;
        }

        var janela = new PostitWindow(anotacao) { Owner = proprietaria };
        janela.Closed += (_, _) => _janelas.Remove(anotacao.Id);
        _janelas[anotacao.Id] = janela;
        janela.Show();
    }

    public void EncerrarTodos()
    {
        foreach (var janela in _janelas.Values.ToArray()) janela.Close();
        _janelas.Clear();
    }
}
