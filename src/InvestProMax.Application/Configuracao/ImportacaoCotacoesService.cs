using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Papeis;

namespace InvestProMax.Application.Configuracao;

public interface IImportacaoCotacoesService
{
    Task<ImportarCotacoesResponse> ImportarAsync(Stream planilha, CancellationToken ct = default);
}

public sealed class ImportacaoCotacoesService : IImportacaoCotacoesService
{
    private readonly ICotacoesReader _reader;
    private readonly IPapelRepository _papelRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public ImportacaoCotacoesService(ICotacoesReader reader, IPapelRepository papelRepository, IUsuarioLogado usuarioLogado)
    {
        _reader = reader;
        _papelRepository = papelRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<ImportarCotacoesResponse> ImportarAsync(Stream planilha, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;
        var linhas = _reader.Ler(planilha);

        var atualizadas = 0;
        var naoEncontrados = new List<string>();

        foreach (var linha in linhas)
        {
            var papel = await _papelRepository.ObterPorCodigoAsync(usuarioId, linha.Codigo, ct);
            if (papel is null)
            {
                naoEncontrados.Add(linha.Codigo);
                continue;
            }

            papel.CotacaoAtual = linha.Cotacao;
            papel.CotacaoAtualizadaEm = DateTime.UtcNow;
            papel.FonteCotacao = "Excel";
            await _papelRepository.AtualizarAsync(papel, ct);
            atualizadas++;
        }

        return new ImportarCotacoesResponse(atualizadas, naoEncontrados);
    }
}
