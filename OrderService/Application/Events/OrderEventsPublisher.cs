using Shared.Application.Messaging;
using Shared.Contracts.Messaging;

namespace OrderService.Application.Events
{
    public class OrderEventsPublisher
    {
        private readonly IEventPublisher _publisher;

        public OrderEventsPublisher(IEventPublisher publisher)
        {
            _publisher = publisher;
        }

        public async Task PublishOrderCreated(Guid orderId, string customerEmail, decimal totalPrice, List<Shared.Contracts.Order.OrderItemDto> items)
        {
            var orderCreatedEvent = new OrderCreatedEvent
            {
                OrderId = orderId,
                CustomerEmail = customerEmail,
                TotalPrice = totalPrice,
                Items = items,
                CreatedAt = DateTime.UtcNow
            };

            await _publisher.PublishAsync(orderCreatedEvent);
        }
    }
}
