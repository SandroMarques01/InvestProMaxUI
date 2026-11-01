using FluentValidation;

namespace InvestProMax.Application.Segmentos;

public sealed class CriarSegmentoRequestValidator : AbstractValidator<CriarSegmentoRequest>
{
    public CriarSegmentoRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}

public sealed class AtualizarSegmentoRequestValidator : AbstractValidator<AtualizarSegmentoRequest>
{
    public AtualizarSegmentoRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}
