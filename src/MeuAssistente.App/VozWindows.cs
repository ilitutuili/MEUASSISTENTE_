using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using MeuAssistente.Application;

namespace MeuAssistente.App;

public sealed class VozWindows : IVozDesktop
{
    public Task<string> ReconhecerAsync(CancellationToken cancellationToken)
        => Task.Run(() =>
        {
            var reconhecedorDisponivel = SpeechRecognitionEngine.InstalledRecognizers()
                .FirstOrDefault(item => item.Culture.Name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase));
            if (reconhecedorDisponivel is null)
                throw new InvalidOperationException("Instale o reconhecimento de fala em português (Brasil) nas configurações de idioma do Windows.");
            using var reconhecedor = new SpeechRecognitionEngine(reconhecedorDisponivel);
            reconhecedor.LoadGrammar(new DictationGrammar());
            reconhecedor.SetInputToDefaultAudioDevice();
            cancellationToken.ThrowIfCancellationRequested();
            var resultado = reconhecedor.Recognize(TimeSpan.FromSeconds(20));
            cancellationToken.ThrowIfCancellationRequested();
            return resultado?.Text ?? "";
        }, cancellationToken);

    public Task FalarAsync(string texto, CancellationToken cancellationToken)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sintetizador = new SpeechSynthesizer();
            var vozPortugues = sintetizador.GetInstalledVoices()
                .FirstOrDefault(item => item.Enabled && item.VoiceInfo.Culture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase));
            if (vozPortugues is not null) sintetizador.SelectVoice(vozPortugues.VoiceInfo.Name);
            sintetizador.Rate = 0;
            using var registroCancelamento = cancellationToken.Register(sintetizador.SpeakAsyncCancelAll);
            sintetizador.Speak(texto);
        }, cancellationToken);
}
