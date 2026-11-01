using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Configuracao;
using InvestProMax.Application.Papeis;

namespace InvestProMax.Application.Cotacoes;

public sealed class AtualizacaoCotacoesService : IAtualizacaoCotacoesService
{
    private readonly IPapelRepository _papelRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public AtualizacaoCotacoesService(IPapelRepository papelRepository, IUsuarioLogado usuarioLogado)
    {
        _papelRepository = papelRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<TickersResponse> ListarTickersAsync(int desatualizadasHaMinutos = 0, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;
        var papeis = await _papelRepository.ListarTodosDoUsuarioAsync(usuarioId, ct);

        var corte = desatualizadasHaMinutos > 0
            ? DateTime.UtcNow.AddMinutes(-desatualizadasHaMinutos)
            : (DateTime?)null;

        var tickers = papeis
            .Where(p =>
            {
                var posicao = CalculadoraPosicao.Calcular(p.Transacoes);
                if (posicao.QuantidadeAtual <= 0)
                {
                    return false;
                }

                if (corte.HasValue && p.CotacaoAtualizadaEm.HasValue && p.CotacaoAtualizadaEm.Value > corte.Value)
                {
                    return false;
                }

                return true;
            })
            .Select(p => p.Codigo)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return new TickersResponse(DateTime.UtcNow, tickers);
    }

    public async Task<ReceberCotacoesResponse> ReceberLoteAsync(ReceberCotacoesRequest request, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;
        var horaRecebimento = DateTime.UtcNow;

        var cotacoesRecebidas = request.Cotacoes ?? [];
        var falhasInformadas = request.Falhas?.Count ?? 0;

        var todosPapeis = await _papelRepository.ListarTodosDoUsuarioAsync(usuarioId, ct);
        var papeisPorCodigo = todosPapeis.ToDictionary(p => p.Codigo, StringComparer.OrdinalIgnoreCase);

        var atualizadas = 0;
        var naoEncontradas = new List<string>();
        var ignoradas = new List<ItemCotacaoIgnorada>();

        foreach (var item in cotacoesRecebidas)
        {
            if (string.IsNullOrWhiteSpace(item.Codigo))
            {
                ignoradas.Add(new ItemCotacaoIgnorada(item.Codigo ?? string.Empty, "código inválido"));
                continue;
            }

            var codigoLimpo = item.Codigo.Trim().ToUpperInvariant();
            var codigoNormalizado = InferenciaPapel.NormalizarCodigo(codigoLimpo);

            if (!item.Cotacao.HasValue || item.Cotacao.Value <= 0)
            {
                ignoradas.Add(new ItemCotacaoIgnorada(codigoNormalizado, "cotação inválida"));
                continue;
            }

            if (!papeisPorCodigo.TryGetValue(codigoNormalizado, out var papel))
            {
                naoEncontradas.Add(codigoNormalizado);
                continue;
            }

            var dataHora = item.DataHoraCotacao ?? horaRecebimento;
            if (papel.CotacaoAtualizadaEm.HasValue && dataHora < papel.CotacaoAtualizadaEm.Value)
            {
                ignoradas.Add(new ItemCotacaoIgnorada(codigoNormalizado, "data da cotação anterior à já gravada"));
                continue;
            }

            papel.CotacaoAtual = item.Cotacao.Value;
            papel.CotacaoAtualizadaEm = dataHora;
            papel.FonteCotacao = request.Fonte;

            await _papelRepository.AtualizarAsync(papel, ct);
            atualizadas++;
        }

        return new ReceberCotacoesResponse(
            Recebidas: cotacoesRecebidas.Count,
            Atualizadas: atualizadas,
            NaoEncontradas: naoEncontradas,
            Ignoradas: ignoradas,
            FalhasInformadas: falhasInformadas,
            AtualizadoEm: horaRecebimento);
    }
}
