using CatalogService.Application.Handlers;
using CatalogService.Application.Queries;
using MassTransit.Mediator;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IMediator = MediatR.IMediator;

namespace CatalogService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CatalogController(ILogger<CatalogController> logger, IMediator mediator, Application.Queries.ProductQueries productQueries) : ControllerBase
    {
        private readonly ILogger<CatalogController> _logger = logger;
        private readonly IMediator _mediator = mediator;
        private readonly ProductQueries _productQueries = productQueries;

        [HttpPost("AddProduct")]
        public async Task<IActionResult> AddProduct([FromBody] AddProductCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                return BadRequest();

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPut("UpdateProduct")]
        public async Task<IActionResult> UpdateProduct([FromBody] UpdateProductCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                return BadRequest();

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteProduct([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
                return BadRequest();

            var command = new DeleteProductCommand(id);
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProductById([FromRoute] Guid id, CancellationToken cancellationToken = default)
        {
            if (id == Guid.Empty)
                return BadRequest();

            var result = await _productQueries.GetProductById(id);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] ProductQueries.GetProductsFilter? filter)
        {
            var result = await _productQueries.GetProducts(filter);
            return Ok(result);
        }

        
        [HttpPost("batch")]
        public async Task<IActionResult> GetProductsByIdList([FromBody] List<Guid> ids)
        {
            var result = await _productQueries.GetProductsByIdList(ids ?? new List<Guid>());
            return Ok(result);
        }

        [HttpPost("decrease")]
        public async Task<IActionResult> DecreaseStock([FromBody] DecreaseStockCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null || command.Items == null || command.Items.Count == 0)
                return BadRequest();

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpPost("increase")]
        public async Task<IActionResult> IncreaseStock([FromBody] IncreaseStockCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null || command.Items == null || command.Items.Count == 0)
                return BadRequest();

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
    }
}
