using InvestProMax.SharedUI.Dtos;

namespace InvestProMax.SharedUI.Api;

/// <summary>
/// Um método por endpoint da Api (seção 8), deliberadamente não-genérico: os controllers do
/// back-end também não seguem um CRUD genérico (cada um tem suas próprias regras de
/// validação e ações extras, como TrocarPapel/Desdobrar), então espelhar isso 1:1 aqui evita
/// uma camada de abstração que teria que ser furada a cada endpoint que foge do CRUD comum.
/// </summary>
public interface IInvestProMaxApiClient
{
    // Auth
    Task<AutenticacaoResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request, CancellationToken ct = default);
    Task AlterarSenhaAsync(AlterarSenhaRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);

    /// <summary>
    /// Chamado uma vez pelo head na inicialização do app (antes de renderizar), pra tentar
    /// retomar a sessão a partir do refresh token persistido (ITokenStorage). Retorna false se
    /// não havia refresh token salvo ou se ele não é mais válido — nesse caso o usuário cai na
    /// tela de login normalmente, via AuthorizeRouteView/RedirectToLogin.
    /// </summary>
    Task<bool> RestaurarSessaoAsync(CancellationToken ct = default);

    // Papeis
    Task<PaginaResultado<PapelResponse>> ListarPapeisAsync(TipoPapel? tipoPapel = null, bool? ativo = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<PapelResponse> ObterPapelAsync(Guid id, CancellationToken ct = default);
    Task<PapelDetalheResponse> ObterDetalhePapelAsync(Guid id, CancellationToken ct = default);
    Task<PapelResponse> CriarPapelAsync(CriarPapelRequest request, CancellationToken ct = default);
    Task<PapelResponse> AtualizarPapelAsync(Guid id, AtualizarPapelRequest request, CancellationToken ct = default);
    Task RemoverPapelAsync(Guid id, CancellationToken ct = default);
    Task<PapelResponse> TrocarPapelAsync(Guid id, TrocarPapelRequest request, CancellationToken ct = default);
    Task<PapelResponse> DesdobrarPapelAsync(Guid id, DesdobramentoRequest request, CancellationToken ct = default);
    Task<PapeisGeralResponse> ObterPapeisGeralAsync(TipoPapel? tipoPapel = null, bool? somenteAtivos = true, DateOnly? dataFinal = null, CancellationToken ct = default);
    Task<SolicitarAtualizacaoResponse> SolicitarAtualizacaoCotacoesAsync(CancellationToken ct = default);

    // Transacoes
    Task<PaginaResultado<TransacaoResponse>> ListarTransacoesAsync(
        Guid? papelId = null, TipoPapel? tipoPapel = null, DateOnly? dataInicio = null, DateOnly? dataFim = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<TransacaoResponse> ObterTransacaoAsync(Guid id, CancellationToken ct = default);
    Task<TransacaoResponse> CriarTransacaoAsync(CriarTransacaoRequest request, CancellationToken ct = default);
    Task<TransacaoResponse> AtualizarTransacaoAsync(Guid id, AtualizarTransacaoRequest request, CancellationToken ct = default);
    Task RemoverTransacaoAsync(Guid id, CancellationToken ct = default);

    // Dividendos
    Task<PaginaResultado<DividendoResponse>> ListarDividendosAsync(
        Guid? papelId = null, TipoPapel? tipoPapel = null, DateOnly? dataInicio = null, DateOnly? dataFim = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<DividendoResponse> ObterDividendoAsync(Guid id, CancellationToken ct = default);
    Task<DividendoResponse> CriarDividendoAsync(CriarDividendoRequest request, CancellationToken ct = default);
    Task<DividendoResponse> AtualizarDividendoAsync(Guid id, AtualizarDividendoRequest request, CancellationToken ct = default);
    Task RemoverDividendoAsync(Guid id, CancellationToken ct = default);

    // Segmentos
    Task<PaginaResultado<SegmentoResponse>> ListarSegmentosAsync(bool? ativo = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<SegmentoResponse> CriarSegmentoAsync(CriarSegmentoRequest request, CancellationToken ct = default);
    Task<SegmentoResponse> AtualizarSegmentoAsync(Guid id, AtualizarSegmentoRequest request, CancellationToken ct = default);
    Task RemoverSegmentoAsync(Guid id, CancellationToken ct = default);

    // Ativos
    Task<PaginaResultado<AtivoResponse>> ListarAtivosAsync(TipoAtivo? tipoAtivo = null, bool? statusAtivo = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<AtivoResponse> CriarAtivoAsync(CriarAtivoRequest request, CancellationToken ct = default);
    Task<AtivoResponse> AtualizarAtivoAsync(Guid id, AtualizarAtivoRequest request, CancellationToken ct = default);
    Task RemoverAtivoAsync(Guid id, CancellationToken ct = default);

    // Inventario
    Task<PaginaResultado<InventarioResponse>> ListarInventarioAsync(Guid? ativoId = null, int? ano = null, int? mes = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<LancamentoLoteResponse> LancarLoteInventarioAsync(LancamentoLoteRequest request, CancellationToken ct = default);
    Task<SugestaoCalculadaResponse> ObterSugestaoCalculadaAsync(int ano, int mes, CancellationToken ct = default);

    // Contas
    Task<PaginaResultado<ContaResponse>> ListarContasAsync(bool? statusAtivo = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ContaResponse> CriarContaAsync(CriarContaRequest request, CancellationToken ct = default);
    Task<ContaResponse> AtualizarContaAsync(Guid id, AtualizarContaRequest request, CancellationToken ct = default);
    Task RemoverContaAsync(Guid id, CancellationToken ct = default);

    // Faturas
    Task<PaginaResultado<FaturaResponse>> ListarFaturasAsync(StatusFatura? status = null, DateOnly? competencia = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<GerarFaturasResponse> GerarFaturasAsync(CancellationToken ct = default);
    Task<FaturaResponse> PagarFaturaAsync(Guid id, PagarFaturaRequest request, CancellationToken ct = default);

    // Configuracao
    string UrlApi { get; }
    Task<InfoAmbienteResponse> ObterInfoAmbienteAsync(CancellationToken ct = default);
    Task<ImportarB3Response> ImportarB3Async(Stream arquivo, string nomeArquivo, CancellationToken ct = default);
    Task<ImportarCotacoesResponse> ImportarCotacoesAsync(Stream arquivo, string nomeArquivo, CancellationToken ct = default);
    Task<(Stream Conteudo, string NomeArquivo)> BaixarBackupAsync(CancellationToken ct = default);
    Task<RestaurarBackupResponse> RestaurarBackupAsync(Stream arquivo, string nomeArquivo, CancellationToken ct = default);
    Task<BackupMongoResponse> ExecutarBackupMongoAsync(CancellationToken ct = default);
    Task<StatusMongoResponse> ObterStatusMongoAsync(CancellationToken ct = default);
    Task ZerarBaseAsync(CancellationToken ct = default);
    Task ZerarCarteiraAsync(CancellationToken ct = default);

    // Dashboard
    Task<DashboardResponse> ObterDashboardAsync(CancellationToken ct = default);
}
