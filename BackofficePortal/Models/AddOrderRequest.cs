namespace BackofficePortal.Models
{
    public class AddOrderRequest
    {
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerTelNumber { get; set; }
        public List<AddOrderItem> Items { get; set; } = new();
        public string? IdempotencyKey { get; set; }
    }

    public class AddOrderItem
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
