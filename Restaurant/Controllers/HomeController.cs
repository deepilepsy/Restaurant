using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Restaurant.Models;

namespace Restaurant.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }
    
    [HttpGet]
    public IActionResult ReservationForm(Tables table)
    {
        
        return View(table);
    }
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

    public IActionResult Login()
    {
        return View();
    }
    
    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        if (username == "admin" && password == "admin")
        {
            HttpContext.Session.SetString("isLoggedIn", "true"); // Logged in.
            return RedirectToAction("Index");
        }
        else
        {
            ViewBag.Error = "Username or Password do not match.";
            return View();
        }
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }

    public IActionResult Admin()
    {
        return View();
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