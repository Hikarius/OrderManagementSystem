using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Shared.Application.Messaging;
using Shared.Contracts;

namespace Shared.Infrastructure.Messaging
{
    public class MassTransitEventPublisher : IEventPublisher
    {
        private readonly IPublishEndpoint _publishEndpoint;

        public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
        {
            _publishEndpoint = publishEndpoint;
        }

        public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class, IMessage
        {
            await _publishEndpoint.Publish<TMessage>(message, cancellationToken);
        }
    }
}
