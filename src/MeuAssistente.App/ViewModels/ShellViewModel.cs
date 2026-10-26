using CommunityToolkit.Mvvm.ComponentModel;
using MeuAssistente.Domain;

namespace MeuAssistente.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    public AuthViewModel Autenticacao { get; }
    public WorkspaceViewModel AreaTrabalho { get; }

    [ObservableProperty] private bool autenticado;

    public ShellViewModel(AuthViewModel autenticacao, WorkspaceViewModel areaTrabalho)
    {
        Autenticacao = autenticacao;
        AreaTrabalho = areaTrabalho;
        Autenticacao.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AuthViewModel.Autenticado))
            {
                Autenticado = Autenticacao.Autenticado;
                AreaTrabalho.CarregarUsuario(Autenticacao.UsuarioAtual);
            }
        };
        AreaTrabalho.SairSolicitado += Autenticacao.SairDaConta;
        Autenticado = Autenticacao.Autenticado;
        if (Autenticado) AreaTrabalho.CarregarUsuario(Autenticacao.UsuarioAtual);
    }
}
