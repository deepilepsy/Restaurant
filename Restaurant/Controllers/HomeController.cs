using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Models;
using System.Security.Cryptography;
using System.Text;

namespace Restaurant.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly RestaurantContext _context;

    public HomeController(ILogger<HomeController> logger, RestaurantContext context)
    {
        _logger = logger;
        _context = context;
    }
    
    /*[HttpGet]
    public IActionResult ReservationForm(Tables table)
    {
        
        return View(table);
    } */
    public IActionResult Menu()
    {
        return View();
    }

    
    [HttpGet]
    public IActionResult Booking()
    {
        
        return View();
    }

    public IActionResult Index()
    {
        return View();
    }

    // GET: Display login page
    public IActionResult Login()
    {
        return View();
    }
    
    // POST: Handle login form submission with database authentication
    [HttpPost]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Please fill in all required fields.";
            return View();
        }
        try
        {
            var user = await _context.Credentials
                .FirstOrDefaultAsync(c => c.Username == loginDto.Username &&
                                          c.Password == loginDto.Password);

            if (user != null)
            {
                HttpContext.Session.SetString("isLoggedIn", "true");
                HttpContext.Session.SetString("username", user.Username);
                return RedirectToAction("Index");
            }
            else
            {
                ViewBag.Error = "Username or Password do not match.";
                return View();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Login error: {ex.Message}");
            ViewBag.Error = "Error: " + ex.Message; // TEMPORARY: expose message for debugging
            return View();
        }
    }


    // Alternative POST method to maintain compatibility with your existing form
    [HttpPost]
    public async Task<IActionResult> LoginLegacy(string username, string password)
    {
        var loginDto = new LoginDto { Username = username, Password = password };
        return await Login(loginDto);
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }

    public IActionResult Admin()
    {
        // Check if user is logged in
        if (HttpContext.Session.GetString("isLoggedIn") != "true")
        {
            return RedirectToAction("Login");
        }
        var staffMembers = _context.Staff.ToList(); // or wherever you get staff
        var upcomingReceipts = _context.Receipts
            .Include(r => r.ServedBy)
            .Include(r => r.Table)
            .Where(r => r.ReservationDate >= DateTime.Today)
            .ToList();

        var model = new AdminPanelView
        {
            StaffMembers = staffMembers ?? new List<Staff>(),
            UpcomingReceipts = upcomingReceipts ?? new List<Receipt>()
        };

        return View(model);
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
    
    // Helper method to hash passwords
    private string HashPassword(string password)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
    
    // Method to create a user (for testing/setup purposes)
    // You might want to remove this in production or add proper authorization
    [HttpPost]
    public async Task<IActionResult> CreateUser(string username, string password)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _context.Credentials
                .FirstOrDefaultAsync(c => c.Username == username);
            
            if (existingUser != null)
            {
                return Json(new { success = false, message = "User already exists" });
            }
            
            var hashedPassword = HashPassword(password);
            var credentials = new Credentials
            {
                Username = username,
                Password = hashedPassword
            };
            
            _context.Credentials.Add(credentials);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, message = "User created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", username);
            return Json(new { success = false, message = "Error creating user" });
        }
    }
    
    // Helper method to check if user is logged in (you can use this in other actions)
    private bool IsLoggedIn()
    {
        return HttpContext.Session.GetString("isLoggedIn") == "true";
    }
    
    // Get current logged in username
    private string GetCurrentUsername()
    {
        return HttpContext.Session.GetString("username") ?? "";
    }
}