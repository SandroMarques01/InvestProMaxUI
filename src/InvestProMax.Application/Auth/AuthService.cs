using System.Security.Cryptography;
using System.Text;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Domain.Entidades;
using InvestProMax.Domain.Enums;
using Microsoft.Extensions.Options;

namespace InvestProMax.Application.Auth;

public sealed class AuthService : IAuthService
{
    private const string BotUsernamePadrao = "MarkX_InvestProMax_MarquesFC_bot";
    private static readonly TimeSpan ValidadeCodigoTelegram = TimeSpan.FromHours(48);

    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly AuthOptions _options;
    private readonly INotificadorTelegramService? _notificadorTelegram;

    public AuthService(
        IUsuarioRepository usuarioRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<AuthOptions> options,
        INotificadorTelegramService? notificadorTelegram = null)
    {
        _usuarioRepository = usuarioRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _options = options.Value;
        _notificadorTelegram = notificadorTelegram;
    }

    public async Task<AutenticacaoResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var usuario = await _usuarioRepository.ObterPorNomeUsuarioAsync(request.NomeUsuario.Trim(), ct);

        if (usuario is null || !_passwordHasher.Verificar(request.Senha, usuario.SenhaHash))
        {
            // Mensagem deliberadamente genérica: não revelar se o problema foi o usuário ou a senha.
            throw new NaoAutorizadoException("Usuário ou senha inválidos.");
        }

        if (usuario.StatusCadastro != StatusCadastroUsuario.Aprovado)
        {
            await LancarExcecaoPendenciaAsync(usuario, ct);
        }

        if (!usuario.Ativo)
        {
            throw new NaoAutorizadoException("Usuário ou senha inválidos.");
        }

        return await EmitirParDeTokensAsync(usuario, ct);
    }

    public async Task<AutenticacaoResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = GeradorDeRefreshToken.CalcularHash(request.RefreshToken);
        var tokenExistente = await _refreshTokenRepository.ObterPorHashAsync(hash, ct);

        if (tokenExistente is null)
        {
            throw new NaoAutorizadoException("Refresh token inválido ou expirado.");
        }

        // Detecção de reuso: um token já revogado sendo reapresentado é sinal de possível
        // comprometimento (ex.: token roubado sendo usado depois que o dono já rotacionou).
        // Revoga todos os tokens ativos do usuário para forçar novo login em todos os
        // dispositivos, em vez de só recusar esta tentativa isolada.
        if (tokenExistente.RevogadoEmUtc is not null)
        {
            await _refreshTokenRepository.RevogarTodosDoUsuarioAsync(tokenExistente.UsuarioId, ct);
            throw new NaoAutorizadoException("Refresh token inválido ou expirado.");
        }

        if (!tokenExistente.EstaAtivo)
        {
            throw new NaoAutorizadoException("Refresh token inválido ou expirado.");
        }

        var usuario = await _usuarioRepository.ObterPorIdAsync(tokenExistente.UsuarioId, ct);
        if (usuario is null || !usuario.Ativo || usuario.StatusCadastro != StatusCadastroUsuario.Aprovado)
        {
            throw new NaoAutorizadoException("Usuário inválido.");
        }

        var resposta = await EmitirParDeTokensAsync(usuario, ct, tokenParaRevogar: tokenExistente);
        return resposta;
    }

    public async Task AlterarSenhaAsync(Guid usuarioId, AlterarSenhaRequest request, CancellationToken ct = default)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(usuarioId, ct);
        if (usuario is null)
        {
            throw new NaoAutorizadoException();
        }

        if (!_passwordHasher.Verificar(request.SenhaAtual, usuario.SenhaHash))
        {
            throw new NaoAutorizadoException("Senha atual incorreta.");
        }

        usuario.SenhaHash = _passwordHasher.Hash(request.NovaSenha);
        await _usuarioRepository.AtualizarAsync(usuario, ct);
    }

    public async Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request, CancellationToken ct = default)
    {
        if (await _usuarioRepository.ExisteNomeUsuarioAsync(request.NomeUsuario, ct))
        {
            throw new RegraDeNegocioException("Nome de usuário já está em uso.");
        }

        if (await _usuarioRepository.ExisteEmailAsync(request.Email, ct))
        {
            throw new RegraDeNegocioException("E-mail já está em uso.");
        }

        var agoraUtc = DateTime.UtcNow;
        var usuario = new Usuario
        {
            Nome = request.Nome.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            NomeUsuario = request.NomeUsuario.Trim(),
            SenhaHash = _passwordHasher.Hash(request.Senha),
            Ativo = true,
            StatusCadastro = StatusCadastroUsuario.Aprovado,
            CriadoEmUtc = agoraUtc,
            AprovadoEmUtc = agoraUtc
        };

        await _usuarioRepository.AdicionarAsync(usuario, ct);

        return ParaUsuarioResponse(usuario);
    }

    public async Task<StatusCadastroResponse> RegistrarAsync(RegistrarUsuarioRequest request, CancellationToken ct = default)
    {
        if (await _usuarioRepository.ExisteNomeUsuarioAsync(request.NomeUsuario, ct))
        {
            throw new RegraDeNegocioException("Nome de usuário já está em uso.");
        }

        if (await _usuarioRepository.ExisteEmailAsync(request.Email, ct))
        {
            throw new RegraDeNegocioException("E-mail já está em uso.");
        }

        var agoraUtc = DateTime.UtcNow;
        var codigo = GerarCodigoValidacaoTelegram();

        var usuario = new Usuario
        {
            Nome = request.Nome.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            NomeUsuario = request.NomeUsuario.Trim(),
            SenhaHash = _passwordHasher.Hash(request.Senha),
            Ativo = false,
            StatusCadastro = StatusCadastroUsuario.PendenteTelegram,
            CodigoValidacaoTelegram = codigo,
            CodigoValidacaoTelegramExpiraEmUtc = agoraUtc.Add(ValidadeCodigoTelegram),
            CriadoEmUtc = agoraUtc
        };

        await _usuarioRepository.AdicionarAsync(usuario, ct);

        return new StatusCadastroResponse(
            StatusCadastro: StatusCadastroUsuario.PendenteTelegram,
            Email: usuario.Email,
            NomeUsuario: usuario.NomeUsuario,
            Mensagem: "Cadastro criado! Agora valide seu Telegram no bot oficial para encaminhar sua solicitação ao administrador.",
            CodigoValidacaoTelegram: codigo,
            LinkTelegramBot: MontarLinkBot(codigo));
    }

    public async Task<StatusCadastroResponse> AutenticarComGoogleAsync(GoogleUsuarioInfo googleUsuario, CancellationToken ct = default)
    {
        var emailNormalizado = googleUsuario.Email.Trim().ToLowerInvariant();
        var usuario = await _usuarioRepository.ObterPorGoogleSubjectAsync(googleUsuario.Subject, ct)
            ?? await _usuarioRepository.ObterPorEmailAsync(emailNormalizado, ct);

        if (usuario is null)
        {
            var agoraUtc = DateTime.UtcNow;
            var nomeUsuario = await GerarNomeUsuarioDisponivelAsync(emailNormalizado, ct);
            var codigo = GerarCodigoValidacaoTelegram();
            var segredoAleatorio = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

            usuario = new Usuario
            {
                Nome = string.IsNullOrWhiteSpace(googleUsuario.Nome) ? nomeUsuario : googleUsuario.Nome.Trim(),
                Email = emailNormalizado,
                NomeUsuario = nomeUsuario,
                SenhaHash = _passwordHasher.Hash(segredoAleatorio),
                Ativo = false,
                StatusCadastro = StatusCadastroUsuario.PendenteTelegram,
                GoogleSubject = googleUsuario.Subject.Trim(),
                CodigoValidacaoTelegram = codigo,
                CodigoValidacaoTelegramExpiraEmUtc = agoraUtc.Add(ValidadeCodigoTelegram),
                CriadoEmUtc = agoraUtc
            };

            await _usuarioRepository.AdicionarAsync(usuario, ct);

            return new StatusCadastroResponse(
                StatusCadastro: StatusCadastroUsuario.PendenteTelegram,
                Email: usuario.Email,
                NomeUsuario: usuario.NomeUsuario,
                Mensagem: "Conta Google vinculada! Agora valide seu Telegram no bot oficial para encaminhar sua solicitação ao administrador.",
                CodigoValidacaoTelegram: codigo,
                LinkTelegramBot: MontarLinkBot(codigo));
        }

        if (string.IsNullOrWhiteSpace(usuario.GoogleSubject))
        {
            usuario.GoogleSubject = googleUsuario.Subject.Trim();
            await _usuarioRepository.AtualizarAsync(usuario, ct);
        }

        if (usuario.StatusCadastro == StatusCadastroUsuario.Aprovado)
        {
            if (!usuario.Ativo)
            {
                return new StatusCadastroResponse(
                    StatusCadastro: StatusCadastroUsuario.Rejeitado,
                    Email: usuario.Email,
                    NomeUsuario: usuario.NomeUsuario,
                    Mensagem: "Este usuário está desativado. Procure o administrador.");
            }

            var tokens = await EmitirParDeTokensAsync(usuario, ct);
            return new StatusCadastroResponse(
                StatusCadastro: StatusCadastroUsuario.Aprovado,
                Email: usuario.Email,
                NomeUsuario: usuario.NomeUsuario,
                Mensagem: "Autenticação concluída com sucesso.",
                Autenticacao: tokens);
        }

        if (usuario.StatusCadastro == StatusCadastroUsuario.PendenteTelegram)
        {
            var codigo = await GarantirCodigoTelegramValidoAsync(usuario, ct);
            return new StatusCadastroResponse(
                StatusCadastro: StatusCadastroUsuario.PendenteTelegram,
                Email: usuario.Email,
                NomeUsuario: usuario.NomeUsuario,
                Mensagem: "Seu cadastro aguarda a validação do Telegram no bot oficial.",
                CodigoValidacaoTelegram: codigo,
                LinkTelegramBot: MontarLinkBot(codigo));
        }

        if (usuario.StatusCadastro == StatusCadastroUsuario.PendenteAprovacaoAdmin)
        {
            return new StatusCadastroResponse(
                StatusCadastro: StatusCadastroUsuario.PendenteAprovacaoAdmin,
                Email: usuario.Email,
                NomeUsuario: usuario.NomeUsuario,
                Mensagem: "Seu Telegram já foi validado. Aguarde a aprovação do usuário Admin para acessar o sistema.");
        }

        return new StatusCadastroResponse(
            StatusCadastro: StatusCadastroUsuario.Rejeitado,
            Email: usuario.Email,
            NomeUsuario: usuario.NomeUsuario,
            Mensagem: "Sua solicitação de cadastro não foi aprovada pelo administrador.");
    }

    public async Task<StatusCadastroResponse> ObterStatusCadastroPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        var codigoLimpo = ExtrairCodigoTelegram(codigo);
        if (string.IsNullOrWhiteSpace(codigoLimpo))
        {
            throw new NaoEncontradoException("Código de validação não informado.");
        }

        var usuario = await _usuarioRepository.ObterPorCodigoTelegramAsync(codigoLimpo, ct)
            ?? throw new NaoEncontradoException("Solicitação de cadastro não encontrada.");

        return usuario.StatusCadastro switch
        {
            StatusCadastroUsuario.Aprovado => new StatusCadastroResponse(
                StatusCadastroUsuario.Aprovado,
                usuario.Email,
                usuario.NomeUsuario,
                "Cadastro aprovado pelo administrador! Já é possível entrar no InvestProMax."),
            StatusCadastroUsuario.PendenteTelegram => new StatusCadastroResponse(
                StatusCadastroUsuario.PendenteTelegram,
                usuario.Email,
                usuario.NomeUsuario,
                "Aguardando validação no bot do Telegram.",
                usuario.CodigoValidacaoTelegram,
                MontarLinkBot(usuario.CodigoValidacaoTelegram)),
            StatusCadastroUsuario.PendenteAprovacaoAdmin => new StatusCadastroResponse(
                StatusCadastroUsuario.PendenteAprovacaoAdmin,
                usuario.Email,
                usuario.NomeUsuario,
                "Telegram validado! Aguardando aprovação do administrador."),
            _ => new StatusCadastroResponse(
                StatusCadastroUsuario.Rejeitado,
                usuario.Email,
                usuario.NomeUsuario,
                "Solicitação de cadastro rejeitada pelo administrador.")
        };
    }

    public async Task<ValidarTelegramResponse> ValidarTelegramAsync(ValidarTelegramRequest request, CancellationToken ct = default)
    {
        var codigoLimpo = ExtrairCodigoTelegram(request.Codigo);
        if (string.IsNullOrWhiteSpace(codigoLimpo))
        {
            return new ValidarTelegramResponse(
                Sucesso: false,
                MensagemUsuario: "Código de validação não informado. Abra o bot pelo link exibido na tela de cadastro do InvestProMax.",
                UsuarioId: null, Nome: null, Email: null, NomeUsuario: null, TelegramId: null,
                AdminTelegramIds: []);
        }

        var usuario = await _usuarioRepository.ObterPorCodigoTelegramAsync(codigoLimpo, ct);
        if (usuario is null)
        {
            return new ValidarTelegramResponse(
                Sucesso: false,
                MensagemUsuario: "Código de validação não encontrado ou inválido. Gere um novo link na tela de login/cadastro do InvestProMax.",
                UsuarioId: null, Nome: null, Email: null, NomeUsuario: null, TelegramId: null,
                AdminTelegramIds: []);
        }

        if (usuario.StatusCadastro == StatusCadastroUsuario.Rejeitado)
        {
            return new ValidarTelegramResponse(
                Sucesso: false,
                MensagemUsuario: "Sua solicitação de acesso não foi aprovada pelo administrador.",
                UsuarioId: usuario.Id, Nome: usuario.Nome, Email: usuario.Email, NomeUsuario: usuario.NomeUsuario,
                TelegramId: usuario.TelegramId, AdminTelegramIds: []);
        }

        if (usuario.StatusCadastro == StatusCadastroUsuario.Aprovado)
        {
            if (usuario.TelegramId != request.TelegramId)
            {
                usuario.TelegramId = request.TelegramId;
                await _usuarioRepository.AtualizarAsync(usuario, ct);
            }

            return new ValidarTelegramResponse(
                Sucesso: true,
                MensagemUsuario: "✅ Seu cadastro já está aprovado! Você já pode fazer login no InvestProMax.",
                UsuarioId: usuario.Id, Nome: usuario.Nome, Email: usuario.Email, NomeUsuario: usuario.NomeUsuario,
                TelegramId: usuario.TelegramId, AdminTelegramIds: []);
        }

        if (usuario.StatusCadastro == StatusCadastroUsuario.PendenteTelegram
            && usuario.CodigoValidacaoTelegramExpiraEmUtc.HasValue
            && usuario.CodigoValidacaoTelegramExpiraEmUtc.Value < DateTime.UtcNow)
        {
            return new ValidarTelegramResponse(
                Sucesso: false,
                MensagemUsuario: "Este código expirou. Tente entrar ou cadastrar novamente no InvestProMax para gerar um novo link.",
                UsuarioId: usuario.Id, Nome: usuario.Nome, Email: usuario.Email, NomeUsuario: usuario.NomeUsuario,
                TelegramId: null, AdminTelegramIds: []);
        }

        var precisaAtualizar = usuario.TelegramId != request.TelegramId
            || usuario.StatusCadastro == StatusCadastroUsuario.PendenteTelegram;

        usuario.TelegramId = request.TelegramId;
        if (usuario.StatusCadastro == StatusCadastroUsuario.PendenteTelegram)
        {
            usuario.StatusCadastro = StatusCadastroUsuario.PendenteAprovacaoAdmin;
        }

        if (precisaAtualizar)
        {
            await _usuarioRepository.AtualizarAsync(usuario, ct);
        }

        var adminTelegramIds = await ObterAdminsTelegramAsync(ct);

        return new ValidarTelegramResponse(
            Sucesso: true,
            MensagemUsuario: $"✅ Telegram vinculado com sucesso para {usuario.Nome} ({usuario.Email})!\n\nAgora seu cadastro aguarda aprovação do administrador no InvestProMax. Você será notificado por aqui assim que for liberado.",
            UsuarioId: usuario.Id,
            Nome: usuario.Nome,
            Email: usuario.Email,
            NomeUsuario: usuario.NomeUsuario,
            TelegramId: usuario.TelegramId,
            AdminTelegramIds: adminTelegramIds);
    }

    public async Task<IReadOnlyList<UsuarioResponse>> ListarUsuariosAsync(CancellationToken ct = default)
    {
        var usuarios = await _usuarioRepository.ListarTodosAsync(ct);
        return usuarios.Select(ParaUsuarioResponse).ToList();
    }

    public async Task<UsuarioResponse> AprovarUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await AplicarDecisaoCadastroAsync(usuarioId, aprovar: true, notificarViaWebhook: true, ct);
        return ParaUsuarioResponse(usuario);
    }

    public async Task<UsuarioResponse> RejeitarUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await AplicarDecisaoCadastroAsync(usuarioId, aprovar: false, notificarViaWebhook: true, ct);
        return ParaUsuarioResponse(usuario);
    }

    public async Task<DecidirCadastroTelegramResponse> DecidirCadastroViaTelegramAsync(
        DecidirCadastroTelegramRequest request,
        CancellationToken ct = default)
    {
        var adminsPermitidos = await ObterAdminsTelegramAsync(ct);
        if (!adminsPermitidos.Contains(request.AdminTelegramId))
        {
            return new DecidirCadastroTelegramResponse(
                Sucesso: false,
                MensagemAdmin: "⛔ Apenas administradores autorizados podem aprovar ou rejeitar cadastros.",
                UsuarioTelegramId: null,
                MensagemUsuario: null);
        }

        var usuarioExistente = await _usuarioRepository.ObterPorIdAsync(request.UsuarioId, ct);
        if (usuarioExistente is null)
        {
            return new DecidirCadastroTelegramResponse(
                Sucesso: false,
                MensagemAdmin: "Solicitação de usuário não encontrada.",
                UsuarioTelegramId: null,
                MensagemUsuario: null);
        }

        try
        {
            var atualizado = await AplicarDecisaoCadastroAsync(request.UsuarioId, request.Aprovar, notificarViaWebhook: false, ct);
            var mensagemAdmin = request.Aprovar
                ? $"✅ Cadastro de {atualizado.Nome} ({atualizado.NomeUsuario}) aprovado com sucesso!"
                : $"❌ Cadastro de {atualizado.Nome} ({atualizado.NomeUsuario}) rejeitado.";
            var mensagemUsuario = MontarMensagemDecisaoUsuario(atualizado, request.Aprovar);

            return new DecidirCadastroTelegramResponse(
                Sucesso: true,
                MensagemAdmin: mensagemAdmin,
                UsuarioTelegramId: atualizado.TelegramId,
                MensagemUsuario: mensagemUsuario);
        }
        catch (RegraDeNegocioException ex)
        {
            return new DecidirCadastroTelegramResponse(
                Sucesso: false,
                MensagemAdmin: ex.Message,
                UsuarioTelegramId: null,
                MensagemUsuario: null);
        }
    }

    private async Task<Usuario> AplicarDecisaoCadastroAsync(
        Guid usuarioId,
        bool aprovar,
        bool notificarViaWebhook,
        CancellationToken ct)
    {
        var usuario = await _usuarioRepository.ObterPorIdAsync(usuarioId, ct)
            ?? throw new NaoEncontradoException("Usuário não encontrado.");

        if (!aprovar && string.Equals(usuario.NomeUsuario, "admin", StringComparison.OrdinalIgnoreCase))
        {
            throw new RegraDeNegocioException("Não é possível rejeitar ou desativar o usuário admin.");
        }

        if (aprovar)
        {
            usuario.StatusCadastro = StatusCadastroUsuario.Aprovado;
            usuario.Ativo = true;
            usuario.AprovadoEmUtc = DateTime.UtcNow;
            if (usuario.CodigoValidacaoTelegramExpiraEmUtc is null || usuario.CodigoValidacaoTelegramExpiraEmUtc > DateTime.UtcNow)
            {
                usuario.CodigoValidacaoTelegramExpiraEmUtc = DateTime.UtcNow;
            }
        }
        else
        {
            usuario.StatusCadastro = StatusCadastroUsuario.Rejeitado;
            usuario.Ativo = false;
            usuario.CodigoValidacaoTelegramExpiraEmUtc = DateTime.UtcNow;
        }

        await _usuarioRepository.AtualizarAsync(usuario, ct);

        if (notificarViaWebhook && usuario.TelegramId is > 0 && _notificadorTelegram is not null)
        {
            await _notificadorTelegram.NotificarUsuarioAsync(
                usuario.TelegramId.Value,
                MontarMensagemDecisaoUsuario(usuario, aprovar),
                ct);
        }

        return usuario;
    }

    private static string MontarMensagemDecisaoUsuario(Usuario usuario, bool aprovado) =>
        aprovado
            ? $"✅ Olá, {usuario.Nome}! Seu cadastro no InvestProMax foi aprovado pelo administrador. Você já pode entrar na plataforma usando {(string.IsNullOrWhiteSpace(usuario.GoogleSubject) ? $"o usuário '{usuario.NomeUsuario}'" : "sua conta Google")}."
            : $"❌ Olá, {usuario.Nome}. Sua solicitação de acesso ao InvestProMax não foi aprovada pelo administrador.";

    private async Task LancarExcecaoPendenciaAsync(Usuario usuario, CancellationToken ct)
    {
        switch (usuario.StatusCadastro)
        {
            case StatusCadastroUsuario.PendenteTelegram:
            {
                var codigo = await GarantirCodigoTelegramValidoAsync(usuario, ct);
                throw new CadastroPendenteException(
                    "Seu cadastro aguarda validação pelo Telegram.",
                    StatusCadastroUsuario.PendenteTelegram,
                    codigo,
                    MontarLinkBot(codigo));
            }
            case StatusCadastroUsuario.PendenteAprovacaoAdmin:
                throw new CadastroPendenteException(
                    "Seu Telegram já foi validado. Aguarde a aprovação do administrador para acessar a plataforma.",
                    StatusCadastroUsuario.PendenteAprovacaoAdmin,
                    usuario.CodigoValidacaoTelegram);
            case StatusCadastroUsuario.Rejeitado:
                throw new CadastroPendenteException(
                    "Sua solicitação de cadastro não foi aprovada pelo administrador.",
                    StatusCadastroUsuario.Rejeitado);
            default:
                throw new NaoAutorizadoException("Usuário ou senha inválidos.");
        }
    }

    private async Task<string> GarantirCodigoTelegramValidoAsync(Usuario usuario, CancellationToken ct)
    {
        var agoraUtc = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(usuario.CodigoValidacaoTelegram)
            || !usuario.CodigoValidacaoTelegramExpiraEmUtc.HasValue
            || usuario.CodigoValidacaoTelegramExpiraEmUtc.Value < agoraUtc)
        {
            usuario.CodigoValidacaoTelegram = GerarCodigoValidacaoTelegram();
            usuario.CodigoValidacaoTelegramExpiraEmUtc = agoraUtc.Add(ValidadeCodigoTelegram);
            await _usuarioRepository.AtualizarAsync(usuario, ct);
        }

        return usuario.CodigoValidacaoTelegram!;
    }

    private async Task<string> GerarNomeUsuarioDisponivelAsync(string email, CancellationToken ct)
    {
        var prefixo = email.Split('@')[0];
        var sb = new StringBuilder(prefixo.Length);
        foreach (var ch in prefixo.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-')
            {
                sb.Append(ch);
            }
        }

        var baseNome = sb.ToString().Trim('.', '_', '-');
        if (baseNome.Length < 3)
        {
            baseNome = "usuario";
        }
        if (baseNome.Length > 40)
        {
            baseNome = baseNome[..40];
        }

        if (!await _usuarioRepository.ExisteNomeUsuarioAsync(baseNome, ct))
        {
            return baseNome;
        }

        for (var i = 1; i <= 99; i++)
        {
            var candidato = $"{baseNome}{i}";
            if (!await _usuarioRepository.ExisteNomeUsuarioAsync(candidato, ct))
            {
                return candidato;
            }
        }

        var sufixoHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
        return $"{baseNome}_{sufixoHex}";
    }

    private async Task<IReadOnlyList<long>> ObterAdminsTelegramAsync(CancellationToken ct)
    {
        var doBanco = await _usuarioRepository.ListarTelegramIdsAdminsAsync(ct);
        var configurados = _notificadorTelegram?.ObterAdminsConfigurados() ?? [];
        return doBanco.Concat(configurados).Where(id => id > 0).Distinct().ToList();
    }

    private string? MontarLinkBot(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        return _notificadorTelegram is not null
            ? _notificadorTelegram.MontarLinkValidacao(codigo)
            : $"https://t.me/{BotUsernamePadrao}?start={Uri.EscapeDataString(codigo)}";
    }

    private static string GerarCodigoValidacaoTelegram() =>
        "ipm_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static string ExtrairCodigoTelegram(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            return string.Empty;
        }

        var texto = entrada.Trim();
        if (texto.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var partes = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return partes.Length > 1 ? partes[1] : string.Empty;
        }

        return texto;
    }

    private static UsuarioResponse ParaUsuarioResponse(Usuario usuario) => new(
        usuario.Id,
        usuario.Nome,
        usuario.Email,
        usuario.NomeUsuario,
        usuario.Ativo,
        usuario.StatusCadastro,
        usuario.TelegramId,
        usuario.CriadoEmUtc,
        usuario.AprovadoEmUtc,
        !string.IsNullOrWhiteSpace(usuario.GoogleSubject));

    private async Task<AutenticacaoResponse> EmitirParDeTokensAsync(
        Usuario usuario, CancellationToken ct, RefreshToken? tokenParaRevogar = null)
    {
        var accessToken = _tokenService.GerarAccessToken(usuario);

        var refreshTokenPlano = GeradorDeRefreshToken.GerarTokenPlano();
        var novoRefreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = GeradorDeRefreshToken.CalcularHash(refreshTokenPlano),
            CriadoEmUtc = DateTime.UtcNow,
            ExpiraEmUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenDuracaoDias)
        };

        await _refreshTokenRepository.AdicionarAsync(novoRefreshToken, ct);

        if (tokenParaRevogar is not null)
        {
            tokenParaRevogar.RevogadoEmUtc = DateTime.UtcNow;
            tokenParaRevogar.SubstituidoPorId = novoRefreshToken.Id;
            await _refreshTokenRepository.AtualizarAsync(tokenParaRevogar, ct);
        }

        return new AutenticacaoResponse(
            accessToken.Token,
            accessToken.ExpiraEmUtc,
            refreshTokenPlano,
            novoRefreshToken.ExpiraEmUtc);
    }
}
