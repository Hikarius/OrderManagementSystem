using Microsoft.AspNetCore.Mvc;
using NotificationService.Data;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Controllers
{
    /// <summary>
    /// Notification read endpoints.
    /// </summary>
    [ApiController]
    [Route("api/v1/notifications")]
    public class NotificationController : ControllerBase
    {       

        private readonly ILogger<NotificationController> _logger;
        private readonly DataContext _dataContext;

        public NotificationController(ILogger<NotificationController> logger, DataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        /// <summary>
        /// Lists notifications with pagination.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            page = Math.Max(page, 1);
            pageSize = pageSize <= 0 ? 50 : pageSize;
            var total = await _dataContext.Notifications.CountAsync();
            var list = await _dataContext.Notifications
                .OrderBy(n => n.OrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return Ok(new { data = list, meta = new { page, pageSize, totalCount = total } });
        }



    }
}
