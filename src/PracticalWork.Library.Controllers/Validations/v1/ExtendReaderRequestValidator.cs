using FluentValidation;
using PracticalWork.Library.Contracts.v1.Reader.Request;

namespace PracticalWork.Library.Controllers.Validations.v1;

public class ExtendReaderRequestValidator : AbstractValidator<ExtendReaderRequest>
{
    public ExtendReaderRequestValidator()
    {
        RuleFor(x => x.NewExpiryDate)
            .NotEmpty().WithMessage("Новый срок истечения должен быть указан.")
            .GreaterThan(DateOnly.FromDateTime(DateTime.Now))
            .WithMessage("Новый срок истечения должен быть позднее сегодняшнего дня.");
    }
}