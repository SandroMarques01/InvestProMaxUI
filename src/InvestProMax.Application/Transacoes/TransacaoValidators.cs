using FluentValidation;

namespace InvestProMax.Application.Transacoes;

public sealed class CriarTransacaoRequestValidator : AbstractValidator<CriarTransacaoRequest>
{
    public CriarTransacaoRequestValidator()
    {
        RuleFor(x => x.PapelId).NotEmpty();
        RuleFor(x => x.TipoTransacao).IsInEnum();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.ValorUnitario).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Data).NotEqual(default(DateOnly));
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}

public sealed class AtualizarTransacaoRequestValidator : AbstractValidator<AtualizarTransacaoRequest>
{
    public AtualizarTransacaoRequestValidator()
    {
        RuleFor(x => x.TipoTransacao).IsInEnum();
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.ValorUnitario).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Data).NotEqual(default(DateOnly));
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}
