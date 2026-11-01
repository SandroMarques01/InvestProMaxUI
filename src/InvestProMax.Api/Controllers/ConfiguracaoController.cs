using InvestProMax.Application.Configuracao;
using InvestProMax.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvestProMax.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/configuracao")]
public class ConfiguracaoController : ControllerBase
{
    private const string TipoConteudoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IImportacaoB3Service _importacaoB3Service;
    private readonly IImportacaoCotacoesService _importacaoCotacoesService;
    private readonly IBackupService _backupService;
    private readonly IConfiguracaoService _configuracaoService;
    private readonly IMongoBackupService _mongoBackupService;

    public ConfiguracaoController(
        IImportacaoB3Service importacaoB3Service,
        IImportacaoCotacoesService importacaoCotacoesService,
        IBackupService backupService,
        IConfiguracaoService configuracaoService,
        IMongoBackupService mongoBackupService)
    {
        _importacaoB3Service = importacaoB3Service;
        _importacaoCotacoesService = importacaoCotacoesService;
        _backupService = backupService;
        _configuracaoService = configuracaoService;
        _mongoBackupService = mongoBackupService;
    }

    [HttpPost("importar-b3")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ImportarB3Response>> ImportarB3(IFormFile arquivo, CancellationToken ct)
    {
        await using var stream = await AbrirArquivoAsync(arquivo, ct);
        return Ok(await _importacaoB3Service.ImportarAsync(stream, ct));
    }

    [HttpPost("importar-cotacoes")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ImportarCotacoesResponse>> ImportarCotacoes(IFormFile arquivo, CancellationToken ct)
    {
        await using var stream = await AbrirArquivoAsync(arquivo, ct);
        return Ok(await _importacaoCotacoesService.ImportarAsync(stream, ct));
    }

    [HttpGet("backup")]
    public async Task<IActionResult> Backup(CancellationToken ct)
    {
        var conteudo = await _backupService.ExportarAsync(ct);
        var nomeArquivo = $"investpromax-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(conteudo, TipoConteudoXlsx, nomeArquivo);
    }

    [HttpPost("backup/restaurar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<RestaurarBackupResponse>> RestaurarBackup(IFormFile arquivo, CancellationToken ct)
    {
        await using var stream = await AbrirArquivoAsync(arquivo, ct);
        return Ok(await _backupService.RestaurarAsync(stream, ct));
    }

    [HttpPost("backup/mongo")]
    public async Task<ActionResult<BackupMongoResponse>> BackupMongo(CancellationToken ct)
    {
        return Ok(await _mongoBackupService.ExecutarBackupAsync(ct));
    }

    [HttpGet("backup/mongo/status")]
    public async Task<ActionResult<StatusMongoResponse>> StatusMongo(CancellationToken ct)
    {
        return Ok(await _mongoBackupService.ObterStatusAsync(ct));
    }

    [HttpDelete("zerar-carteira")]
    public async Task<IActionResult> ZerarCarteira(CancellationToken ct)
    {
        await _configuracaoService.ZerarCarteiraAsync(ct);
        return NoContent();
    }

    [HttpDelete("zerar-base")]
    public async Task<IActionResult> ZerarBase(CancellationToken ct)
    {
        await _configuracaoService.ZerarBaseAsync(ct);
        return NoContent();
    }

    [HttpGet("info-ambiente")]
    public async Task<ActionResult<InfoAmbienteResponse>> ObterInfoAmbiente(
        [FromServices] InvestProMaxDbContext db,
        [FromServices] IWebHostEnvironment env,
        CancellationToken ct,
        [FromServices] IDbContextFactory<SupabaseDbContext>? supabaseFactory = null)
    {
        var provedor = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true
            ? "PostgreSQL (Supabase)"
            : "SQL Server (LocalDB)";

        var connStr = db.Database.GetConnectionString() ?? "";
        string servidor = "Desconhecido";
        string banco = "postgres";

        try
        {
            if (connStr.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new Npgsql.NpgsqlConnectionStringBuilder(connStr);
                servidor = $"{builder.Host}:{builder.Port}";
                banco = builder.Database ?? "postgres";
            }
            else if (connStr.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) || connStr.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connStr);
                servidor = builder.DataSource;
                banco = builder.InitialCatalog;
            }
        }
        catch
        {
            servidor = "Configurado";
        }

        bool conectado;
        try
        {
            conectado = await db.Database.CanConnectAsync(ct);
        }
        catch
        {
            conectado = false;
        }

        if (supabaseFactory is not null)
        {
            try
            {
                await using var supabaseDb = await supabaseFactory.CreateDbContextAsync(ct);
                var supabaseConnStr = supabaseDb.Database.GetConnectionString() ?? "";
                var supabaseBuilder = new Npgsql.NpgsqlConnectionStringBuilder(supabaseConnStr);
                var supabaseOk = await supabaseDb.Database.CanConnectAsync(ct);

                provedor = supabaseOk
                    ? $"{provedor} + PostgreSQL (Supabase Sync Ativo)"
                    : $"{provedor} + PostgreSQL (Supabase Offline)";
                servidor = $"{servidor} | {supabaseBuilder.Host}:{supabaseBuilder.Port}";
                banco = $"{banco} | {supabaseBuilder.Database ?? "postgres"}";
            }
            catch
            {
                provedor = $"{provedor} + PostgreSQL (Supabase)";
            }
        }

        return Ok(new InfoAmbienteResponse(
            ProvedorBanco: provedor,
            ServidorBanco: servidor,
            NomeBanco: banco,
            Conectado: conectado,
            Ambiente: env.EnvironmentName,
            DataHoraServidorUtc: DateTime.UtcNow));
    }

    private static async Task<MemoryStream> AbrirArquivoAsync(IFormFile? arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            throw new BadHttpRequestException("Envie um arquivo no campo 'arquivo' (multipart/form-data).");
        }

        var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, ct);
        memoria.Position = 0;
        return memoria;
    }
}
