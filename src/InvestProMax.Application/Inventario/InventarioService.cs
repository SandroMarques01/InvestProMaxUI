using InvestProMax.Application.Ativos;
using InvestProMax.Application.Common;
using InvestProMax.Application.Common.Exceptions;
using InvestProMax.Application.Common.Interfaces;
using InvestProMax.Application.Papeis;
using InvestProMax.Domain.Enums;
using InventarioEntidade = InvestProMax.Domain.Entidades.Inventario;

namespace InvestProMax.Application.Inventario;

public interface IInventarioService
{
    Task<PaginaResultado<InventarioResponse>> ListarAsync(InventarioFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default);
    Task<LancamentoLoteResponse> LancarLoteAsync(LancamentoLoteRequest request, CancellationToken ct = default);
    Task<SugestaoCalculadaResponse> ObterSugestaoCalculadaAsync(int ano, int mes, CancellationToken ct = default);
}

public sealed class InventarioService : IInventarioService
{
    private readonly IInventarioRepository _inventarioRepository;
    private readonly IAtivoRepository _ativoRepository;
    private readonly IPapelRepository _papelRepository;
    private readonly IUsuarioLogado _usuarioLogado;

    public InventarioService(
        IInventarioRepository inventarioRepository,
        IAtivoRepository ativoRepository,
        IPapelRepository papelRepository,
        IUsuarioLogado usuarioLogado)
    {
        _inventarioRepository = inventarioRepository;
        _ativoRepository = ativoRepository;
        _papelRepository = papelRepository;
        _usuarioLogado = usuarioLogado;
    }

    public async Task<PaginaResultado<InventarioResponse>> ListarAsync(
        InventarioFiltro filtro, ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var (itens, total) = await _inventarioRepository.ListarAsync(
            _usuarioLogado.UsuarioId, filtro, paginacao.Skip, paginacao.TamanhoEfetivo, ct);

        return new PaginaResultado<InventarioResponse>(itens.Select(ParaResponse).ToList(), total, paginacao.PaginaEfetiva, paginacao.TamanhoEfetivo);
    }

    /// <summary>
    /// Lançamento mensal em lote (seção 8): upsert por (AtivoId, Ano, Mes) — reenviar o lote do
    /// mesmo mês corrige os valores em vez de gerar duplicata, o que é o comportamento
    /// esperado de um formulário "lançamento do mês" que o usuário pode reabrir e reeditar.
    /// </summary>
    public async Task<LancamentoLoteResponse> LancarLoteAsync(LancamentoLoteRequest request, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;
        var data = new DateOnly(request.Ano, request.Mes, 1);
        var criados = 0;
        var atualizados = 0;

        foreach (var item in request.Itens)
        {
            var ativo = await _ativoRepository.ObterPorIdAsync(item.AtivoId, usuarioId, ct)
                ?? throw new NaoEncontradoException($"Ativo '{item.AtivoId}' não encontrado.");

            var valorArredondado = Math.Round(item.Valor, 2, MidpointRounding.AwayFromZero);
            var existente = await _inventarioRepository.ObterPorAtivoEMesAsync(ativo.Id, request.Ano, request.Mes, ct);
            if (existente is null)
            {
                await _inventarioRepository.AdicionarAsync(new InventarioEntidade
                {
                    AtivoId = ativo.Id, Valor = valorArredondado, Data = data, Descricao = item.Descricao, StatusAtivo = true
                }, ct);
                criados++;
            }
            else
            {
                existente.Valor = valorArredondado;
                existente.Descricao = item.Descricao;
                await _inventarioRepository.AtualizarAsync(existente, ct);
                atualizados++;
            }
        }

        return new LancamentoLoteResponse(criados, atualizados);
    }

    /// <summary>
    /// Regra 6. ASSUNÇÃO SINALIZADA: Ano/Mes aqui só identificam para qual competência a
    /// sugestão está sendo preparada (ecoados na resposta) — o cálculo em si sempre usa a
    /// posição e a CotacaoAtual *correntes* dos papéis, porque o sistema não guarda cotação
    /// histórica por mês. Ou seja, pedir a sugestão para um mês passado dá o mesmo valor que
    /// pedir para o mês corrente.
    /// </summary>
    public async Task<SugestaoCalculadaResponse> ObterSugestaoCalculadaAsync(int ano, int mes, CancellationToken ct = default)
    {
        var usuarioId = _usuarioLogado.UsuarioId;
        var ativosCalculados = await _ativoRepository.ListarPorOrigemAsync(usuarioId, TipoOrigemAtivo.Calculado, ct);

        var itens = new List<ItemSugestaoCalculada>();
        foreach (var ativo in ativosCalculados)
        {
            if (ativo.TipoPapelVinculado is null)
            {
                continue; // dado inconsistente (não deveria acontecer — validado na criação/edição do Ativo)
            }

            var papeis = await _papelRepository.ListarPorTipoAsync(usuarioId, ativo.TipoPapelVinculado.Value, ct);
            var posicoes = papeis.Select(p => (Papel: p, Posicao: CalculadoraPosicao.Calcular(p.Transacoes))).ToList();
            var valorSugerido = Math.Round(posicoes.Sum(x => x.Posicao.QuantidadeAtual * x.Papel.CotacaoAtual), 2, MidpointRounding.AwayFromZero);
            // Aluguel não é venda: a quantidade total já inclui o que está emprestado.
            var quantidadeTotal = posicoes.Sum(x => x.Posicao.QuantidadeAtual);
            var quantidadeEmAluguel = posicoes.Sum(x => CalculadoraAluguel.QuantidadeEmAluguel(x.Papel, x.Posicao.QuantidadeAtual));
            itens.Add(new ItemSugestaoCalculada(ativo.Id, ativo.Nome, valorSugerido, quantidadeTotal, quantidadeEmAluguel));
        }

        return new SugestaoCalculadaResponse(ano, mes, itens);
    }

    private static InventarioResponse ParaResponse(InventarioEntidade i) => new(i.Id, i.AtivoId, i.Valor, i.Data, i.Descricao, i.StatusAtivo);
}
