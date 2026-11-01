using FluentValidation;

namespace InvestProMax.Application.Dividendos;

public sealed class CriarDividendoRequestValidator : AbstractValidator<CriarDividendoRequest>
{
    public CriarDividendoRequestValidator()
    {
        RuleFor(x => x.PapelId).NotEmpty();
        RuleFor(x => x.TipoDividendo).IsInEnum();
        RuleFor(x => x.ValorRecebido).GreaterThan(0);
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.Data).NotEqual(default(DateOnly));
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}

public sealed class AtualizarDividendoRequestValidator : AbstractValidator<AtualizarDividendoRequest>
{
    public AtualizarDividendoRequestValidator()
    {
        RuleFor(x => x.TipoDividendo).IsInEnum();
        RuleFor(x => x.ValorRecebido).GreaterThan(0);
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.Data).NotEqual(default(DateOnly));
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}
