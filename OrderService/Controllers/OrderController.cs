using Microsoft.AspNetCore.Mvc;
using MediatR;
using OrderService.Application.Handlers;
using OrderService.Application.Queries;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;


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

        [HttpPost("/auth/login")]
        public IActionResult Login([FromBody] LoginModel model)
        {
            // WARNING: This is a demo login for local development only.
            if (model == null)
                return Unauthorized();

            // simple demo users: admin and operator (password both 'pass')
            var isAdmin = model.Username == "admin" && model.Password == "pass";
            var isOperator = model.Username == "operator" && model.Password == "pass";
            if (!isAdmin && !isOperator)
                return Unauthorized();

            var role = isAdmin ? "admin" : "operator";

            var claims = new[] {
                new Claim(ClaimTypes.Name, model.Username ?? string.Empty),
                new Claim(ClaimTypes.Role, role)
            };

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("change_this_in_production"));
            var creds = new JwtSecurityToken(
                issuer: "local",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );
            var token = new JwtSecurityTokenHandler().WriteToken(creds);
            return Ok(new { token });
        }

        public class LoginModel { public required string Username { get; set; } public required string Password { get; set; } }

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
