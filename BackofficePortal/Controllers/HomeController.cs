using BackofficePortal.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;

namespace BackofficePortal.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly BackofficePortal.Services.ApiClient _apiClient;

        public HomeController(ILogger<HomeController> logger, BackofficePortal.Services.ApiClient apiClient)
        {
            _logger = logger;
            _apiClient = apiClient;
        }

        public IActionResult Index()
        {
            var token = HttpContext.Session.GetString("jwt");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToAction(nameof(Login));
            }

            return View();
        }

        // Login form
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            var token = HttpContext.Session.GetString("jwt");
            if (!string.IsNullOrEmpty(token))
            {
                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Username and password are required");
                return View();
            }

            // call auth endpoint on OrderService for demo (endpoint expected: /auth/login)
            try
            {
                var result = await _apiClient.PostAsync<Dictionary<string,string>>("OrderService", "/auth/login", new { Username = username, Password = password });
                if (result != null && result.TryGetValue("token", out var token))
                {
                    HttpContext.Session.SetString("jwt", token);
                    return RedirectToAction("Index");
                }
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Login failed");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("jwt");
            return RedirectToAction(nameof(Login));
        }

        public async Task<IActionResult> GetOrders()
        {
            try
            {
                var data = await _apiClient.GetAsync<object[]>("OrderService", "/order/list");
                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CancelOrder([FromBody] Guid id)
        {
            if (id == Guid.Empty) return BadRequest(new { error = "Invalid id" });
            try
            {
                var res = await _apiClient.PostAsync<object>("OrderService", "/order/cancel", new { Id = id });
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddOrder([FromBody] BackofficePortal.Models.AddOrderRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CustomerEmail) || request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            try
            {
                var payload = new
                {
                    request.CustomerEmail,
                    request.CustomerTelNumber,
                    Items = request.Items.Select(i => new { i.ProductId, i.Quantity }).ToList(),
                    IdempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString()
                };

                var orderId = await _apiClient.PostAsync<Guid>("OrderService", "/order/addorder", payload);
                if (orderId == Guid.Empty)
                    return BadRequest(new { error = "Failed to create order" });

                return Json(new { success = true, orderId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var data = await _apiClient.GetAsync<object[]>("CatalogService", "/catalog");
                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddProduct([FromBody] dynamic body)
        {
            try
            {
                var id = await _apiClient.PostAsync<Guid>("CatalogService", "/catalog/addproduct", body);
                return Json(new { success = id != Guid.Empty, id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateProduct([FromBody] dynamic body)
        {
            try
            {
                var id = await _apiClient.PutAsync<Guid>("CatalogService", "/catalog/updateproduct", body);
                return Json(new { success = id != Guid.Empty, id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            if (id == Guid.Empty) return BadRequest(new { error = "Invalid id" });
            try
            {
                var deletedId = await _apiClient.DeleteAsync<Guid>("CatalogService", $"/catalog/{id}");
                return Json(new { success = deletedId != Guid.Empty, id = deletedId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        public async Task<IActionResult> GetOrderById(Guid id)
        {
            if (id == Guid.Empty) return BadRequest(new { error = "Invalid id" });
            try
            {
                var data = await _apiClient.GetAsync<object>("OrderService", $"/order/{id}");
                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var data = await _apiClient.GetAsync<object[]>("NotificationService", "/notification");
                return Json(data);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Privacy removed

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
