using Shared.Application.Result;
using Shared.Contracts.Catalog.Dtos;

namespace Shared.Http
{
    public interface ICatalogServiceClient
    {
        Task<Result<ProductDto>> GetProductById(Guid id);
        Task<Result<List<ProductDto>>> GetProductsByIdList(List<Guid> ids);
        Task<Result<bool>> DecreaseStock(List<DecreaseItemDto> items);
        Task<Result<bool>> IncreaseStock(List<IncreaseItemDto> items);
    }
}
