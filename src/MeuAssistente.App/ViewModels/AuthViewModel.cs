using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeuAssistente.Application;

namespace MeuAssistente.App.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    private readonly ServicoAutenticacao _autenticacao;

    [ObservableProperty] private string nome = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string senha = "";
    [ObservableProperty] private string confirmacaoSenha = "";
    [ObservableProperty] private string mensagem = "";
    [ObservableProperty] private bool manterSessao;
    [ObservableProperty] private bool modoCadastro;
    [ObservableProperty] private bool autenticado;
    [ObservableProperty] private string nomeUsuario = "";
    [ObservableProperty] private MeuAssistente.Domain.Usuario? usuarioAtual;

    public AuthViewModel(ServicoAutenticacao autenticacao)
    {
        _autenticacao = autenticacao;
        var usuarioSalvo = _autenticacao.ObterSessaoSalva();
        if (usuarioSalvo is not null)
        {
            UsuarioAtual = usuarioSalvo;
            NomeUsuario = usuarioSalvo.Nome;
            Autenticado = true;
            Mensagem = $"Olá, {usuarioSalvo.Nome}. Sua sessão foi restaurada neste computador.";
        }
    }

    [RelayCommand]
    private void MostrarCadastro() { ModoCadastro = true; Mensagem = ""; }

    [RelayCommand]
    private void MostrarLogin() { ModoCadastro = false; Mensagem = ""; }

    [RelayCommand]
    private async Task EntrarAsync()
    {
        var emailInformado = Email;
        var senhaInformada = Senha;
        Senha = "";
        var resultado = await Task.Run(() => _autenticacao.Entrar(emailInformado, senhaInformada, ManterSessao));
        Mensagem = resultado.Mensagem;
        if (resultado.Sucesso && resultado.Usuario is not null)
        {
            UsuarioAtual = resultado.Usuario;
            NomeUsuario = resultado.Usuario.Nome;
            Autenticado = true;
        }
    }

    [RelayCommand]
    private async Task CadastrarAsync()
    {
        var nomeInformado = Nome;
        var emailInformado = Email;
        var senhaInformada = Senha;
        var confirmacaoInformada = ConfirmacaoSenha;
        Senha = "";
        ConfirmacaoSenha = "";

        var resultado = await Task.Run(() => _autenticacao.Cadastrar(
            nomeInformado, emailInformado, senhaInformada, confirmacaoInformada));
        Mensagem = resultado switch
        {
            ResultadoCadastro.Criada => "Conta criada. Agora entre para continuar.",
            ResultadoCadastro.EmailEmUso => "Já existe uma conta com esse e-mail.",
            _ => "Confira os campos, o e-mail e se as senhas coincidem."
        };
        if (resultado == ResultadoCadastro.Criada)
            ModoCadastro = false;
    }

    [RelayCommand]
    private void Sair() => SairDaConta();

    public void SairDaConta()
    {
        _autenticacao.Sair();
        Autenticado = false;
        UsuarioAtual = null;
        NomeUsuario = "";
        Mensagem = "Sessão encerrada.";
    }
}
