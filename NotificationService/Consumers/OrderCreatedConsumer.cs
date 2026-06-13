using MassTransit;
using Shared.Contracts.Messaging;
using NotificationService.Data;
using NotificationService.Domain.Entities;

namespace NotificationService.Consumers
{
    public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
    {
        private readonly DataContext _db;

        public OrderCreatedConsumer(Data.DataContext db)
        {
            _db = db;
        }

        public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
        {
            var message = context.Message;

            var notification = new Notification
            {
                OrderId = message.OrderId,
                Channel = "Email",
                Message = $"Order {message.OrderId} created for {message.CustomerEmail}"
            };

            _db.Add(notification);
            await _db.SaveChangesAsync();
        }
    }
}
