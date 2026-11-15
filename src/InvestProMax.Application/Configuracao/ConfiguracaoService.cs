using InvestProMax.Application.Ativos;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Contas;
using InvestProMax.Application.Papeis;

namespace InvestProMax.Application.Configuracao;

public interface IConfiguracaoService
{
    Task ZerarBaseAsync(CancellationToken ct = default);

    /// <summary>Apaga só a carteira de investimentos (papéis, transações, dividendos, aluguéis);
    /// preserva ativos, inventário, contas e faturas. Útil para reimportar os extratos da B3.</summary>
    Task ZerarCarteiraAsync(CancellationToken ct = default);
}

/// <summary>
/// ASSUNÇÃO REVISADA (Fase 5): nas Fases 3/4 esta operação apagava só a carteira de
/// investimentos, com uma nota explícita para revisar quando Ativo/Conta existissem — agora
/// que existem, "apaga todos os dados do usuário autenticado" (seção 8) é lido ao pé da letra:
/// carteira de investimentos + patrimônio + contas a pagar, tudo.
/// </summary>
public sealed class ConfiguracaoService : IConfiguracaoService
{
    private readonly IPapelRepository _papelRepository;
    private readonly IAtivoRepository _ativoRepository;
    private readonly IContaRepository _contaRepository;
    private readonly IUsuarioLogado _usuarioLogado;
    private readonly IMongoBackupService? _mongoBackupService;

    public ConfiguracaoService(
        IPapelRepository papelRepository,
        IAtivoRepository ativoRepository,
        IContaRepository contaRepository,
        IUsuarioLogado usuarioLogado,
        IMongoBackupService? mongoBackupService = null)
    {
        _papelRepository = papelRepository;
        _ativoRepository = ativoRepository;
        _contaRepository = contaRepository;
        _usuarioLogado = usuarioLogado;
        _mongoBackupService = mongoBackupService;
    }

    public async Task ZerarCarteiraAsync(CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;

        if (_mongoBackupService is not null)
        {
            await _mongoBackupService.ZerarCarteiraDoUsuarioAsync(usuarioId, ct);
        }

        await _papelRepository.ZerarCarteiraDoUsuarioAsync(usuarioId, ct);
    }

    public async Task ZerarBaseAsync(CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;

        if (_mongoBackupService is not null)
        {
            await _mongoBackupService.ZerarBaseDoUsuarioAsync(usuarioId, ct);
        }

        await _papelRepository.ZerarCarteiraDoUsuarioAsync(usuarioId, ct);
        await _ativoRepository.ZerarPatrimonioDoUsuarioAsync(usuarioId, ct);
        await _contaRepository.ZerarContasDoUsuarioAsync(usuarioId, ct);
    }
}
