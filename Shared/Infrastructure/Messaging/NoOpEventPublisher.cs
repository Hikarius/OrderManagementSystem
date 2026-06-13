using System.Threading;
using System.Threading.Tasks;
using Shared.Application.Messaging;
using Shared.Contracts;

namespace Shared.Infrastructure.Messaging
{
    public class NoOpEventPublisher : IEventPublisher
    {
        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class, IMessage
        {
            // no-op in-memory publisher for development/testing when MassTransit license not available
            return Task.CompletedTask;
        }
    }
}
