using Shared.Contracts;
using Shared.Contracts.Order;

namespace Shared.Contracts.Messaging
{
    public class OrderCreatedEvent : IMessage
    {
        public Guid OrderId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
        public DateTime CreatedAt { get; set; }
    }
}
