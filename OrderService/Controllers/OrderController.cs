using Microsoft.AspNetCore.Mvc;
using MediatR;
using OrderService.Application.Handlers;
using OrderService.Application.Queries;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;


namespace OrderService.Controllers
{
    /// <summary>
    /// Order management endpoints.
    /// </summary>
    [ApiController]
    [Route("api/v1/orders")]
    [Authorize]
    public class OrderController : ControllerBase
    {

        private readonly ILogger<OrderController> _logger;
        private readonly IMediator _mediator;
        private readonly OrderQueries _orderQueries;
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;

        public OrderController(ILogger<OrderController> logger, IMediator mediator, OrderQueries orderQueries, IConfiguration configuration)
        {
            _logger = logger;
            _mediator = mediator;
            _orderQueries = orderQueries;
            _jwtKey = configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? "dev_super_secret_key_please_change_0123456789ABCDEF";
            _jwtIssuer = configuration["Jwt:Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "local";
        }

        /// <summary>
        /// Demo login endpoint (for dev only).
        /// </summary>
        [HttpPost("/api/v1/auth/login")]
        [AllowAnonymous]
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

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtKey));
            var creds = new JwtSecurityToken(
                issuer: _jwtIssuer,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );
            var token = new JwtSecurityTokenHandler().WriteToken(creds);
            return Ok(new { token });
        }

        public class LoginModel { public required string Username { get; set; } public required string Password { get; set; } }

        /// <summary>
        /// Creates a new order.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddOrder([FromBody] AddOrderCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null) return BadRequest();
            var result = await _mediator.Send(command, cancellationToken);
            if(result.IsSuccess == false)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// Lists orders with pagination.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] OrderQueries.GetOrdersFilter? filter)
        {
            var listResult = await _orderQueries.GetOrders(filter);
            if (listResult.IsSuccess == false)
            {
                return BadRequest(listResult.ErrorMessage);
            }
            var total = await _orderQueries.GetOrdersTotalCount(filter);
            var page = Math.Max(filter?.PageNumber ?? 1, 1);
            var size = filter?.PageSize ?? (listResult.Value?.Count ?? 0);
            var envelope = new
            {
                data = listResult.Value ?? new List<Shared.Contracts.Order.OrderDto>(),
                meta = new { page, pageSize = size, totalCount = total }
            };
            return Ok(envelope);
        }

        /// <summary>
        /// Gets order details by id.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Detail([FromRoute] Guid id)
        {
            var result = await _orderQueries.GetOrderById(id);
            if(result.IsSuccess == false)
            {
                return NotFound(result.ErrorMessage);
            }
            return Ok(result.Value);
        }

        /// <summary>
        /// Cancels an existing order.
        /// </summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelOrderCommand command, CancellationToken cancellationToken = default)
        {
            if (command is null) return BadRequest();
            var result = await _mediator.Send(command, cancellationToken);
            if (result.IsSuccess == false)
            {
                return BadRequest(result.ErrorMessage);
            }
            return Ok();
        }
        
    }
}
