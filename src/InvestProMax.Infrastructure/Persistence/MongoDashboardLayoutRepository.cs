using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Dashboard;
using MongoDB.Bson;
using MongoDB.Driver;

namespace InvestProMax.Infrastructure.Persistence;

/// <summary>
/// Guarda a ordem dos blocos do Dashboard por usuário na collection IPM_DashboardLayout
/// (um documento por usuário, _id = UsuarioId).
/// </summary>
public sealed class MongoDashboardLayoutRepository : IDashboardLayoutRepository
{
    public const string NomeColecao = "IPM_DashboardLayout";

    private readonly IMongoDatabase? _mongoDb;

    public MongoDashboardLayoutRepository(IMongoDatabase? mongoDb = null) => _mongoDb = mongoDb;

    public async Task<DashboardLayoutDto?> ObterAsync(Guid usuarioId, CancellationToken ct = default)
    {
        // Sem MongoDB configurado não há layout salvo: o serviço devolve o layout padrão.
        if (_mongoDb is null) return null;

        var doc = await Colecao().Find(Filtro(usuarioId)).FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        var kpis = doc.Contains("Kpis") && doc["Kpis"].IsBsonArray
            ? doc["Kpis"].AsBsonArray.Select(v => v.AsString).ToList()
            : DashboardLayoutService.KpisPadrao.ToList();

        var cards = doc.Contains("Cards") && doc["Cards"].IsBsonArray
            ? doc["Cards"].AsBsonArray.Select(v => v.AsString).ToList()
            : DashboardLayoutService.CardsPadrao.ToList();

        var dimensoes = DashboardLayoutService.ObterDimensoesPadrao()
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        if (doc.Contains("Dimensoes") && doc["Dimensoes"].IsBsonDocument)
        {
            foreach (var elem in doc["Dimensoes"].AsBsonDocument.Elements)
            {
                if (!elem.Value.IsBsonDocument) continue;
                var sub = elem.Value.AsBsonDocument;
                var colunas = sub.Contains("Colunas") ? sub["Colunas"].ToInt32() : 6;
                var altura = sub.Contains("Altura") ? sub["Altura"].AsString : "auto";
                dimensoes[elem.Name] = new DashboardItemDimensaoDto(colunas, altura);
            }
        }

        return new DashboardLayoutDto(kpis, cards, dimensoes);
    }

    public async Task SalvarAsync(Guid usuarioId, DashboardLayoutDto layout, CancellationToken ct = default)
    {
        if (_mongoDb is null)
        {
            throw new RegraDeNegocioException("MongoDB não configurado: não é possível salvar o layout do Dashboard.");
        }

        var dimensoesBson = new BsonDocument();
        if (layout.Dimensoes is not null)
        {
            foreach (var (id, dim) in layout.Dimensoes)
            {
                dimensoesBson[id] = new BsonDocument
                {
                    { "Colunas", dim.Colunas },
                    { "Altura", dim.Altura }
                };
            }
        }

        var doc = new BsonDocument
        {
            { "_id", usuarioId.ToString() },
            { "UsuarioId", usuarioId.ToString() },
            { "Kpis", new BsonArray(layout.Kpis) },
            { "Cards", new BsonArray(layout.Cards) },
            { "Dimensoes", dimensoesBson },
            { "AtualizadoEmUtc", DateTime.UtcNow }
        };

        await Colecao().ReplaceOneAsync(Filtro(usuarioId), doc, new ReplaceOptions { IsUpsert = true }, ct);
    }

    private IMongoCollection<BsonDocument> Colecao() => _mongoDb!.GetCollection<BsonDocument>(NomeColecao);

    private static FilterDefinition<BsonDocument> Filtro(Guid usuarioId) =>
        Builders<BsonDocument>.Filter.Eq("_id", usuarioId.ToString());
}

