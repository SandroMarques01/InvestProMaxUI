using FluentValidation;

namespace InvestProMax.Application.Faturas;

public sealed class PagarFaturaRequestValidator : AbstractValidator<PagarFaturaRequest>
{
    public PagarFaturaRequestValidator()
    {
        RuleFor(x => x.ValorPago).GreaterThan(0);
        RuleFor(x => x.Data).NotEqual(default(DateOnly));
        RuleFor(x => x.FormaPagamento).MaximumLength(50);
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}
