using FluentValidation;
using InvestProMax.Domain.Enums;

namespace InvestProMax.Application.Ativos;

/// <summary>
/// ASSUNÇÃO SINALIZADA: a especificação não decide o que fazer se TipoOrigem = Calculado sem
/// TipoPapelVinculado (a regra 6 depende diretamente desse campo para funcionar) — validado
/// aqui como erro. Da mesma forma, TipoPapelVinculado preenchido com TipoOrigem = Manual não
/// faz sentido (o campo só existe para o caso Calculado) e também é rejeitado.
/// </summary>
public sealed class CriarAtivoRequestValidator : AbstractValidator<CriarAtivoRequest>
{
    public CriarAtivoRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TipoAtivo).IsInEnum();
        RuleFor(x => x.TipoOrigem).IsInEnum();
        RuleFor(x => x.Descricao).MaximumLength(1000);
        RuleFor(x => x.TipoPapelVinculado)
            .NotNull().WithMessage("Ativos com origem Calculado precisam de TipoPapelVinculado.")
            .When(x => x.TipoOrigem == TipoOrigemAtivo.Calculado);
        RuleFor(x => x.TipoPapelVinculado)
            .Null().WithMessage("TipoPapelVinculado só se aplica a ativos com origem Calculado.")
            .When(x => x.TipoOrigem == TipoOrigemAtivo.Manual);
    }
}

public sealed class AtualizarAtivoRequestValidator : AbstractValidator<AtualizarAtivoRequest>
{
    public AtualizarAtivoRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TipoAtivo).IsInEnum();
        RuleFor(x => x.TipoOrigem).IsInEnum();
        RuleFor(x => x.Descricao).MaximumLength(1000);
        RuleFor(x => x.TipoPapelVinculado)
            .NotNull().WithMessage("Ativos com origem Calculado precisam de TipoPapelVinculado.")
            .When(x => x.TipoOrigem == TipoOrigemAtivo.Calculado);
        RuleFor(x => x.TipoPapelVinculado)
            .Null().WithMessage("TipoPapelVinculado só se aplica a ativos com origem Calculado.")
            .When(x => x.TipoOrigem == TipoOrigemAtivo.Manual);
    }
}
