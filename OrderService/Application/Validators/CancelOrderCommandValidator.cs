using FluentValidation;

namespace OrderService.Application.Handlers
{
    public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
    {
        public CancelOrderCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
        }
    }
}
