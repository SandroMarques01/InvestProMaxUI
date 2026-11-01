using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvestProMax.SharedUI.Auth;
using InvestProMax.SharedUI.Dtos;

namespace InvestProMax.SharedUI.Api;

/// <summary>
/// Cliente HTTP tipado registrado como HttpClient tipado (AddHttpClient&lt;IInvestProMaxApiClient,
/// InvestProMaxApiClient&gt;) — o BaseAddress é configurado por cada head (Fase 7), não aqui.
///
/// Renovação silenciosa de token: toda chamada autenticada passa por EnviarAsync, que garante
/// um access token válido antes de mandar a requisição (renovando via refresh token se preciso)
/// e, se mesmo assim vier 401 (ex.: token revogado por reuso — ver AuthService), tenta renovar
/// mais uma vez antes de desistir e limpar a sessão. As páginas só precisam tratar ApiException
/// com EhNaoAutorizado == true redirecionando para /login — nunca lidam com token diretamente.
/// </summary>
public sealed class InvestProMaxApiClient : IInvestProMaxApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AuthState _authState;
    private readonly ITokenStorage _tokenStorage;

    public InvestProMaxApiClient(HttpClient http, AuthState authState, ITokenStorage tokenStorage)
    {
        _http = http;
        _authState = authState;
        _tokenStorage = tokenStorage;
    }

    // ===== Auth =====

    public async Task<AutenticacaoResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        try
        {
            using var resposta = await _http.PostAsJsonAsync("api/auth/login", request, JsonOptions, ct);
            if (!resposta.IsSuccessStatusCode)
            {
                throw await CriarExcecaoAsync(resposta, ct);
            }

            var autenticacao = (await resposta.Content.ReadFromJsonAsync<AutenticacaoResponse>(JsonOptions, ct))!;
            await AplicarSessaoAsync(autenticacao);
            return autenticacao;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException(0, "Não foi possível conectar ao servidor da API (https://localhost:7199). Verifique se o back-end está em execução.", ex);
        }
    }

    public async Task AlterarSenhaAsync(AlterarSenhaRequest request, CancellationToken ct = default) =>
        await EnviarSemRespostaAsync(HttpMethod.Post, "api/auth/alterar-senha", request, ct);

    public Task<UsuarioResponse> CriarUsuarioAsync(CriarUsuarioRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<UsuarioResponse>(HttpMethod.Post, "api/auth/usuarios", request, ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        _authState.LimparSessao();
        await _tokenStorage.LimparAsync();
    }

    public async Task<bool> RestaurarSessaoAsync(CancellationToken ct = default)
    {
        var refreshTokenSalvo = await _tokenStorage.ObterRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshTokenSalvo))
        {
            return false;
        }

        _authState.DefinirRefreshTokenPendente(refreshTokenSalvo);
        return await TentarRenovarTokenAsync(ct);
    }

    // ===== Papeis =====

    public Task<PaginaResultado<PapelResponse>> ListarPapeisAsync(
        TipoPapel? tipoPapel = null, bool? ativo = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        ObterAsync<PaginaResultado<PapelResponse>>(
            ComQuery("api/papeis", ("tipoPapel", tipoPapel), ("ativo", ativo), ("page", page), ("pageSize", pageSize)), ct);

    public Task<PapelResponse> ObterPapelAsync(Guid id, CancellationToken ct = default) =>
        ObterAsync<PapelResponse>($"api/papeis/{id}", ct);

    public Task<PapelDetalheResponse> ObterDetalhePapelAsync(Guid id, CancellationToken ct = default) =>
        ObterAsync<PapelDetalheResponse>($"api/papeis/{id}/detalhe", ct);

    public Task<PapelResponse> CriarPapelAsync(CriarPapelRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<PapelResponse>(HttpMethod.Post, "api/papeis", request, ct);

    public Task<PapelResponse> AtualizarPapelAsync(Guid id, AtualizarPapelRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<PapelResponse>(HttpMethod.Put, $"api/papeis/{id}", request, ct);

    public Task RemoverPapelAsync(Guid id, CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Delete, $"api/papeis/{id}", null, ct);

    public Task<PapelResponse> TrocarPapelAsync(Guid id, TrocarPapelRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<PapelResponse>(HttpMethod.Post, $"api/papeis/{id}/trocar", request, ct);

    public Task<PapelResponse> DesdobrarPapelAsync(Guid id, DesdobramentoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<PapelResponse>(HttpMethod.Post, $"api/papeis/{id}/desdobramento", request, ct);

    public Task<PapeisGeralResponse> ObterPapeisGeralAsync(
        TipoPapel? tipoPapel = null, bool? somenteAtivos = true, DateOnly? dataFinal = null, CancellationToken ct = default) =>
        ObterAsync<PapeisGeralResponse>(
            ComQuery("api/papeis/geral", ("tipoPapel", tipoPapel), ("somenteAtivos", somenteAtivos), ("dataFinal", dataFinal)), ct);

    public Task<SolicitarAtualizacaoResponse> SolicitarAtualizacaoCotacoesAsync(CancellationToken ct = default) =>
        EnviarJsonAsync<SolicitarAtualizacaoResponse>(HttpMethod.Post, "api/cotacoes/atualizar", null, ct);

    // ===== Transacoes =====

    public Task<PaginaResultado<TransacaoResponse>> ListarTransacoesAsync(
        Guid? papelId = null, TipoPapel? tipoPapel = null, DateOnly? dataInicio = null, DateOnly? dataFim = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        ObterAsync<PaginaResultado<TransacaoResponse>>(
            ComQuery("api/transacoes", ("papelId", papelId), ("tipoPapel", tipoPapel), ("dataInicio", dataInicio), ("dataFim", dataFim), ("page", page), ("pageSize", pageSize)), ct);

    public Task<TransacaoResponse> ObterTransacaoAsync(Guid id, CancellationToken ct = default) =>
        ObterAsync<TransacaoResponse>($"api/transacoes/{id}", ct);

    public Task<TransacaoResponse> CriarTransacaoAsync(CriarTransacaoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<TransacaoResponse>(HttpMethod.Post, "api/transacoes", request, ct);

    public Task<TransacaoResponse> AtualizarTransacaoAsync(Guid id, AtualizarTransacaoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<TransacaoResponse>(HttpMethod.Put, $"api/transacoes/{id}", request, ct);

    public Task RemoverTransacaoAsync(Guid id, CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Delete, $"api/transacoes/{id}", null, ct);

    // ===== Dividendos =====

    public Task<PaginaResultado<DividendoResponse>> ListarDividendosAsync(
        Guid? papelId = null, TipoPapel? tipoPapel = null, DateOnly? dataInicio = null, DateOnly? dataFim = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        ObterAsync<PaginaResultado<DividendoResponse>>(
            ComQuery("api/dividendos", ("papelId", papelId), ("tipoPapel", tipoPapel), ("dataInicio", dataInicio), ("dataFim", dataFim), ("page", page), ("pageSize", pageSize)), ct);

    public Task<DividendoResponse> ObterDividendoAsync(Guid id, CancellationToken ct = default) =>
        ObterAsync<DividendoResponse>($"api/dividendos/{id}", ct);

    public Task<DividendoResponse> CriarDividendoAsync(CriarDividendoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<DividendoResponse>(HttpMethod.Post, "api/dividendos", request, ct);

    public Task<DividendoResponse> AtualizarDividendoAsync(Guid id, AtualizarDividendoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<DividendoResponse>(HttpMethod.Put, $"api/dividendos/{id}", request, ct);

    public Task RemoverDividendoAsync(Guid id, CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Delete, $"api/dividendos/{id}", null, ct);

    // ===== Segmentos =====

    public Task<PaginaResultado<SegmentoResponse>> ListarSegmentosAsync(
        bool? ativo = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        ObterAsync<PaginaResultado<SegmentoResponse>>(
            ComQuery("api/segmentos", ("ativo", ativo), ("page", page), ("pageSize", pageSize)), ct);

    public Task<SegmentoResponse> CriarSegmentoAsync(CriarSegmentoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<SegmentoResponse>(HttpMethod.Post, "api/segmentos", request, ct);

    public Task<SegmentoResponse> AtualizarSegmentoAsync(Guid id, AtualizarSegmentoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<SegmentoResponse>(HttpMethod.Put, $"api/segmentos/{id}", request, ct);

    public Task RemoverSegmentoAsync(Guid id, CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Delete, $"api/segmentos/{id}", null, ct);

    // ===== Ativos =====

    public Task<PaginaResultado<AtivoResponse>> ListarAtivosAsync(
        TipoAtivo? tipoAtivo = null, bool? statusAtivo = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        ObterAsync<PaginaResultado<AtivoResponse>>(
            ComQuery("api/ativos", ("tipoAtivo", tipoAtivo), ("statusAtivo", statusAtivo), ("page", page), ("pageSize", pageSize)), ct);

    public Task<AtivoResponse> CriarAtivoAsync(CriarAtivoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<AtivoResponse>(HttpMethod.Post, "api/ativos", request, ct);

    public Task<AtivoResponse> AtualizarAtivoAsync(Guid id, AtualizarAtivoRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<AtivoResponse>(HttpMethod.Put, $"api/ativos/{id}", request, ct);

    public Task RemoverAtivoAsync(Guid id, CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Delete, $"api/ativos/{id}", null, ct);

    // ===== Inventario =====

    public Task<PaginaResultado<InventarioResponse>> ListarInventarioAsync(
        Guid? ativoId = null, int? ano = null, int? mes = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        ObterAsync<PaginaResultado<InventarioResponse>>(
            ComQuery("api/inventario", ("ativoId", ativoId), ("ano", ano), ("mes", mes), ("page", page), ("pageSize", pageSize)), ct);

    public Task<LancamentoLoteResponse> LancarLoteInventarioAsync(LancamentoLoteRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<LancamentoLoteResponse>(HttpMethod.Post, "api/inventario/lote", request, ct);

    public Task<SugestaoCalculadaResponse> ObterSugestaoCalculadaAsync(int ano, int mes, CancellationToken ct = default) =>
        ObterAsync<SugestaoCalculadaResponse>(ComQuery("api/inventario/sugestao-calculada", ("ano", ano), ("mes", mes)), ct);

    // ===== Contas =====

    public Task<PaginaResultado<ContaResponse>> ListarContasAsync(
        bool? statusAtivo = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        ObterAsync<PaginaResultado<ContaResponse>>(
            ComQuery("api/contas", ("statusAtivo", statusAtivo), ("page", page), ("pageSize", pageSize)), ct);

    public Task<ContaResponse> CriarContaAsync(CriarContaRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<ContaResponse>(HttpMethod.Post, "api/contas", request, ct);

    public Task<ContaResponse> AtualizarContaAsync(Guid id, AtualizarContaRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<ContaResponse>(HttpMethod.Put, $"api/contas/{id}", request, ct);

    public Task RemoverContaAsync(Guid id, CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Delete, $"api/contas/{id}", null, ct);

    // ===== Faturas =====

    public Task<PaginaResultado<FaturaResponse>> ListarFaturasAsync(
        StatusFatura? status = null, DateOnly? competencia = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        ObterAsync<PaginaResultado<FaturaResponse>>(
            ComQuery("api/faturas", ("status", status), ("competencia", competencia), ("page", page), ("pageSize", pageSize)), ct);

    public Task<GerarFaturasResponse> GerarFaturasAsync(CancellationToken ct = default) =>
        EnviarJsonAsync<GerarFaturasResponse>(HttpMethod.Post, "api/faturas/gerar", null, ct);

    public Task<FaturaResponse> PagarFaturaAsync(Guid id, PagarFaturaRequest request, CancellationToken ct = default) =>
        EnviarJsonAsync<FaturaResponse>(HttpMethod.Post, $"api/faturas/{id}/pagar", request, ct);

    // ===== Configuracao =====

    public Task<ImportarB3Response> ImportarB3Async(Stream arquivo, string nomeArquivo, CancellationToken ct = default) =>
        EnviarMultipartAsync<ImportarB3Response>("api/configuracao/importar-b3", arquivo, nomeArquivo, ct);

    public Task<ImportarCotacoesResponse> ImportarCotacoesAsync(Stream arquivo, string nomeArquivo, CancellationToken ct = default) =>
        EnviarMultipartAsync<ImportarCotacoesResponse>("api/configuracao/importar-cotacoes", arquivo, nomeArquivo, ct);

    public async Task<(Stream Conteudo, string NomeArquivo)> BaixarBackupAsync(CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/configuracao/backup"), ct);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(ct);
        var nomeArquivo = resposta.Content.Headers.ContentDisposition?.FileNameStar
            ?? resposta.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"investpromax-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return (new MemoryStream(bytes), nomeArquivo);
    }

    public Task<RestaurarBackupResponse> RestaurarBackupAsync(Stream arquivo, string nomeArquivo, CancellationToken ct = default) =>
        EnviarMultipartAsync<RestaurarBackupResponse>("api/configuracao/backup/restaurar", arquivo, nomeArquivo, ct);

    public Task<BackupMongoResponse> ExecutarBackupMongoAsync(CancellationToken ct = default) =>
        EnviarJsonAsync<BackupMongoResponse>(HttpMethod.Post, "api/configuracao/backup/mongo", null, ct);

    public Task<StatusMongoResponse> ObterStatusMongoAsync(CancellationToken ct = default) =>
        ObterAsync<StatusMongoResponse>("api/configuracao/backup/mongo/status", ct);

    public string UrlApi => _http.BaseAddress?.ToString().TrimEnd('/') ?? "https://localhost:7199";

    public Task<InfoAmbienteResponse> ObterInfoAmbienteAsync(CancellationToken ct = default) =>
        ObterAsync<InfoAmbienteResponse>("api/configuracao/info-ambiente", ct);

    public Task ZerarCarteiraAsync(CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Delete, "api/configuracao/zerar-carteira", null, ct);

    public Task ZerarBaseAsync(CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Delete, "api/configuracao/zerar-base", null, ct);

    // ===== Dashboard =====

    public Task<DashboardResponse> ObterDashboardAsync(CancellationToken ct = default) =>
        ObterAsync<DashboardResponse>("api/dashboard", ct);

    // ===== Infra interna =====

    private async Task<T> ObterAsync<T>(string url, CancellationToken ct)
    {
        using var resposta = await EnviarAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
        return (await resposta.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
    }

    private async Task<T> EnviarJsonAsync<T>(HttpMethod metodo, string url, object? corpo, CancellationToken ct)
    {
        using var resposta = await EnviarAsync(() => CriarRequisicaoJson(metodo, url, corpo), ct);
        return (await resposta.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
    }

    private async Task EnviarSemRespostaAsync(HttpMethod metodo, string url, object? corpo, CancellationToken ct)
    {
        using var resposta = await EnviarAsync(() => CriarRequisicaoJson(metodo, url, corpo), ct);
    }

    private static HttpRequestMessage CriarRequisicaoJson(HttpMethod metodo, string url, object? corpo)
    {
        var requisicao = new HttpRequestMessage(metodo, url);
        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo, options: JsonOptions);
        }
        return requisicao;
    }

    private async Task<T> EnviarMultipartAsync<T>(string url, Stream arquivo, string nomeArquivo, CancellationToken ct)
    {
        using var resposta = await EnviarAsync(() =>
        {
            // A stream precisa ser rebobinada aqui porque este delegate pode rodar duas vezes
            // (uma retentativa após renovar o token consome a stream de novo) — exige que
            // `arquivo` seja seekable (é o caso de um MemoryStream vindo de InputFile do Blazor).
            arquivo.Position = 0;
            var multipart = new MultipartFormDataContent { { new StreamContent(arquivo), "arquivo", nomeArquivo } };
            return new HttpRequestMessage(HttpMethod.Post, url) { Content = multipart };
        }, ct);
        return (await resposta.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
    }

    private async Task<HttpResponseMessage> EnviarAsync(Func<HttpRequestMessage> criarRequisicao, CancellationToken ct)
    {
        try
        {
            await GarantirTokenValidoAsync(ct);

            using var primeiraRequisicao = criarRequisicao();
            AnexarToken(primeiraRequisicao);
            var resposta = await _http.SendAsync(primeiraRequisicao, ct);

            if (resposta.StatusCode == HttpStatusCode.Unauthorized && _authState.RefreshToken is not null)
            {
                if (await TentarRenovarTokenAsync(ct))
                {
                    resposta.Dispose();
                    using var segundaRequisicao = criarRequisicao();
                    AnexarToken(segundaRequisicao);
                    resposta = await _http.SendAsync(segundaRequisicao, ct);
                }
            }

            if (!resposta.IsSuccessStatusCode)
            {
                throw await CriarExcecaoAsync(resposta, ct);
            }

            return resposta;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException(0, "Não foi possível conectar ao servidor da API (https://localhost:7199). Verifique se o back-end está em execução.", ex);
        }
    }

    private void AnexarToken(HttpRequestMessage requisicao)
    {
        if (_authState.AccessToken is not null)
        {
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.AccessToken);
        }
    }

    private async Task GarantirTokenValidoAsync(CancellationToken ct)
    {
        var prestesAVencer = _authState.AccessTokenExpiraEmUtc is null
            || _authState.AccessTokenExpiraEmUtc <= DateTime.UtcNow.AddSeconds(30);

        if (prestesAVencer && _authState.RefreshToken is not null)
        {
            await TentarRenovarTokenAsync(ct);
        }
    }

    private async Task<bool> TentarRenovarTokenAsync(CancellationToken ct)
    {
        if (_authState.RefreshToken is null)
        {
            return false;
        }

        try
        {
            using var resposta = await _http.PostAsJsonAsync(
                "api/auth/refresh", new RefreshRequest(_authState.RefreshToken), JsonOptions, ct);

            if (!resposta.IsSuccessStatusCode)
            {
                await LogoutAsync(ct);
                return false;
            }

            var autenticacao = (await resposta.Content.ReadFromJsonAsync<AutenticacaoResponse>(JsonOptions, ct))!;
            await AplicarSessaoAsync(autenticacao);
            return true;
        }
        catch (HttpRequestException)
        {
            // Sem rede — mantém a sessão como estava (não desloga por causa de uma falha
            // transitória de conectividade); a próxima chamada tenta de novo.
            return false;
        }
    }

    private async Task AplicarSessaoAsync(AutenticacaoResponse autenticacao)
    {
        _authState.DefinirSessao(autenticacao);
        await _tokenStorage.SalvarRefreshTokenAsync(autenticacao.RefreshToken);
    }

    private static string ComQuery(string caminho, params (string Nome, object? Valor)[] parametros)
    {
        var partes = parametros
            .Where(p => p.Valor is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Nome)}={Uri.EscapeDataString(FormatarValorDeQuery(p.Valor!))}");

        var query = string.Join("&", partes);
        return query.Length == 0 ? caminho : $"{caminho}?{query}";
    }

    private static string FormatarValorDeQuery(object valor) => valor switch
    {
        DateOnly data => data.ToString("yyyy-MM-dd"),
        Enum enumValor => Convert.ToInt32(enumValor).ToString(),
        bool booleano => booleano ? "true" : "false",
        _ => valor.ToString() ?? string.Empty
    };

    private static async Task<ApiException> CriarExcecaoAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        var statusCode = (int)resposta.StatusCode;
        try
        {
            var problema = await resposta.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions, ct);
            var mensagem = problema?.Detail ?? problema?.Title ?? resposta.ReasonPhrase ?? "Erro ao comunicar com a API.";
            return new ApiException(statusCode, mensagem);
        }
        catch (JsonException)
        {
            return new ApiException(statusCode, resposta.ReasonPhrase ?? "Erro ao comunicar com a API.");
        }
    }
}
