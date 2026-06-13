using FluentValidation;
using MediatR;

namespace Shared.Application.MediatR
{
    // Pipeline behavior that runs FluentValidation validators for the incoming request
    // and short-circuits returning a Result if validation fails.
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TResponse : Result.Result, new()
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (_validators != null && _validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);
                var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
                var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

                if (failures.Count > 0)
                {
                    var msg = string.Join("; ", failures.Select(f => f.ErrorMessage));
                    var result = new TResponse();
                    result.IsSuccess = false;
                    result.ErrorMessage = msg;
                    return result;
                }
            }

            return await next();
        }
    }
}
