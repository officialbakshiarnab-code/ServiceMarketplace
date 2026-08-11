using FluentValidation;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Validators;

public class CreateBidDtoValidator : AbstractValidator<CreateBidDto>
{
    public CreateBidDtoValidator()
    {
        RuleFor(x => x.ServiceRequestId)
            .NotEmpty();

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.ProposedDateTime)
            .GreaterThan(DateTime.UtcNow.AddMinutes(-1));

        RuleFor(x => x.Message)
            .MaximumLength(500);

        RuleFor(x => x.EstimatedDurationMinutes)
            .InclusiveBetween(1, 10080)
            .When(x => x.EstimatedDurationMinutes.HasValue);
    }
}
