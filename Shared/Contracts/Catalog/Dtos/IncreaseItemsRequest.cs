namespace Shared.Contracts.Catalog.Dtos
{
    public class IncreaseItemDto
    {
        public Guid ProductId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class IncreaseItemsRequest
    {
        public List<IncreaseItemDto> Items { get; set; } = new List<IncreaseItemDto>();
    }
}
