using InvestProMax.SharedUI.Auth;

namespace InvestProMax.Maui;

public sealed class MauiBiometricAuthService : IBiometricAuthService
{
    private const string ChaveUsuario = "investpromax.biometric.usuario";
    private const string ChaveSenha = "investpromax.biometric.senha";
    private const int BiometricStrong = 0x000F;
    private const int BiometricWeak = 0x00FF;

    public Task<bool> EstaDisponivelAsync()
    {
#if ANDROID
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                return Task.FromResult(false);
            }

            var context = global::Android.App.Application.Context;
            var biometricManager = (global::Android.Hardware.Biometrics.BiometricManager?)context.GetSystemService(global::Android.Content.Context.BiometricService);
            if (biometricManager is null)
            {
                return Task.FromResult(false);
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var authenticators = BiometricStrong | BiometricWeak;
                var result = biometricManager.CanAuthenticate(authenticators);
                return Task.FromResult(result == global::Android.Hardware.Biometrics.BiometricCode.Success);
            }

#pragma warning disable CA1422
            return Task.FromResult(biometricManager.CanAuthenticate() == global::Android.Hardware.Biometrics.BiometricCode.Success);
#pragma warning restore CA1422
        }
        catch
        {
            return Task.FromResult(false);
        }
#else
        return Task.FromResult(false);
#endif
    }

    public async Task<bool> PossuiCredenciaisSalvasAsync()
    {
        try
        {
            var usuario = await SecureStorage.Default.GetAsync(ChaveUsuario);
            var senha = await SecureStorage.Default.GetAsync(ChaveSenha);
            return !string.IsNullOrWhiteSpace(usuario) && !string.IsNullOrWhiteSpace(senha);
        }
        catch
        {
            return false;
        }
    }

    public async Task SalvarCredenciaisAsync(string nomeUsuario, string senha)
    {
        if (string.IsNullOrWhiteSpace(nomeUsuario) || string.IsNullOrWhiteSpace(senha))
        {
            return;
        }

        await SecureStorage.Default.SetAsync(ChaveUsuario, nomeUsuario);
        await SecureStorage.Default.SetAsync(ChaveSenha, senha);
    }

    public async Task<CredenciaisBiometricas?> ObterCredenciaisSalvasAsync()
    {
        try
        {
            var usuario = await SecureStorage.Default.GetAsync(ChaveUsuario);
            var senha = await SecureStorage.Default.GetAsync(ChaveSenha);
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(senha))
            {
                return null;
            }
            return new CredenciaisBiometricas(usuario, senha);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> AutenticarAsync(string motivo = "Use sua digital ou reconhecimento facial para acessar o InvestProMax")
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            return false;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                var activity = Platform.CurrentActivity;
                if (activity is null)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                var executor = activity.MainExecutor;
                if (executor is null)
                {
                    tcs.TrySetResult(false);
                    return;
                }

                var builder = new global::Android.Hardware.Biometrics.BiometricPrompt.Builder(activity)
                    .SetTitle("InvestProMax")
                    .SetSubtitle(motivo);

                if (OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    builder.SetAllowedAuthenticators(BiometricStrong | BiometricWeak);
                }

                builder.SetNegativeButton("Usar senha", executor, new NegativeButtonClickListener(() => tcs.TrySetResult(false)));

                var prompt = builder.Build();
                var cancellationSignal = new global::Android.OS.CancellationSignal();
                prompt.Authenticate(cancellationSignal, executor, new BiometricAuthCallback(tcs));
            }
            catch
            {
                tcs.TrySetResult(false);
            }
        });

        return await tcs.Task;
#else
        await Task.CompletedTask;
        return false;
#endif
    }

#if ANDROID
    private sealed class NegativeButtonClickListener(Action onCancel)
        : Java.Lang.Object, global::Android.Content.IDialogInterfaceOnClickListener
    {
        public void OnClick(global::Android.Content.IDialogInterface? dialog, int which) => onCancel();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android28.0")]
    private sealed class BiometricAuthCallback(TaskCompletionSource<bool> tcs)
        : global::Android.Hardware.Biometrics.BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(global::Android.Hardware.Biometrics.BiometricPrompt.AuthenticationResult? result)
        {
            base.OnAuthenticationSucceeded(result);
            tcs.TrySetResult(true);
        }

        public override void OnAuthenticationError(global::Android.Hardware.Biometrics.BiometricErrorCode errorCode, Java.Lang.ICharSequence? errString)
        {
            base.OnAuthenticationError(errorCode, errString);
            tcs.TrySetResult(false);
        }
    }
#endif
}
