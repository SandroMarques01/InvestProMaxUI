using FluentValidation;

namespace InvestProMax.Application.Inventario;

public sealed class LancamentoLoteRequestValidator : AbstractValidator<LancamentoLoteRequest>
{
    public LancamentoLoteRequestValidator()
    {
        RuleFor(x => x.Ano).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Mes).InclusiveBetween(1, 12);
        RuleFor(x => x.Itens).NotEmpty();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.AtivoId).NotEmpty();
            item.RuleFor(i => i.Valor).GreaterThanOrEqualTo(0);
        });
    }
}
