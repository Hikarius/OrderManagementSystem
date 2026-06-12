namespace Shared.Contracts.Catalog.Dtos
{
    public class DecreaseItemDto
    {
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class DecreaseItemsRequest
    {
        public List<DecreaseItemDto> Items { get; set; } = new List<DecreaseItemDto>();
    }
}
