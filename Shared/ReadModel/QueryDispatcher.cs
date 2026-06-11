using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.ReadModel
{
    public class QueryDispatcher
    {
        private readonly IServiceProvider _provider;

        public QueryDispatcher(IServiceProvider provider)
        {
            _provider = provider;
        }

        public Task<TResult> Dispatch<TQuery, TResult>(TQuery query)
            where TQuery : IQuery<TResult>
        {
            var handler = _provider.GetService<IQueryHandler<TQuery, TResult>>();
            if (handler == null) throw new InvalidOperationException($"Handler for {typeof(TQuery).FullName} not registered");
            return handler.Handle(query);
        }
    }
}
