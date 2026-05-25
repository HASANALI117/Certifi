using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TrainingPlatform.Reports.Models;

namespace TrainingPlatform.Reports.Controllers
{
    public class AccountController : Controller
    {
        private readonly HttpClient _httpClient;

        // We "inject" the HttpClient here so the controller can use it
        public AccountController(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Convert the username/password into a JSON package
            var jsonString = JsonSerializer.Serialize(model);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            // send the login request to the API and wait for the response
            var response = await _httpClient.PostAsync("https://localhost:7181/api/Auth/login", content);

            // Check if the API accepted the login
            if (response.IsSuccessStatusCode)
            {
                // Extract the JWT token from the API's response
                var token = await response.Content.ReadAsStringAsync();

                // Store the token safely in a browser cookie so we can use it later
                Response.Cookies.Append("JWToken", token, new CookieOptions { HttpOnly = true });

                // Redirect to the dashboard
                return RedirectToAction("Index", "Home");
            }

            // If the API says unauthorized (wrong password), show an error on the form
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }
    }
}