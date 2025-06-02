using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Models;

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

    // Basic pages
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Menu()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    // Login and Authentication
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Error = "Please fill in all fields.";
            return View();
        }

        // Check admin login
        var admin = await _context.Credentials
            .FirstOrDefaultAsync(c => c.Username == loginDto.Username && c.Password == loginDto.Password);

        if (admin != null)
        {
            HttpContext.Session.SetString("admin", "true");
            HttpContext.Session.SetString("username", admin.Username);
            return RedirectToAction("Index");
        }

        // Check staff login
        var staff = await _context.StaffCredentials
            .FirstOrDefaultAsync(s => s.Username == loginDto.Username && s.Password == loginDto.Password);

        if (staff != null)
        {
            HttpContext.Session.SetString("staff", "true");
            HttpContext.Session.SetString("username", staff.Username);
            return RedirectToAction("Index");
        }

        ViewBag.Error = "Username or Password do not match.";
        return View();
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index");
    }

    // Admin Panel
    public async Task<IActionResult> Admin()
    {
        if (HttpContext.Session.GetString("admin") != "true")
        {
            return RedirectToAction("Login");
        }

        var staffMembers = await _context.Staff.ToListAsync();
        var activeReservations = await _context.Reservations
            .Include(r => r.ServedBy)
            .Include(r => r.Table)
            .Include(r => r.Customer)
            .Where(r => r.ReservationStatus == "active" && r.ReservationDate >= DateTime.Today)
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.ReservationHour)
            .ToListAsync();

        var model = new AdminPanelView
        {
            StaffMembers = staffMembers,
            UpcomingReceipts = activeReservations
        };

        return View(model);
    }

    // Staff Panel
    public async Task<IActionResult> Staff()
    {
        if (HttpContext.Session.GetString("staff") != "true")
        {
            return RedirectToAction("Login");
        }
    
        var activeReservations = await _context.Reservations
            .Include(r => r.Table)
            .Include(r => r.ServedBy)
            .Include(r => r.Customer)
            .Where(r => r.ReservationStatus == "active" && r.ReservationDate >= DateTime.Today)
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.ReservationHour)
            .ToListAsync();

        var model = new AdminPanelView
        {
            UpcomingReceipts = activeReservations
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!CheckUserLoggedIn())
        {
            return RedirectToAction("Index");
        }

        var reservation = await _context.Reservations
            .Include(r => r.Table)
            .Include(r => r.ServedBy)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.ReservationId == id);

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return GoBackToPanel();
        }

        return View(reservation);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Reservation model)
    {
        if (!CheckUserLoggedIn())
        {
            return RedirectToAction("Login");
        }

        // Remove validation for optional fields
        ModelState.Remove("Customer");
        ModelState.Remove("Table");
        ModelState.Remove("ServedBy");

        if (!ModelState.IsValid)
        {
            // Reload related data for the view
            var reservationForView = await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Table)
                .Include(r => r.ServedBy)
                .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);
            
            if (reservationForView != null)
            {
                // Copy form data back to the model for display
                if (reservationForView.Customer != null)
                {
                    reservationForView.Customer.Name = Request.Form["Customer.Name"];
                    reservationForView.Customer.Surname = Request.Form["Customer.Surname"];
                    reservationForView.Customer.TelNo = Request.Form["Customer.TelNo"];
                    reservationForView.Customer.Email = Request.Form["Customer.Email"];
                }
                reservationForView.SpecialRequests = model.SpecialRequests;
                reservationForView.GuestNumber = model.GuestNumber;
                reservationForView.TableId = model.TableId;
                reservationForView.ReservationDate = model.ReservationDate;
                reservationForView.ReservationHour = model.ReservationHour;
                reservationForView.ServedById = model.ServedById;
            }
            
            return View(reservationForView ?? model);
        }

        var reservation = await _context.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Table)
            .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return GoBackToPanel();
        }

        // Update customer information
        if (reservation.Customer != null)
        {
            reservation.Customer.Name = Request.Form["Customer.Name"].ToString().Trim();
            reservation.Customer.Surname = string.IsNullOrWhiteSpace(Request.Form["Customer.Surname"]) ? 
                null : Request.Form["Customer.Surname"].ToString().Trim();
            reservation.Customer.TelNo = Request.Form["Customer.TelNo"].ToString().Trim();
            reservation.Customer.Email = string.IsNullOrWhiteSpace(Request.Form["Customer.Email"]) ? 
                null : Request.Form["Customer.Email"].ToString().Trim();
        }

        // Update reservation-specific fields
        reservation.SpecialRequests = string.IsNullOrWhiteSpace(model.SpecialRequests) ? 
            null : model.SpecialRequests.Trim();

        // Only update reservation details if status is active
        if (reservation.ReservationStatus == "active")
        {
            // Validate the selected table can accommodate the guest count
            var selectedTable = await _context.RestaurantTables
                .Include(t => t.ServedBy)
                .FirstOrDefaultAsync(t => t.TableId == model.TableId);

            if (selectedTable == null)
            {
                TempData["Error"] = "Selected table not found.";
                return View(reservation);
            }

            if (model.GuestNumber < selectedTable.MinCapacity || model.GuestNumber > selectedTable.MaxCapacity)
            {
                TempData["Error"] = $"Table {model.TableId} capacity is {selectedTable.MinCapacity}-{selectedTable.MaxCapacity} people. You selected {model.GuestNumber} guests.";
                return View(reservation);
            }

            // Check if the table is available for the selected date/time (excluding current reservation)
            var existingReservation = await _context.Reservations
                .Where(r => r.TableId == model.TableId && 
                           r.ReservationDate.Date == model.ReservationDate.Date && 
                           r.ReservationHour == model.ReservationHour &&
                           r.ReservationStatus == "active" &&
                           r.ReservationId != model.ReservationId)
                .FirstOrDefaultAsync();

            if (existingReservation != null)
            {
                TempData["Error"] = $"Table {model.TableId} is already reserved for {model.ReservationDate.ToShortDateString()} at {model.ReservationHour}.";
                return View(reservation);
            }

            // Check if date is not in the past
            if (model.ReservationDate.Date < DateTime.Today)
            {
                TempData["Error"] = "Cannot set reservation date to a past date.";
                return View(reservation);
            }

            // Update reservation details
            reservation.GuestNumber = model.GuestNumber;
            reservation.TableId = model.TableId;
            reservation.ReservationDate = model.ReservationDate;
            reservation.ReservationHour = model.ReservationHour;
            reservation.ServedById = selectedTable.ServedById; // Auto-assign staff based on table
        }

        try
        {
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Reservation #{model.ReservationId} has been updated successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating reservation {ReservationId}", model.ReservationId);
            TempData["Error"] = "An error occurred while updating the reservation. Please try again.";
            return View(reservation);
        }

        return GoBackToPanel();
    }

    // Delete Reservations
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        if (!CheckUserLoggedIn())
        {
            return RedirectToAction("Login");
        }

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == id);

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return GoBackToPanel();
        }

        if (reservation.ReservationStatus != "active")
        {
            TempData["Error"] = "Only active reservations can be deleted.";
            return RedirectToAction("Edit", new { id = reservation.ReservationId });
        }

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Reservation #{id} has been permanently deleted.";
        return GoBackToPanel();
    }

    // Booking Page
    [HttpGet]
    public async Task<IActionResult> Booking()
    {
        var tables = await _context.RestaurantTables
            .Include(t => t.ServedBy)
            .OrderBy(t => t.TableId)
            .ToListAsync();

        var today = DateTime.Today;
        var todayReservations = await _context.Reservations
            .Where(r => r.ReservationDate.Date == today && r.ReservationStatus == "active")
            .Select(r => new { r.TableId, r.ReservationHour })
            .ToListAsync();

        var occupiedTablesByHour = todayReservations
            .GroupBy(r => r.ReservationHour)
            .ToDictionary(g => g.Key, g => g.Select(r => r.TableId).ToList());

        var viewModel = new BookingViewModel
        {
            AvailableTables = tables,
            OccupiedTablesByHour = occupiedTablesByHour
        };

        return View(viewModel);
    }

    // Staff Management
    [HttpGet]
    public IActionResult CreateStaff()
    {
        if (HttpContext.Session.GetString("admin") != "true")
        {
            return RedirectToAction("Login");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateStaff(Staff staff)
    {
        if (!ModelState.IsValid)
        {
            return View(staff);
        }

        // Check if phone number already exists
        var existingStaff = await _context.Staff
            .FirstOrDefaultAsync(s => s.TelNo == staff.TelNo);

        if (existingStaff != null)
        {
            ModelState.AddModelError("TelNo", "A staff member with this phone number already exists.");
            return View(staff);
        }

        // Check if job is valid
        var validJobs = new[] { "waiter", "chef", "manager", "cashier", "dishwasher", "cleaner" };
        if (!validJobs.Contains(staff.Job.ToLower()))
        {
            ModelState.AddModelError("Job", "Please select a valid job position.");
            return View(staff);
        }

        _context.Staff.Add(staff);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully added.";
        return RedirectToAction("Admin");
    }

    [HttpGet]
    public async Task<IActionResult> EditStaff(int id)
    {
        if (HttpContext.Session.GetString("admin") != "true")
        {
            return RedirectToAction("Login");
        }

        var staff = await _context.Staff.FindAsync(id);
        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }
        return View(staff);
    }

    [HttpPost]
    public async Task<IActionResult> EditStaff(Staff staff)
    {
        if (HttpContext.Session.GetString("admin") != "true")
        {
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid)
        {
            return View(staff);
        }

        // Check if phone number exists for another staff member
        var existingStaff = await _context.Staff
            .FirstOrDefaultAsync(s => s.TelNo == staff.TelNo && s.StaffId != staff.StaffId);

        if (existingStaff != null)
        {
            ModelState.AddModelError("TelNo", "Another staff member with this phone number already exists.");
            return View(staff);
        }

        // Check if job is valid
        var validJobs = new[] { "waiter", "chef", "manager", "cashier", "dishwasher", "cleaner" };
        if (!validJobs.Contains(staff.Job.ToLower()))
        {
            ModelState.AddModelError("Job", "Please select a valid job position.");
            return View(staff);
        }

        _context.Update(staff);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully updated.";
        return RedirectToAction("Admin");
    }

    [HttpGet]
    public async Task<IActionResult> DeleteStaff(int id)
    {
        if (HttpContext.Session.GetString("admin") != "true")
        {
            return RedirectToAction("Login");
        }

        var staff = await _context.Staff
            .FirstOrDefaultAsync(s => s.StaffId == id);

        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }

        // Check if staff has active reservations
        var activeReservations = await _context.Reservations
            .CountAsync(r => r.ServedById == id && r.ReservationStatus == "active");

        ViewBag.ActiveReservations = activeReservations;
        return View(staff);
    }

    [HttpPost, ActionName("DeleteStaff")]
    public async Task<IActionResult> DeleteStaffConfirmed(int id)
    {
        if (HttpContext.Session.GetString("admin") != "true")
        {
            return RedirectToAction("Login");
        }

        var staff = await _context.Staff.FindAsync(id);
        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }

        // Check if staff has active reservations
        var activeReservations = await _context.Reservations
            .CountAsync(r => r.ServedById == id && r.ReservationStatus == "active");

        if (activeReservations > 0)
        {
            TempData["Error"] = $"Cannot delete staff member. They have {activeReservations} active reservation(s). Please reassign or complete these reservations first.";
            return RedirectToAction("Admin");
        }

        _context.Staff.Remove(staff);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully deleted.";
        return RedirectToAction("Admin");
    }

    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(string username, string password, string type)
    {
        // Check if user already exists
        var existingStaff = await _context.StaffCredentials
            .FirstOrDefaultAsync(c => c.Username == username);
        var existingAdmin = await _context.Credentials.FirstOrDefaultAsync(c => c.Username == username);

        if (existingStaff != null || existingAdmin != null)
        {
            TempData["UserExists"] = "User already exists.";
            return RedirectToAction("CreateUser");
        }

        if (type == "staff")
        {
            var staffCredentials = new StaffCredentials
            {
                Username = username,
                Password = password
            };

            _context.StaffCredentials.Add(staffCredentials);
        }
        else if (type == "admin")
        {
            var adminCredentials = new Credentials()
            {
                Username = username,
                Password = password
            };

            _context.Credentials.Add(adminCredentials);
        }

        await _context.SaveChangesAsync();

        return RedirectToAction("Admin");
    }

    [HttpGet]
    public async Task<IActionResult> SearchReservations(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            return Json(new { success = false, message = "Query too short" });
        }

        var reservations = await _context.Reservations
            .Include(r => r.Table)
            .Include(r => r.ServedBy)
            .Include(r => r.Customer)
            .Where(r => r.ReservationId.ToString().Contains(query) ||
                        (r.Customer != null && r.Customer.Name.Contains(query)) ||
                        (r.Customer != null && r.Customer.Surname != null && r.Customer.Surname.Contains(query)) ||
                        (r.Customer != null && r.Customer.TelNo.Contains(query)))
            .OrderByDescending(r => r.ReservationDate)
            .Take(5)
            .Select(r => new
            {
                r.ReservationId,
                Name = r.Customer != null ? r.Customer.Name : "",
                Surname = r.Customer != null ? r.Customer.Surname : "",
                r.ReservationDate,
                r.ReservationHour,
                r.TableId
            })
            .ToListAsync();

        return Json(new { success = true, results = reservations });
    }

    [HttpGet]
    public async Task<IActionResult> GetAvailableTables(DateTime date, string time, int currentReservationId)
    {
        var availableTables = await _context.RestaurantTables
            .Include(t => t.ServedBy)
            .Where(t => !_context.Reservations.Any(r =>
                r.TableId == t.TableId &&
                r.ReservationDate.Date == date.Date &&
                r.ReservationHour == time &&
                r.ReservationStatus == "active" &&
                r.ReservationId != currentReservationId))
            .OrderBy(t => t.TableId)
            .Select(t => new
            {
                t.TableId,
                t.MinCapacity,
                t.MaxCapacity,
                ServerName = t.ServedBy.Name + " " + t.ServedBy.Surname
            })
            .ToListAsync();

        return Json(new { success = true, tables = availableTables });
    }

    [HttpGet]
    public async Task<IActionResult> GetStaffMembers()
    {
        var staffMembers = await _context.Staff
            .Where(s => s.Job == "waiter")
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.StaffId,
                Name = s.Name + " " + s.Surname,
                s.Job,
                s.TelNo
            })
            .ToListAsync();

        return Json(new { success = true, staff = staffMembers });
    }

    [HttpGet]
    public async Task<IActionResult> GetOccupiedTables(string date, string time)
    {
        if (!DateTime.TryParse(date, out DateTime reservationDate))
        {
            return Json(new { success = false, message = "Invalid date format" });
        }

        var occupiedTables = await _context.Reservations
            .Where(r => r.ReservationDate.Date == reservationDate.Date &&
                       r.ReservationHour == time &&
                       r.ReservationStatus == "active")
            .Select(r => r.TableId)
            .ToListAsync();

        return Json(new { success = true, occupiedTables });
    }

    [HttpGet]
    public async Task<IActionResult> GetTableCapacities()
    {
        var tableCapacities = await _context.RestaurantTables
            .OrderBy(t => t.TableId)
            .Select(t => new { t.TableId, t.MinCapacity, t.MaxCapacity })
            .ToListAsync();

        return Json(new { success = true, tableCapacities });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateReservationStatus([FromBody] UpdateReservationStatusRequest request)
    {
        if (!CheckUserLoggedIn())
        {
            return Json(new { success = false, message = "Unauthorized access." });
        }

        if (request == null || request.ReservationId <= 0 || string.IsNullOrWhiteSpace(request.Status))
        {
            return Json(new { success = false, message = "Invalid request data." });
        }

        // Check if status is valid
        var validStatuses = new[] { "checked-in", "cancelled" };
        if (!validStatuses.Contains(request.Status.ToLower()))
        {
            return Json(new { success = false, message = "Invalid status value." });
        }

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == request.ReservationId);

        if (reservation == null)
        {
            return Json(new { success = false, message = "Reservation not found." });
        }

        if (reservation.ReservationStatus != "active")
        {
            return Json(new { success = false, message = "This reservation has already been processed." });
        }

        // Update the status
        reservation.ReservationStatus = request.Status.ToLower();
        await _context.SaveChangesAsync();

        var statusText = request.Status.ToLower() == "checked-in" ? "checked in" : "cancelled";
        var message = $"Reservation #{reservation.ReservationId} has been {statusText} successfully.";

        return Json(new { success = true, message = message });
    }

    [HttpGet]
    public async Task<IActionResult> GetStaffStatistics()
    {
        if (HttpContext.Session.GetString("admin") != "true")
        {
            return Json(new { success = false, message = "Unauthorized" });
        }

        var totalStaff = await _context.Staff.CountAsync();
        var totalWaiters = await _context.Staff.CountAsync(s => s.Job.ToLower() == "waiter");
        var totalChefs = await _context.Staff.CountAsync(s => s.Job.ToLower() == "chef");
        var totalManagers = await _context.Staff.CountAsync(s => s.Job.ToLower() == "manager");
        var totalCashiers = await _context.Staff.CountAsync(s => s.Job.ToLower() == "cashier");
        var activeReservations = await _context.Reservations.CountAsync(r => r.ReservationStatus == "active");

        return Json(new
        {
            success = true,
            statistics = new
            {
                totalStaff,
                totalWaiters,
                totalChefs,
                totalManagers,
                totalCashiers,
                activeReservations
            }
        });
    }
    
    // Search for reservations
    public async Task<IActionResult> Receipt(string receiptSearch)
    {
        if (string.IsNullOrWhiteSpace(receiptSearch))
        {
            TempData["Error"] = "Please enter a reservation ID to search.";
            return RedirectToAction("Index");
        }

        if (int.TryParse(receiptSearch.Trim(), out int reservationId))
        {
            var reservation = await _context.Reservations
                .Include(r => r.Table)
                .Include(r => r.ServedBy)
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            if (reservation != null)
            {
                return RedirectToAction("Confirmation", "Reservation", new { id = reservationId });
            }
        }

        TempData["Error"] = $"No reservation found with ID: {receiptSearch}";
        return RedirectToAction("Index");
    }
// ORDER MANAGEMENT 
public async Task<IActionResult> OrderManagement(int reservationId)
{
    try
    {
        _logger.LogInformation("Loading order management for reservation ID: {ReservationId}", reservationId);

        // Get the reservation with related data
        var reservation = await _context.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Table)
            .Include(r => r.ServedBy)
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation not found for ID: {ReservationId}", reservationId);
            TempData["Error"] = "Reservation not found.";
            return RedirectToAction("Index");
        }

        // Get all menu items
        var menuItems = await _context.MenuItems
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Subcategory)
            .ThenBy(m => m.ItemName)
            .ToListAsync();

        _logger.LogInformation("Loaded {MenuItemCount} menu items", menuItems.Count);

        // Check if there's an existing receipt for this reservation
        Receipt? existingReceipt = null;
        try
        {
            existingReceipt = await _context.Receipts
                .Include(r => r.ReceiptItems)
                .ThenInclude(ri => ri.MenuItem)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

            if (existingReceipt != null)
            {
                _logger.LogInformation("Found existing receipt {ReceiptId} for reservation {ReservationId}", 
                    existingReceipt.ReceiptId, reservationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading existing receipt for reservation {ReservationId}", reservationId);
            // Continue without existing receipt - this is not critical
        }

        var viewModel = new OrderManagementViewModel
        {
            Reservation = reservation,
            MenuItems = menuItems,
            ExistingReceipt = existingReceipt
        };

        return View(viewModel);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in OrderManagement for reservation ID: {ReservationId}", reservationId);
        TempData["Error"] = $"An error occurred while loading the order management page: {ex.Message}";
        return RedirectToAction("Index");
    }
}

// POST: ProcessOrder
[HttpPost]
public async Task<IActionResult> ProcessOrder([FromBody] OrderRequest orderRequest)
{
    try
    {
        _logger.LogInformation("Processing order for reservation {ReservationId}", orderRequest?.ReservationId);

        if (orderRequest == null || orderRequest.Items == null || !orderRequest.Items.Any())
        {
            _logger.LogWarning("Invalid order request - no items provided");
            return Json(new { success = false, message = "No items in the order." });
        }

        // Validate reservation exists
        var reservationExists = await _context.Reservations
            .AnyAsync(r => r.ReservationId == orderRequest.ReservationId);

        if (!reservationExists)
        {
            _logger.LogWarning("Reservation {ReservationId} not found", orderRequest.ReservationId);
            return Json(new { success = false, message = "Reservation not found." });
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Check if a receipt already exists for this reservation
            var existingReceipt = await _context.Receipts
                .Include(r => r.ReceiptItems)
                .FirstOrDefaultAsync(r => r.ReservationId == orderRequest.ReservationId);

            Receipt receipt;
            bool isUpdate = false;

            if (existingReceipt != null)
            {
                _logger.LogInformation("Updating existing receipt {ReceiptId}", existingReceipt.ReceiptId);
                
                // Update existing receipt
                receipt = existingReceipt;
                receipt.TotalAmount = orderRequest.TotalAmount;
                receipt.CreatedAt = DateTime.Now;
                isUpdate = true;

                // Remove existing receipt items
                if (receipt.ReceiptItems.Any())
                {
                    _context.ReceiptItems.RemoveRange(receipt.ReceiptItems);
                    await _context.SaveChangesAsync();
                }

                _context.Receipts.Update(receipt);
            }
            else
            {
                _logger.LogInformation("Creating new receipt for reservation {ReservationId}", orderRequest.ReservationId);
                
                // Create new receipt
                receipt = new Receipt
                {
                    ReservationId = orderRequest.ReservationId,
                    TableId = orderRequest.TableId,
                    CustomerId = orderRequest.CustomerId,
                    StaffId = orderRequest.StaffId,
                    TotalAmount = orderRequest.TotalAmount,
                    CreatedAt = DateTime.Now
                };

                _context.Receipts.Add(receipt);
            }

            await _context.SaveChangesAsync();

            // Create new receipt items
            var receiptItems = new List<ReceiptItem>();
            foreach (var item in orderRequest.Items)
            {
                // Validate menu item exists
                var menuItemExists = await _context.MenuItems
                    .AnyAsync(m => m.ItemId == item.ItemId);

                if (!menuItemExists)
                {
                    _logger.LogWarning("Menu item {ItemId} not found", item.ItemId);
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = $"Menu item with ID {item.ItemId} not found." });
                }

                var receiptItem = new ReceiptItem
                {
                    ReceiptId = receipt.ReceiptId,
                    ItemId = item.ItemId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice,
                    SpecialNotes = orderRequest.SpecialNotes
                };

                receiptItems.Add(receiptItem);
            }

            _context.ReceiptItems.AddRange(receiptItems);
            await _context.SaveChangesAsync();

            // Update reservation status if needed
            var reservation = await _context.Reservations.FindAsync(orderRequest.ReservationId);
            if (reservation != null && reservation.ReservationStatus != "active")
            {
                reservation.ReservationStatus = "active";
                _context.Reservations.Update(reservation);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            string message = isUpdate ? "Order updated successfully!" : "Order processed successfully!";
            _logger.LogInformation("Order {Action} successfully for reservation {ReservationId}", 
                isUpdate ? "updated" : "created", orderRequest.ReservationId);
            
            return Json(new { success = true, message = message, isUpdate = isUpdate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in transaction for reservation {ReservationId}", orderRequest.ReservationId);
            await transaction.RollbackAsync();
            throw;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing order for reservation {ReservationId}", orderRequest?.ReservationId);
        return Json(new { success = false, message = "An error occurred while processing the order: " + ex.Message });
    }
}

// GET: OrderManagement by tableId (alternative method if you prefer table-based routing)
public async Task<IActionResult> OrderManagementByTable(int tableId)
{
    try
    {
        _logger.LogInformation("Loading order management for table ID: {TableId}", tableId);

        // Find active reservation for this table
        var reservation = await _context.Reservations
            .Include(r => r.Customer)
            .Include(r => r.Table)
            .Include(r => r.ServedBy)
            .Where(r => r.TableId == tableId && r.ReservationStatus == "active")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (reservation == null)
        {
            _logger.LogWarning("No active reservation found for table {TableId}", tableId);
            TempData["Error"] = "No active reservation found for this table.";
            return RedirectToAction("Index");
        }

        return await OrderManagement(reservation.ReservationId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in OrderManagementByTable for table ID: {TableId}", tableId);
        TempData["Error"] = "An error occurred while loading the order management page.";
        return RedirectToAction("Index");
    }
}

// Helper method to get menu items by category (optional - for AJAX calls)
[HttpGet]
public async Task<IActionResult> GetMenuItemsByCategory(string category)
{
    try
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Json(new { error = "Category is required." });
        }

        var menuItems = await _context.MenuItems
            .Where(m => m.Category == category)
            .OrderBy(m => m.Subcategory)
            .ThenBy(m => m.ItemName)
            .ToListAsync();

        return Json(menuItems);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting menu items for category: {Category}", category);
        return Json(new { error = "Failed to load menu items." });
    }
}

// Method to get receipt details (optional - for viewing existing orders)
public async Task<IActionResult> ViewReceipt(int receiptId)
{
    try
    {
        var receipt = await _context.Receipts
            .Include(r => r.Table)
            .Include(r => r.Customer)
            .Include(r => r.Staff)
            .Include(r => r.Reservation)
            .Include(r => r.ReceiptItems)
            .ThenInclude(ri => ri.MenuItem)
            .FirstOrDefaultAsync(r => r.ReceiptId == receiptId);

        if (receipt == null)
        {
            TempData["Error"] = "Receipt not found.";
            return RedirectToAction("Index");
        }

        return View(receipt);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error viewing receipt {ReceiptId}", receiptId);
        TempData["Error"] = "An error occurred while loading the receipt.";
        return RedirectToAction("Index");
    }
}

// Additional helper method to get existing receipt by reservation ID
[HttpGet]
public async Task<IActionResult> GetExistingReceipt(int reservationId)
{
    try
    {
        var receipt = await _context.Receipts
            .Include(r => r.ReceiptItems)
            .ThenInclude(ri => ri.MenuItem)
            .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

        if (receipt == null)
        {
            return Json(new { exists = false });
        }

        var receiptData = new
        {
            exists = true,
            receiptId = receipt.ReceiptId,
            totalAmount = receipt.TotalAmount,
            items = receipt.ReceiptItems.Select(ri => new
            {
                itemId = ri.ItemId,
                itemName = ri.MenuItem?.ItemName ?? "Unknown Item",
                quantity = ri.Quantity,
                unitPrice = ri.UnitPrice,
                totalPrice = ri.TotalPrice,
                specialNotes = ri.SpecialNotes
            }).ToList()
        };

        return Json(receiptData);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting existing receipt for reservation {ReservationId}", reservationId);
        return Json(new { error = "Failed to load existing receipt." });
    }
}

    // Helper Methods
    private bool CheckUserLoggedIn()
    {
        return HttpContext.Session.GetString("admin") == "true" ||
               HttpContext.Session.GetString("staff") == "true";
    }

    private IActionResult GoBackToPanel()
    {
        if (HttpContext.Session.GetString("admin") == "true")
            return RedirectToAction("Admin");
        else
            return RedirectToAction("Staff");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}