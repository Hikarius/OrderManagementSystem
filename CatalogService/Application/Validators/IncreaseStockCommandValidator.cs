using FluentValidation;

namespace CatalogService.Application.Handlers
{
    public class IncreaseStockCommandValidator : AbstractValidator<IncreaseStockCommand>
    {
        public IncreaseStockCommandValidator()
        {
            RuleFor(x => x.Items).NotNull().NotEmpty();
            RuleForEach(x => x.Items).SetValidator(new IncreaseItemStockValidator());
        }
    }

    public class IncreaseItemStockValidator : AbstractValidator<IncreaseItemStock>
    {
        public IncreaseItemStockValidator()
        {
            RuleFor(x => x.ProductId).NotEmpty();
            RuleFor(x => x.Quantity).GreaterThan(0);
        }
    }
}
