using Microsoft.AspNetCore.Mvc;
using NotificationService.Data;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NotificationController : ControllerBase
    {       

        private readonly ILogger<NotificationController> _logger;
        private readonly DataContext _dataContext;

        public NotificationController(ILogger<NotificationController> logger, DataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _dataContext.Notifications.ToListAsync();
            return Ok(list);
        }



    }
}
