using FluentValidation;

namespace InvestProMax.Application.Cotacoes;

public sealed class ReceberCotacoesRequestValidator : AbstractValidator<ReceberCotacoesRequest>
{
    public ReceberCotacoesRequestValidator()
    {
        RuleFor(r => r.Fonte)
            .NotEmpty().WithMessage("A fonte das cotações é obrigatória.")
            .MaximumLength(30).WithMessage("A fonte não pode exceder 30 caracteres.");

        RuleFor(r => r)
            .Must(r =>
            {
                var total = (r.Cotacoes?.Count ?? 0) + (r.Falhas?.Count ?? 0);
                return total > 0;
            })
            .WithMessage("O lote não pode ser vazio.")
            .Must(r =>
            {
                var total = (r.Cotacoes?.Count ?? 0) + (r.Falhas?.Count ?? 0);
                return total <= 500;
            })
            .WithMessage("O lote não pode conter mais de 500 itens.");
    }
}
