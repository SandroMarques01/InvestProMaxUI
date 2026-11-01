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
        IMongoClient? mongoClient = null)
    {
        _db = db;
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
}

