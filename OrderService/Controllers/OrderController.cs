using Microsoft.AspNetCore.Mvc;
using MediatR;
using OrderService.Application.Handlers;
using OrderService.Application.Queries;


namespace OrderService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OrderController : ControllerBase
    {

        private readonly ILogger<OrderController> _logger;
        private readonly IMediator _mediator;
        private readonly OrderQueries _orderQueries;

        public OrderController(ILogger<OrderController> logger, IMediator mediator, OrderQueries orderQueries)
        {
            _logger = logger;
            _mediator = mediator;
            _orderQueries = orderQueries;
        }

        [HttpPost("AddOrder")]
        public async Task<IActionResult> AddOrder([FromBody] AddOrderCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null) return BadRequest();
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }

        [HttpGet("List")]
        public async Task<IActionResult> List([FromQuery] OrderQueries.GetOrdersFilter? filter)
        {
            var result = await _orderQueries.GetOrders(filter);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Detail([FromRoute] Guid id)
        {
            var result = await _orderQueries.GetOrderById(id);
            return Ok(result);
        }

        [HttpPost("Cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelOrderCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null) return BadRequest();
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        
    }
}
