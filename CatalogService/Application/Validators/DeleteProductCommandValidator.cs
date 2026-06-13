using FluentValidation;

namespace CatalogService.Application.Handlers
{
    public class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
    {
        public DeleteProductCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
        }
    }
}
