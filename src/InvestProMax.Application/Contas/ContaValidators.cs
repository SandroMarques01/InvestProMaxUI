using FluentValidation;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Contas;

/// <summary>
/// ASSUNÇÃO SINALIZADA: mesmo padrão aplicado a Ativo na Fase 4 — NumeroParcelas só faz
/// sentido quando Recorrencia = Parcelada (a especificação diz isso, mas não diz o que fazer
/// se vier preenchido em outra recorrência); tratado como erro de validação nos dois sentidos.
/// </summary>
public sealed class CriarContaRequestValidator : AbstractValidator<CriarContaRequest>
{
    public CriarContaRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ValorEstimado).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiaVencimento).InclusiveBetween(1, 31);
        RuleFor(x => x.Recorrencia).IsInEnum();
        RuleFor(x => x.Categoria).MaximumLength(100);
        RuleFor(x => x.Descricao).MaximumLength(1000);
        RuleFor(x => x.NumeroParcelas)
            .NotNull().GreaterThan(0).WithMessage("Informe NumeroParcelas quando Recorrencia = Parcelada.")
            .When(x => x.Recorrencia == Recorrencia.Parcelada);
        RuleFor(x => x.NumeroParcelas)
            .Null().WithMessage("NumeroParcelas só se aplica quando Recorrencia = Parcelada.")
            .When(x => x.Recorrencia != Recorrencia.Parcelada);
    }
}

public sealed class AtualizarContaRequestValidator : AbstractValidator<AtualizarContaRequest>
{
    public AtualizarContaRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ValorEstimado).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiaVencimento).InclusiveBetween(1, 31);
        RuleFor(x => x.Recorrencia).IsInEnum();
        RuleFor(x => x.Categoria).MaximumLength(100);
        RuleFor(x => x.Descricao).MaximumLength(1000);
        RuleFor(x => x.NumeroParcelas)
            .NotNull().GreaterThan(0).WithMessage("Informe NumeroParcelas quando Recorrencia = Parcelada.")
            .When(x => x.Recorrencia == Recorrencia.Parcelada);
        RuleFor(x => x.NumeroParcelas)
            .Null().WithMessage("NumeroParcelas só se aplica quando Recorrencia = Parcelada.")
            .When(x => x.Recorrencia != Recorrencia.Parcelada);
    }
}
