using System.Text.Json;
using System.Text.Json.Serialization;
using InvestProMax.Application.Configuracao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace InvestProMax.Infrastructure.Persistence;

public sealed class MongoBackupService : IMongoBackupService
{
    private readonly InvestProMaxDbContext _db;
    private readonly SupabaseDbContext? _supabaseDb;
    private readonly LocalDbContext? _localDb;
    private readonly IMongoClient? _mongoClient;
    private readonly string _databaseName;
    private readonly ILogger<MongoBackupService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    public MongoBackupService(
        InvestProMaxDbContext db,
        IConfiguration configuration,
        ILogger<MongoBackupService> logger,
        IMongoClient? mongoClient = null,
        SupabaseDbContext? supabaseDb = null,
        LocalDbContext? localDb = null)
    {
        _db = db;
        _supabaseDb = supabaseDb;
        _localDb = localDb;
        _mongoClient = mongoClient;
        _logger = logger;

        var connStr = configuration.GetConnectionString("MongoDb");
        if (!string.IsNullOrWhiteSpace(connStr))
        {
            var url = new MongoUrl(connStr);
            _databaseName = !string.IsNullOrWhiteSpace(url.DatabaseName) ? url.DatabaseName : "InvestProMax";
        }
        else
        {
            _databaseName = "InvestProMax";
        }
    }

    public async Task<StatusMongoResponse> ObterStatusAsync(CancellationToken ct = default)
    {
        if (_mongoClient is null)
        {
            return new StatusMongoResponse(
                Conectado: false,
                Servidor: "Não configurado",
                Banco: _databaseName,
                Colecoes: [],
                TotalDocumentos: 0);
        }

        try
        {
            var mongoDb = _mongoClient.GetDatabase(_databaseName);
            var ping = await mongoDb.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
            var conectado = ping.Contains("ok") && ping["ok"].ToDouble() == 1.0;

            var serverDesc = _mongoClient.Settings?.Server?.Host ?? "marquesfc.qjuhho6.mongodb.net";

            var colecoes = new List<BackupMongoTabelaItem>();
            var colNames = await (await mongoDb.ListCollectionNamesAsync(cancellationToken: ct)).ToListAsync(ct);

            int totalGeral = 0;
            foreach (var colName in colNames.Where(c => c.StartsWith("IPM_", StringComparison.OrdinalIgnoreCase)))
            {
                var col = mongoDb.GetCollection<BsonDocument>(colName);
                var qtd = (int)await col.CountDocumentsAsync(new BsonDocument(), cancellationToken: ct);
                var nomeTabela = colName["IPM_".Length..];
                colecoes.Add(new BackupMongoTabelaItem(nomeTabela, colName, qtd));
                totalGeral += qtd;
            }

            return new StatusMongoResponse(
                Conectado: conectado,
                Servidor: serverDesc,
                Banco: _databaseName,
                Colecoes: colecoes.OrderBy(c => c.NomeColecao).ToList(),
                TotalDocumentos: totalGeral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter status do MongoDB Atlas.");
            return new StatusMongoResponse(
                Conectado: false,
                Servidor: ex.Message,
                Banco: _databaseName,
                Colecoes: [],
                TotalDocumentos: 0);
        }
    }

    public async Task<BackupMongoResponse> ExecutarBackupAsync(CancellationToken ct = default)
    {
        if (_mongoClient is null)
        {
            return new BackupMongoResponse(
                Sucesso: false,
                Mensagem: "MongoDB não configurado ou cliente indisponível.",
                DataHoraUtc: DateTime.UtcNow,
                BancoOrigem: _db.Database.ProviderName ?? "Desconhecido",
                BancoDestino: _databaseName,
                Tabelas: [],
                TotalGeralRegistros: 0);
        }

        var mongoDb = _mongoClient.GetDatabase(_databaseName);
        var itensTabelas = new List<BackupMongoTabelaItem>();
        int totalGeral = 0;

        async Task BackupTabelaAsync<T>(IQueryable<T> query, string nomeTabela) where T : class
        {
            var nomeColecao = $"IPM_{nomeTabela}";
            var col = mongoDb.GetCollection<BsonDocument>(nomeColecao);
            var registros = await query.AsNoTracking().ToListAsync(ct);

            if (registros.Count > 0)
            {
                var docs = new List<WriteModel<BsonDocument>>();
                foreach (var item in registros)
                {
                    var json = JsonSerializer.Serialize(item, JsonOpts);
                    var doc = BsonDocument.Parse(json);
                    if (doc.Contains("Id"))
                    {
                        doc["_id"] = doc["Id"].ToString();
                        var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
                        docs.Add(new ReplaceOneModel<BsonDocument>(filter, doc) { IsUpsert = true });
                    }
                    else
                    {
                        docs.Add(new InsertOneModel<BsonDocument>(doc));
                    }
                }

                if (docs.Count > 0)
                {
                    await col.BulkWriteAsync(docs, cancellationToken: ct);
                }
            }

            itensTabelas.Add(new BackupMongoTabelaItem(nomeTabela, nomeColecao, registros.Count));
            totalGeral += registros.Count;
        }

        try
        {
            await BackupTabelaAsync(_db.Usuarios, "Usuarios");
            await BackupTabelaAsync(_db.Papeis, "Papeis");
            await BackupTabelaAsync(_db.Transacoes, "Transacoes");
            await BackupTabelaAsync(_db.Dividendos, "Dividendos");
            await BackupTabelaAsync(_db.MovimentosAluguel, "MovimentosAluguel");
            await BackupTabelaAsync(_db.Segmentos, "Segmentos");
            await BackupTabelaAsync(_db.Ativos, "Ativos");
            await BackupTabelaAsync(_db.Inventarios, "Inventarios");
            await BackupTabelaAsync(_db.Contas, "Contas");
            await BackupTabelaAsync(_db.Faturas, "Faturas");
            await BackupTabelaAsync(_db.Pagamentos, "Pagamentos");

            // Registra metadados do backup na collection IPM_HistoricoBackup
            var colHistorico = mongoDb.GetCollection<BsonDocument>("IPM_HistoricoBackup");
            var historicoDoc = new BsonDocument
            {
                { "_id", Guid.NewGuid().ToString() },
                { "dataHoraUtc", DateTime.UtcNow },
                { "bancoOrigem", _db.Database.ProviderName ?? "Supabase" },
                { "totalGeralRegistros", totalGeral },
                { "detalhesTabelas", new BsonArray(itensTabelas.Select(t => new BsonDocument
                    {
                        { "tabela", t.NomeTabela },
                        { "colecao", t.NomeColecao },
                        { "total", t.TotalRegistros }
                    }))
                }
            };
            await colHistorico.InsertOneAsync(historicoDoc, cancellationToken: ct);

            return new BackupMongoResponse(
                Sucesso: true,
                Mensagem: $"Backup no MongoDB Atlas executado com sucesso! {totalGeral} registros consolidados em {itensTabelas.Count} coleções.",
                DataHoraUtc: DateTime.UtcNow,
                BancoOrigem: _db.Database.ProviderName ?? "Supabase",
                BancoDestino: _databaseName,
                Tabelas: itensTabelas,
                TotalGeralRegistros: totalGeral);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao executar backup para o MongoDB Atlas.");
            return new BackupMongoResponse(
                Sucesso: false,
                Mensagem: $"Erro ao executar backup para MongoDB: {ex.Message}",
                DataHoraUtc: DateTime.UtcNow,
                BancoOrigem: _db.Database.ProviderName ?? "Supabase",
                BancoDestino: _databaseName,
                Tabelas: itensTabelas,
                TotalGeralRegistros: totalGeral);
        }
    }

    public async Task RegistrarBackupEventoPapelAsync(
        IReadOnlyList<BackupTransacaoEventoItem> transacoes,
        IReadOnlyList<BackupDividendoEventoItem> dividendos,
        CancellationToken ct = default)
    {
        if (_mongoClient is null)
        {
            _logger.LogWarning("MongoDB não configurado: backup de evento corporativo (IPM_BKP_Transacoes / IPM_BKP_Dividendos) ignorado.");
            return;
        }

        try
        {
            var mongoDb = _mongoClient.GetDatabase(_databaseName);
            var agoraUtc = DateTime.UtcNow;

            if (transacoes.Count > 0)
            {
                var colTransacoes = mongoDb.GetCollection<BsonDocument>("IPM_BKP_Transacoes");
                var docsTransacoes = transacoes.Select(t => new BsonDocument
                {
                    { "_id", Guid.NewGuid().ToString() },
                    { "TransacaoId", t.TransacaoId.ToString() },
                    { "PapelAnteriorId", t.PapelAnteriorId.ToString() },
                    { "PapelAtualId", t.PapelAtualId.ToString() },
                    { "CodigoAnterior", t.CodigoAnterior },
                    { "CodigoAtual", t.CodigoAtual },
                    { "DataAcao", t.DataAcao.ToString("yyyy-MM-dd") },
                    { "DataHoraAcaoUtc", agoraUtc },
                    { "TipoAcao", t.TipoAcao },
                    { "FatorMultiplicador", t.FatorMultiplicador.HasValue ? (BsonValue)new BsonDecimal128(t.FatorMultiplicador.Value) : BsonNull.Value },
                    { "DataTransacao", t.DataTransacao.ToString("yyyy-MM-dd") },
                    { "TipoTransacao", t.TipoTransacao },
                    { "QuantidadeAnterior", new BsonDecimal128(t.QuantidadeAnterior) },
                    { "QuantidadeAtual", new BsonDecimal128(t.QuantidadeAtual) },
                    { "ValorAnterior", new BsonDecimal128(t.ValorAnterior) },
                    { "ValorAtual", new BsonDecimal128(t.ValorAtual) },
                    { "Descricao", t.Descricao is not null ? (BsonValue)t.Descricao : BsonNull.Value }
                }).ToList();

                await colTransacoes.InsertManyAsync(docsTransacoes, cancellationToken: ct);
            }

            if (dividendos.Count > 0)
            {
                var colDividendos = mongoDb.GetCollection<BsonDocument>("IPM_BKP_Dividendos");
                var docsDividendos = dividendos.Select(d => new BsonDocument
                {
                    { "_id", Guid.NewGuid().ToString() },
                    { "DividendoId", d.DividendoId.ToString() },
                    { "PapelAnteriorId", d.PapelAnteriorId.ToString() },
                    { "PapelAtualId", d.PapelAtualId.ToString() },
                    { "CodigoAnterior", d.CodigoAnterior },
                    { "CodigoAtual", d.CodigoAtual },
                    { "DataAcao", d.DataAcao.ToString("yyyy-MM-dd") },
                    { "DataHoraAcaoUtc", agoraUtc },
                    { "TipoAcao", d.TipoAcao },
                    { "FatorMultiplicador", d.FatorMultiplicador.HasValue ? (BsonValue)new BsonDecimal128(d.FatorMultiplicador.Value) : BsonNull.Value },
                    { "DataDividendo", d.DataDividendo.ToString("yyyy-MM-dd") },
                    { "TipoDividendo", d.TipoDividendo },
                    { "QuantidadeAnterior", new BsonDecimal128(d.QuantidadeAnterior) },
                    { "QuantidadeAtual", new BsonDecimal128(d.QuantidadeAtual) },
                    { "ValorAnterior", new BsonDecimal128(d.ValorAnterior) },
                    { "ValorAtual", new BsonDecimal128(d.ValorAtual) },
                    { "Descricao", d.Descricao is not null ? (BsonValue)d.Descricao : BsonNull.Value }
                }).ToList();

                await colDividendos.InsertManyAsync(docsDividendos, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar backup de evento corporativo nas coleções IPM_BKP_Transacoes / IPM_BKP_Dividendos.");
        }
    }

    public async Task ZerarCarteiraDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        if (_mongoClient is null)
        {
            return;
        }

        var mongoDb = _mongoClient.GetDatabase(_databaseName);
        var papelGuids = await ColetarGuidsRelacionaisAsync(
            ctx => ctx.Papeis.Where(p => p.UsuarioId == usuarioId).Select(p => p.Id).ToListAsync(ct));

        var papelIdStrings = CriarConjuntoIds(papelGuids);
        await ColetarIdsMongoAsync(
            mongoDb,
            "IPM_Papeis",
            FiltroGuidIgual("UsuarioId", usuarioId),
            papelGuids,
            papelIdStrings,
            ct);

        if (papelIdStrings.Count > 0)
        {
            await DeletarPorCampoEmIdsAsync(mongoDb, "IPM_Transacoes", "PapelId", papelIdStrings, ct);
            await DeletarPorCampoEmIdsAsync(mongoDb, "IPM_Dividendos", "PapelId", papelIdStrings, ct);
            await DeletarPorCampoEmIdsAsync(mongoDb, "IPM_MovimentosAluguel", "PapelId", papelIdStrings, ct);
            await DeletarPorCampoEmIdsAsync(mongoDb, "IPM_PapelSegmentos", "PapelId", papelIdStrings, ct);

            var filtroBkp = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.In("PapelAnteriorId", papelIdStrings),
                Builders<BsonDocument>.Filter.In("PapelAtualId", papelIdStrings),
                FiltroGuidIgual("UsuarioId", usuarioId));

            await mongoDb.GetCollection<BsonDocument>("IPM_BKP_Transacoes").DeleteManyAsync(filtroBkp, ct);
            await mongoDb.GetCollection<BsonDocument>("IPM_BKP_Dividendos").DeleteManyAsync(filtroBkp, ct);
        }
        else
        {
            var filtroUsuario = FiltroGuidIgual("UsuarioId", usuarioId);
            await mongoDb.GetCollection<BsonDocument>("IPM_BKP_Transacoes").DeleteManyAsync(filtroUsuario, ct);
            await mongoDb.GetCollection<BsonDocument>("IPM_BKP_Dividendos").DeleteManyAsync(filtroUsuario, ct);
        }

        var filtroPapeis = papelIdStrings.Count > 0
            ? Builders<BsonDocument>.Filter.Or(
                FiltroGuidIgual("UsuarioId", usuarioId),
                Builders<BsonDocument>.Filter.In("_id", papelIdStrings))
            : FiltroGuidIgual("UsuarioId", usuarioId);

        await mongoDb.GetCollection<BsonDocument>("IPM_Papeis").DeleteManyAsync(filtroPapeis, ct);
    }

    public async Task ZerarBaseDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        if (_mongoClient is null)
        {
            return;
        }

        await ZerarCarteiraDoUsuarioAsync(usuarioId, ct);

        var mongoDb = _mongoClient.GetDatabase(_databaseName);

        // Patrimônio: Ativos e Inventários do usuário
        var ativoGuids = await ColetarGuidsRelacionaisAsync(
            ctx => ctx.Ativos.Where(a => a.UsuarioId == usuarioId).Select(a => a.Id).ToListAsync(ct));

        var ativoIdStrings = CriarConjuntoIds(ativoGuids);
        await ColetarIdsMongoAsync(
            mongoDb,
            "IPM_Ativos",
            FiltroGuidIgual("UsuarioId", usuarioId),
            ativoGuids,
            ativoIdStrings,
            ct);

        if (ativoIdStrings.Count > 0)
        {
            await DeletarPorCampoEmIdsAsync(mongoDb, "IPM_Inventarios", "AtivoId", ativoIdStrings, ct);
        }

        var filtroAtivos = ativoIdStrings.Count > 0
            ? Builders<BsonDocument>.Filter.Or(
                FiltroGuidIgual("UsuarioId", usuarioId),
                Builders<BsonDocument>.Filter.In("_id", ativoIdStrings))
            : FiltroGuidIgual("UsuarioId", usuarioId);

        await mongoDb.GetCollection<BsonDocument>("IPM_Ativos").DeleteManyAsync(filtroAtivos, ct);

        // Financeiro: Contas, Faturas e Pagamentos do usuário
        var contaGuids = await ColetarGuidsRelacionaisAsync(
            ctx => ctx.Contas.Where(c => c.UsuarioId == usuarioId).Select(c => c.Id).ToListAsync(ct));

        var contaIdStrings = CriarConjuntoIds(contaGuids);
        await ColetarIdsMongoAsync(
            mongoDb,
            "IPM_Contas",
            FiltroGuidIgual("UsuarioId", usuarioId),
            contaGuids,
            contaIdStrings,
            ct);

        var faturaGuids = await ColetarGuidsRelacionaisAsync(
            ctx => ctx.Faturas
                .Where(f => contaGuids.Contains(f.ContaId) || (f.Conta != null && f.Conta.UsuarioId == usuarioId))
                .Select(f => f.Id)
                .ToListAsync(ct));

        var faturaIdStrings = CriarConjuntoIds(faturaGuids);
        if (contaIdStrings.Count > 0)
        {
            await ColetarIdsMongoAsync(
                mongoDb,
                "IPM_Faturas",
                Builders<BsonDocument>.Filter.In("ContaId", contaIdStrings),
                faturaGuids,
                faturaIdStrings,
                ct);
        }

        if (faturaIdStrings.Count > 0)
        {
            await DeletarPorCampoEmIdsAsync(mongoDb, "IPM_Pagamentos", "FaturaId", faturaIdStrings, ct);
        }

        if (contaIdStrings.Count > 0 && faturaIdStrings.Count > 0)
        {
            var filtroFaturas = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.In("ContaId", contaIdStrings),
                Builders<BsonDocument>.Filter.In("_id", faturaIdStrings));
            await mongoDb.GetCollection<BsonDocument>("IPM_Faturas").DeleteManyAsync(filtroFaturas, ct);
        }
        else if (contaIdStrings.Count > 0)
        {
            await DeletarPorCampoEmIdsAsync(mongoDb, "IPM_Faturas", "ContaId", contaIdStrings, ct);
        }
        else if (faturaIdStrings.Count > 0)
        {
            await DeletarPorCampoEmIdsAsync(mongoDb, "IPM_Faturas", "_id", faturaIdStrings, ct);
        }

        var filtroContas = contaIdStrings.Count > 0
            ? Builders<BsonDocument>.Filter.Or(
                FiltroGuidIgual("UsuarioId", usuarioId),
                Builders<BsonDocument>.Filter.In("_id", contaIdStrings))
            : FiltroGuidIgual("UsuarioId", usuarioId);

        await mongoDb.GetCollection<BsonDocument>("IPM_Contas").DeleteManyAsync(filtroContas, ct);
    }

    private IEnumerable<InvestProMaxDbContext> ObterContextosDisponiveis()
    {
        var contextos = new List<InvestProMaxDbContext> { _db };
        if (_supabaseDb is not null && !contextos.Any(c => ReferenceEquals(c, _supabaseDb)))
        {
            contextos.Add(_supabaseDb);
        }
        if (_localDb is not null && !contextos.Any(c => ReferenceEquals(c, _localDb)))
        {
            contextos.Add(_localDb);
        }
        return contextos;
    }

    private async Task<HashSet<Guid>> ColetarGuidsRelacionaisAsync(
        Func<InvestProMaxDbContext, Task<List<Guid>>> consulta)
    {
        var guids = new HashSet<Guid>();
        foreach (var ctx in ObterContextosDisponiveis())
        {
            if (ReferenceEquals(ctx, _db))
            {
                var ids = await consulta(ctx);
                foreach (var id in ids)
                {
                    guids.Add(id);
                }
            }
            else
            {
                try
                {
                    var ids = await consulta(ctx);
                    foreach (var id in ids)
                    {
                        guids.Add(id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao consultar IDs no contexto secundário {Contexto} durante limpeza por usuário no MongoDB.", ctx.GetType().Name);
                }
            }
        }
        return guids;
    }

    private static async Task ColetarIdsMongoAsync(
        IMongoDatabase mongoDb,
        string nomeColecao,
        FilterDefinition<BsonDocument> filtro,
        HashSet<Guid> guids,
        HashSet<string> idStrings,
        CancellationToken ct)
    {
        var col = mongoDb.GetCollection<BsonDocument>(nomeColecao);
        var projecao = Builders<BsonDocument>.Projection.Include("_id").Include("Id");
        var docs = await col.Find(filtro).Project(projecao).ToListAsync(ct);

        foreach (var doc in docs)
        {
            AdicionarValorId(doc, "_id", guids, idStrings);
            AdicionarValorId(doc, "Id", guids, idStrings);
        }
    }

    private static void AdicionarValorId(
        BsonDocument doc,
        string campo,
        HashSet<Guid> guids,
        HashSet<string> idStrings)
    {
        if (!doc.TryGetValue(campo, out var valor) || valor is null || valor.IsBsonNull)
        {
            return;
        }

        if (valor.IsString)
        {
            var texto = valor.AsString;
            if (!string.IsNullOrWhiteSpace(texto))
            {
                idStrings.Add(texto);
                if (Guid.TryParse(texto, out var g))
                {
                    AdicionarGuid(g, guids, idStrings);
                }
            }
        }
        else if (valor.IsGuid)
        {
            AdicionarGuid(valor.AsGuid, guids, idStrings);
        }
    }

    private static HashSet<string> CriarConjuntoIds(IEnumerable<Guid> guids)
    {
        var conjunto = new HashSet<string>(StringComparer.Ordinal);
        foreach (var g in guids)
        {
            conjunto.Add(g.ToString("D").ToLowerInvariant());
            conjunto.Add(g.ToString("D").ToUpperInvariant());
        }
        return conjunto;
    }

    private static void AdicionarGuid(Guid id, HashSet<Guid> guids, HashSet<string> idStrings)
    {
        guids.Add(id);
        idStrings.Add(id.ToString("D").ToLowerInvariant());
        idStrings.Add(id.ToString("D").ToUpperInvariant());
    }

    private static FilterDefinition<BsonDocument> FiltroGuidIgual(string campo, Guid id) =>
        Builders<BsonDocument>.Filter.In(
            campo,
            new[] { id.ToString("D").ToLowerInvariant(), id.ToString("D").ToUpperInvariant() });

    private static Task DeletarPorCampoEmIdsAsync(
        IMongoDatabase mongoDb,
        string nomeColecao,
        string campo,
        IEnumerable<string> ids,
        CancellationToken ct)
    {
        var filtro = Builders<BsonDocument>.Filter.In(campo, ids);
        return mongoDb.GetCollection<BsonDocument>(nomeColecao).DeleteManyAsync(filtro, ct);
    }
}

