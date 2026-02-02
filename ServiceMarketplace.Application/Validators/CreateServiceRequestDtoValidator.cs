using FluentValidation;
using ServiceMarketplace.Application.DTOs;

namespace ServiceMarketplace.Application.Validators;

public class CreateServiceRequestDtoValidator : AbstractValidator<CreateServiceRequestDto>
{
    public CreateServiceRequestDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Location)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180);
    }
}
