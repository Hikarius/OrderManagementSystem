using Shared.Contracts.Order;

namespace Shared.Contracts.Messaging
{
    public  class OrderCancelledEvent : IMessage
    {
        public Guid OrderId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
