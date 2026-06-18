using CatalogService.Application.Handlers;
using CatalogService.Application.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IMediator = MediatR.IMediator;

namespace CatalogService.Controllers
{
    /// <summary>
    /// Product catalog endpoints.
    /// </summary>
    [ApiController]
    [Route("api/v1/products")]
    [Authorize]
    public class CatalogController(ILogger<CatalogController> logger, IMediator mediator, ProductQueries productQueries) : ControllerBase
    {
        private readonly ILogger<CatalogController> _logger = logger;
        private readonly IMediator _mediator = mediator;
        private readonly ProductQueries _productQueries = productQueries;

        /// <summary>
        /// Creates a new product.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddProduct([FromBody] AddProductCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                return BadRequest();

            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsSuccess == false)
            {
                _logger.LogError("Failed to add product: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// Updates an existing product.
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> UpdateProduct([FromBody] UpdateProductCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                return BadRequest();

            var result = await _mediator.Send(command, cancellationToken);
            if(result.IsSuccess == false)
            {
                _logger.LogError("Failed to update product: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// Deletes a product by id.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteProduct([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
                return BadRequest();

            var command = new DeleteProductCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsSuccess == false)
            {
                _logger.LogError("Failed to delete product: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// Gets a product by id.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProductById([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
                return BadRequest();

            var result = await _productQueries.GetProductById(id);
            return Ok(result.Value);
        }

        /// <summary>
        /// Lists products with pagination.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] ProductQueries.GetProductsFilter? filter)
        {
            var listResult = await _productQueries.GetProducts(filter);
            var total = await _productQueries.GetProductsTotalCount(filter);
            var page = Math.Max(filter?.PageNumber ?? 1, 1);
            var size = filter?.PageSize ?? (listResult.Value?.Count ?? 0);
            var envelope = new
            {
                data = listResult.Value ?? new List<Shared.Contracts.Catalog.Dtos.ProductDto>(),
                meta = new { page, pageSize = size, totalCount = total }
            };
            return Ok(envelope);
        }

        
        /// <summary>
        /// Gets products by a list of ids.
        /// </summary>
        [HttpPost("batch")]
        public async Task<IActionResult> GetProductsByIdList([FromBody] List<Guid> ids)
        {
            var result = await _productQueries.GetProductsByIdList(ids ?? new List<Guid>());
            if(result.IsSuccess == false)
            {
                _logger.LogError("Failed to get products by id list: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// Decreases stock for the given items.
        /// </summary>
        [HttpPost("decrease")]
        public async Task<IActionResult> DecreaseStock([FromBody] DecreaseStockCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null || command.Items == null || command.Items.Count == 0)
                return BadRequest();

            var result = await _mediator.Send(command, cancellationToken);
            if(result.IsSuccess == false)
            {
                _logger.LogError("Failed to decrease stock: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// Increases stock for the given items.
        /// </summary>
        [HttpPost("increase")]
        public async Task<IActionResult> IncreaseStock([FromBody] IncreaseStockCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null || command.Items == null || command.Items.Count == 0)
                return BadRequest();

            var result = await _mediator.Send(command, cancellationToken);
            if(result.IsSuccess == false)
            {
                _logger.LogError("Failed to increase stock: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Value);
        }
    }
}
