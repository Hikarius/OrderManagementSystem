namespace Shared.Contracts.Catalog.Dtos
{
    public class DecreaseStockRequest
    {
        public Guid ProductId { get; set; }
        public int Amount { get; set; }
    }
}
