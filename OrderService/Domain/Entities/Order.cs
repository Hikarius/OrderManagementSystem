using Shared.Infrastructure.Data;
using static Shared.Contracts.Order.Enums;

namespace OrderService.Domain.Entities
{
    public class Order : AggregateRoot
    {
        public required string CustomerEmail { get; set; }
        public string? CustomerTelNumber { get; set; }
        public decimal TotalPrice => Items.Sum(i => i.TotalPrice);
        public OrderStatus Status { get; set; }
        public List<OrderItem> Items { get; set; } = [];

    }
}
