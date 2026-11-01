using FluentValidation;

namespace InvestProMax.Application.Papeis;

public sealed class CriarPapelRequestValidator : AbstractValidator<CriarPapelRequest>
{
    public CriarPapelRequestValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TipoPapel).IsInEnum();
        RuleFor(x => x.CotacaoAtual).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}

public sealed class AtualizarPapelRequestValidator : AbstractValidator<AtualizarPapelRequest>
{
    public AtualizarPapelRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TipoPapel).IsInEnum();
        RuleFor(x => x.CotacaoAtual).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Descricao).MaximumLength(1000);
    }
}

public sealed class TrocarPapelRequestValidator : AbstractValidator<TrocarPapelRequest>
{
    public TrocarPapelRequestValidator()
    {
        RuleFor(x => x.PapelDestinoId).NotEmpty();
    }
}

public sealed class DesdobramentoRequestValidator : AbstractValidator<DesdobramentoRequest>
{
    public DesdobramentoRequestValidator()
    {
        RuleFor(x => x.Fator).GreaterThan(1)
            .WithMessage("O fator deve ser maior que 1 (ex.: desdobro 1:2 → 2, grupamento 10:1 → 10).");
        RuleFor(x => x.Tipo).IsInEnum();
    }
}
