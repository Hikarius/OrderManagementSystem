using MassTransit;
using NotificationService.Data;
using NotificationService.Domain.Entities;
using Shared.Contracts.Messaging;

namespace NotificationService.Consumers
{
    public class OrderCancelledConsumer(DataContext db) : IConsumer<OrderCancelledEvent>
    {
        private readonly DataContext _db = db;

        public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
        {
            var message = context.Message;

            var notification = new Notification
            {
                OrderId = message.OrderId,
                Channel = "Email",
                Message = $"Order {message.OrderId} cancelled for {message.CustomerEmail}"
            };

            _db.Add(notification);
            await _db.SaveChangesAsync();
        }
    }
}
