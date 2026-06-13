using BackofficePortal.Models;
using Microsoft.AspNetCore.Mvc;
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
            return View();
        }

        // Login form
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
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
                var result = await _apiClient.PostAsync<Dictionary<string,string>>("OrderService", "/auth/login", new { username, password });
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

        public IActionResult Logout()
        {
            HttpContext.Session.Remove("jwt");
            return RedirectToAction("Index");
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

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
