using InvestProMax.Domain.Common;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Domain.Entidades;

public class Papel : EntidadeBase
{
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public TipoPapel TipoPapel { get; set; }
    public decimal CotacaoAtual { get; set; }
    public DateTime? CotacaoAtualizadaEm { get; set; }
    public string? FonteCotacao { get; set; }
    public string? Descricao { get; set; }

    /// <summary>
    /// Vira false automaticamente quando a posição líquida chega a zero (regra de negócio 2).
    /// A regra em si vive na camada Application, não aqui.
    /// </summary>
    public bool Ativo { get; set; } = true;

    public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();
    public ICollection<Dividendo> Dividendos { get; set; } = new List<Dividendo>();
    public ICollection<MovimentoAluguel> MovimentosAluguel { get; set; } = new List<MovimentoAluguel>();
    public ICollection<PapelSegmento> PapelSegmentos { get; set; } = new List<PapelSegmento>();
}
