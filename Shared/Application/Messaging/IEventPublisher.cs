using Shared.Contracts;

namespace Shared.Application.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default) where TMessage : class, IMessage;
    }
}
