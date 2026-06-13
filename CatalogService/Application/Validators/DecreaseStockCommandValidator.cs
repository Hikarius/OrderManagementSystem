using FluentValidation;

namespace CatalogService.Application.Handlers
{
    public class DecreaseStockCommandValidator : AbstractValidator<DecreaseStockCommand>
    {
        public DecreaseStockCommandValidator()
        {
            RuleFor(x => x.Items).NotNull().NotEmpty();
            RuleForEach(x => x.Items).SetValidator(new DecreaseItemStockValidator());
        }
    }

    public class DecreaseItemStockValidator : AbstractValidator<DecreaseItemStock>
    {
        public DecreaseItemStockValidator()
        {
            RuleFor(x => x.ProductId).NotEmpty();
            RuleFor(x => x.Quantity).GreaterThan(0);
        }
    }
}
