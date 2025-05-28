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

    public IActionResult Menu()
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
            var staff = await _context.StaffCredentials.FirstOrDefaultAsync(s => s.Username == loginDto.Username &&
                s.Password == loginDto.Password);

            if (user != null)
            {
                HttpContext.Session.SetString("admin", "true");
                HttpContext.Session.SetString("username", user.Username);
                return RedirectToAction("Index");
            }
            else if (staff != null)
            {
                HttpContext.Session.SetString("staff", "true");
                HttpContext.Session.SetString("username", staff.Username);
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
            ViewBag.Error = "Error: " + ex.Message;
            return View();
        }
    }

    // Alternative POST method to maintain compatibility with the existing form
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

    public async Task<IActionResult> Admin()
    {
        // Check if user is logged in
        if (HttpContext.Session.GetString("admin") != "true")
        {
            return RedirectToAction("Login");
        }

        try
        {
            var staffMembers = await _context.Staff.ToListAsync();
            var upcomingReceipts = await _context.Reservations
                .Include(r => r.ServedBy)
                .Include(r => r.Table)
                .Where(r => r.ReservationStatus == "active")
                .Where(r => r.ReservationDate >= DateTime.Today)
                .OrderBy(r => r.ReservationDate)
                .ThenBy(r => r.ReservationHour)
                .ToListAsync();

            var model = new AdminPanelView
            {
                StaffMembers = staffMembers ?? new List<Staff>(),
                UpcomingReceipts = upcomingReceipts ?? new List<Reservation>()
            };

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading admin panel");
            TempData["Error"] = "Error loading admin panel data.";
            return View(new AdminPanelView());
        }
    }

    // public async Task<IActionResult> Staff()
    // {
    //     // Check if user is logged in
    //     if (HttpContext.Session.GetString("staff") != "true")
    //     {
    //         return RedirectToAction("Login");
    //     }
    //
    //     try
    //     {
    //         var staffMembers = await _context.Staff.ToListAsync();
    //         var upcomingReceipts = await _context.Reservations
    //             .Include(r => r.ServedBy)
    //             .Include(r => r.Table)
    //             .Where(r => r.ReservationDate >= DateTime.Today)
    //             .OrderBy(r => r.ReservationDate)
    //             .ThenBy(r => r.ReservationHour)
    //             .ToListAsync();
    //
    //         var model = new AdminPanelView
    //         {
    //             StaffMembers = staffMembers ?? new List<Staff>(),
    //             UpcomingReceipts = upcomingReceipts ?? new List<Reservation>()
    //         };
    //
    //         return View(model);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Error loading staff panel");
    //         TempData["Error"] = "Error loading staff panel data.";
    //         return View(new AdminPanelView());
    //     }
    // }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
    
    // Method to create a user (for testing/setup purposes)
    [HttpPost]
    public async Task<IActionResult> CreateUser(string username, string password)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _context.StaffCredentials
                .FirstOrDefaultAsync(c => c.Username == username);
            
            if (existingUser != null)
            {
                return Json(new { success = false, message = "User already exists" });
            }
            
            var staffCredentials = new StaffCredentials
            {
                Username = username,
                Password = password
            };
            
            _context.StaffCredentials.Add(staffCredentials);
            await _context.SaveChangesAsync();
            
            return Json(new { success = true, message = "User created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", username);
            return Json(new { success = false, message = "Error creating user" });
        }
    }

    // API endpoint for live reservation search (optional enhanced feature)
    [HttpGet]
    public async Task<IActionResult> SearchReservations(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            return Json(new { success = false, message = "Query too short" });
        }

        try
        {
            var reservations = await _context.Reservations
                .Include(r => r.Table)
                .Include(r => r.ServedBy)
                .Where(r => r.ReservationId.ToString().Contains(query) ||
                           r.Name.Contains(query) ||
                           (r.Surname != null && r.Surname.Contains(query)) ||
                           r.TelNo.Contains(query))
                .OrderByDescending(r => r.ReservationDate)
                .Take(5) // Limit to 5 results for performance
                .Select(r => new
                {
                    r.ReservationId,
                    r.Name,
                    r.Surname,
                    r.ReservationDate,
                    r.ReservationHour,
                    r.TableId
                })
                .ToListAsync();

            return Json(new { success = true, results = reservations });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching reservations with query: {Query}", query);
            return Json(new { success = false, message = "Search error occurred" });
        }
    }

    // Action to search for reservations by ID
    [HttpGet]
    public async Task<IActionResult> Receipt(string receiptSearch)
    {
        if (string.IsNullOrWhiteSpace(receiptSearch))
        {
            TempData["Error"] = "Please enter a reservation ID to search.";
            return RedirectToAction("Index");
        }

        try
        {
            // Try to parse as reservation ID
            if (int.TryParse(receiptSearch.Trim(), out int reservationId))
            {
                var reservation = await _context.Reservations
                    .Include(r => r.Table)
                    .Include(r => r.ServedBy)
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

                if (reservation != null)
                {
                    // Redirect to confirmation page with the reservation
                    return RedirectToAction("Confirmation", "Reservation", new { id = reservationId });
                }
            }

            TempData["Error"] = $"No reservation found with ID: {receiptSearch}";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for reservation: {ReservationId}", receiptSearch);
            TempData["Error"] = "An error occurred while searching for the reservation.";
            return RedirectToAction("Index");
        }
    }

    // Get current logged in username
    private string GetCurrentUsername()
    {
        return HttpContext.Session.GetString("username") ?? "";
    }

    // Action to edit reservations (for admin/staff)
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        // Check if user is logged in as admin or staff
        if (HttpContext.Session.GetString("admin") != "true" && 
            HttpContext.Session.GetString("staff") != "true")
        {
            return RedirectToAction("Login");
        }

        try
        {
            var reservation = await _context.Reservations
                .Include(r => r.Table)
                .Include(r => r.ServedBy)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return RedirectToAction("Admin");
            }

            return View(reservation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reservation for edit: {Id}", id);
            TempData["Error"] = "Error loading reservation.";
            return RedirectToAction("Admin");
        }
    }

    
    [HttpPost]
public async Task<IActionResult> UpdateReservationStatus([FromBody] UpdateReservationStatusRequest request)
{
    try
    {
        if (request == null || request.ReservationId <= 0 || string.IsNullOrWhiteSpace(request.Status))
        {
            return Json(new { success = false, message = "Invalid request data." });
        }

        // Validate status values
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

        // Check if reservation is already processed
        if (reservation.ReservationStatus != "active")
        {
            return Json(new { success = false, message = "This reservation has already been processed." });
        }

        // Update the status
        reservation.ReservationStatus = request.Status.ToLower();
        await _context.SaveChangesAsync();

        var statusText = request.Status.ToLower() == "checked-in" ? "checked in" : "cancelled";
        var message = $"Reservation #{reservation.ReservationId} has been {statusText} successfully.";

        _logger.LogInformation("Reservation {ReservationId} status updated to {Status}", 
            request.ReservationId, request.Status);

        return Json(new { success = true, message = message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating reservation status for ID: {ReservationId}", 
            request.ReservationId);
        return Json(new { success = false, message = "An error occurred while updating the reservation." });
    }
}

// Update your Staff action to only show active reservations
public async Task<IActionResult> Staff()
{
    try
    {
        // Only get active reservations for upcoming dates
        var upcomingReservations = await _context.Reservations
            .Include(r => r.Table)
            .Include(r => r.ServedBy)
            .Where(r => r.ReservationStatus == "active" && 
                       r.ReservationDate >= DateTime.Today)
            .OrderBy(r => r.ReservationDate)
            .ThenBy(r => r.ReservationHour)
            .ToListAsync();

        var adminPanelView = new AdminPanelView
        {
            UpcomingReceipts = upcomingReservations
        };

        return View(adminPanelView);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading staff page");
        TempData["Error"] = "An error occurred while loading reservations.";
        return View(new AdminPanelView { UpcomingReceipts = new List<Reservation>() });
    }
}

// Add this class to your Models folder or at the end of your controller
public class UpdateReservationStatusRequest
{
    public int ReservationId { get; set; }
    public string Status { get; set; }
}
// Add these methods to your HomeController.cs

// GET: Edit reservation

// POST: Update reservation
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(Reservation model)
{
    // Check if user is logged in as admin or staff
    if (HttpContext.Session.GetString("admin") != "true" && 
        HttpContext.Session.GetString("staff") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        // Remove validation for fields that might not be present
        ModelState.Remove("Table");
        ModelState.Remove("ServedBy");

        if (!ModelState.IsValid)
        {
            // Reload the reservation with includes if validation fails
            var reservationWithIncludes = await _context.Reservations
                .Include(r => r.Table)
                .Include(r => r.ServedBy)
                .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);
            
            if (reservationWithIncludes != null)
            {
                // Copy the navigation properties
                model.Table = reservationWithIncludes.Table;
                model.ServedBy = reservationWithIncludes.ServedBy;
            }
            
            TempData["Error"] = "Please correct the errors and try again.";
            return View(model);
        }

        // Get the existing reservation from database
        var existingReservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);

        if (existingReservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return RedirectToAction("Staff");
        }

        // Check if trying to modify a non-active reservation's core details
        if (existingReservation.ReservationStatus != "active")
        {
            // Only allow name, phone, and special requests to be updated for non-active reservations
            existingReservation.Name = model.Name?.Trim();
            existingReservation.Surname = model.Surname?.Trim();
            existingReservation.TelNo = model.TelNo?.Trim();
            
            TempData["Success"] = "Contact information updated successfully.";
        }
        else
        {
            // For active reservations, check for conflicts if changing table, date, or time
            if (existingReservation.TableId != model.TableId ||
                existingReservation.ReservationDate != model.ReservationDate ||
                existingReservation.ReservationHour != model.ReservationHour)
            {
                // Check for conflicts with the new table/date/time
                var conflictingReservation = await _context.Reservations
                    .Where(r => r.ReservationId != model.ReservationId &&
                               r.TableId == model.TableId &&
                               r.ReservationDate.Date == model.ReservationDate.Date &&
                               r.ReservationHour == model.ReservationHour &&
                               r.ReservationStatus == "active")
                    .FirstOrDefaultAsync();

                if (conflictingReservation != null)
                {
                    TempData["Error"] = "The selected table is already reserved for that date and time.";
                    
                    // Reload with includes for the view
                    model.Table = await _context.RestaurantTables
                        .Include(t => t.ServedBy)
                        .FirstOrDefaultAsync(t => t.TableId == existingReservation.TableId);
                    model.ServedBy = model.Table?.ServedBy;
                    
                    return View(model);
                }
            }

            // Update all fields for active reservations
            existingReservation.Name = model.Name?.Trim();
            existingReservation.Surname = model.Surname?.Trim();
            existingReservation.TelNo = model.TelNo?.Trim();
            existingReservation.GuestNumber = model.GuestNumber;
            existingReservation.TableId = model.TableId;
            existingReservation.ReservationDate = model.ReservationDate;
            existingReservation.ReservationHour = model.ReservationHour;
            
            // Update served_by if table changed
            if (existingReservation.TableId != model.TableId)
            {
                var newTable = await _context.RestaurantTables
                    .FirstOrDefaultAsync(t => t.TableId == model.TableId);
                if (newTable != null)
                {
                    existingReservation.ServedById = newTable.ServedById;
                }
            }

            TempData["Success"] = "Reservation updated successfully.";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reservation {ReservationId} updated by {User}", 
            model.ReservationId, GetCurrentUsername());

        return RedirectToAction("Staff");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating reservation: {Id}", model.ReservationId);
        TempData["Error"] = "An error occurred while updating the reservation.";
        
        // Reload with includes for the view
        var reservationWithIncludes = await _context.Reservations
            .Include(r => r.Table)
            .Include(r => r.ServedBy)
            .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);
        
        if (reservationWithIncludes != null)
        {
            model.Table = reservationWithIncludes.Table;
            model.ServedBy = reservationWithIncludes.ServedBy;
        }
        
        return View(model);
    }
}

// GET: Delete reservation (with confirmation)
[HttpGet]
public async Task<IActionResult> Delete(int id)
{
    // Check if user is logged in as admin or staff
    if (HttpContext.Session.GetString("admin") != "true" && 
        HttpContext.Session.GetString("staff") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var reservation = await _context.Reservations
            .Include(r => r.Table)
            .Include(r => r.ServedBy)
            .FirstOrDefaultAsync(r => r.ReservationId == id);

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return RedirectToAction("Staff");
        }

        // Only allow deletion of active reservations
        if (reservation.ReservationStatus != "active")
        {
            TempData["Error"] = "Only active reservations can be deleted.";
            return RedirectToAction("Staff");
        }

        return View(reservation);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading reservation for deletion: {Id}", id);
        TempData["Error"] = "Error loading reservation.";
        return RedirectToAction("Staff");
    }
}

// POST: Confirm delete reservation
[HttpPost, ActionName("Delete")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteConfirmed(int id)
{
    // Check if user is logged in as admin or staff
    if (HttpContext.Session.GetString("admin") != "true" && 
        HttpContext.Session.GetString("staff") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.ReservationId == id);

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return RedirectToAction("Staff");
        }

        // Only allow deletion of active reservations
        if (reservation.ReservationStatus != "active")
        {
            TempData["Error"] = "Only active reservations can be deleted.";
            return RedirectToAction("Staff");
        }

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reservation {ReservationId} deleted by {User}", 
            id, GetCurrentUsername());

        TempData["Success"] = $"Reservation #{id} has been permanently deleted.";
        return RedirectToAction("Staff");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error deleting reservation: {Id}", id);
        TempData["Error"] = "An error occurred while deleting the reservation.";
        return RedirectToAction("Staff");
    }
}

// API endpoint to get available tables for a specific date/time (for edit form)
[HttpGet]
public async Task<IActionResult> GetAvailableTables(DateTime date, string time, int currentReservationId)
{
    try
    {
        var availableTables = await _context.RestaurantTables
            .Include(t => t.ServedBy)
            .Where(t => !_context.Reservations.Any(r => 
                r.TableId == t.TableId && 
                r.ReservationDate.Date == date.Date && 
                r.ReservationHour == time &&
                r.ReservationStatus == "active" &&
                r.ReservationId != currentReservationId)) // Exclude current reservation
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
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting available tables");
        return Json(new { success = false, message = "Error retrieving available tables" });
    }
}

// API endpoint to get staff members (for served_by dropdown)
[HttpGet]
public async Task<IActionResult> GetStaffMembers()
{
    try
    {
        var staffMembers = await _context.Staff
            .Where(s => s.Job == "waiter") // Only waiters can serve tables
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
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting staff members");
        return Json(new { success = false, message = "Error retrieving staff members" });
    }
}
// Add these methods to your HomeController.cs for Staff Management

// GET: Create new staff member
[HttpGet]
public IActionResult CreateStaff()
{
    // Check if user is logged in as admin
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    return View();
}

// POST: Create new staff member
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> CreateStaff(Staff staff)
{
    // Check if user is logged in as admin
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
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

        // Validate job position
        var validJobs = new[] { "waiter", "chef", "manager", "cashier" };
        if (!validJobs.Contains(staff.Job.ToLower()))
        {
            ModelState.AddModelError("Job", "Please select a valid job position.");
            return View(staff);
        }

        // Set creation date if your model has it
        // staff.CreatedAt = DateTime.Now;

        _context.Staff.Add(staff);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New staff member created: {Name} {Surname} by {User}", 
            staff.Name, staff.Surname, GetCurrentUsername());

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully added.";
        return RedirectToAction("Admin");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating staff member: {Name} {Surname}", staff.Name, staff.Surname);
        TempData["Error"] = "An error occurred while adding the staff member. Please try again.";
        return View(staff);
    }
}

// GET: Edit staff member
[HttpGet]
public async Task<IActionResult> EditStaff(int id)
{
    // Check if user is logged in as admin
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var staff = await _context.Staff.FindAsync(id);
        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }

        return View(staff);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading staff member for edit: {Id}", id);
        TempData["Error"] = "Error loading staff member.";
        return RedirectToAction("Admin");
    }
}

// POST: Edit staff member
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EditStaff(Staff staff)
{
    // Check if user is logged in as admin
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        if (!ModelState.IsValid)
        {
            return View(staff);
        }

        // Check if phone number already exists (excluding current staff member)
        var existingStaff = await _context.Staff
            .FirstOrDefaultAsync(s => s.TelNo == staff.TelNo && s.StaffId != staff.StaffId);

        if (existingStaff != null)
        {
            ModelState.AddModelError("TelNo", "Another staff member with this phone number already exists.");
            return View(staff);
        }

        // Validate job position
        var validJobs = new[] { "waiter", "chef", "manager", "cashier" };
        if (!validJobs.Contains(staff.Job.ToLower()))
        {
            ModelState.AddModelError("Job", "Please select a valid job position.");
            return View(staff);
        }

        _context.Update(staff);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Staff member updated: {Name} {Surname} by {User}", 
            staff.Name, staff.Surname, GetCurrentUsername());

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully updated.";
        return RedirectToAction("Admin");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating staff member: {Id}", staff.StaffId);
        TempData["Error"] = "An error occurred while updating the staff member. Please try again.";
        return View(staff);
    }
}

// GET: Staff details
[HttpGet]
public async Task<IActionResult> StaffDetails(int id)
{
    // Check if user is logged in as admin
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var staff = await _context.Staff
            .FirstOrDefaultAsync(s => s.StaffId == id);

        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }

        // Get reservations served by this staff member
        var reservationsServed = await _context.Reservations
            .Include(r => r.Table)
            .Where(r => r.ServedById == id)
            .OrderByDescending(r => r.ReservationDate)
            .Take(10) // Last 10 reservations
            .ToListAsync();

        ViewBag.ReservationsServed = reservationsServed;
        return View(staff);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading staff details: {Id}", id);
        TempData["Error"] = "Error loading staff details.";
        return RedirectToAction("Admin");
    }
}

// GET: Delete staff confirmation
[HttpGet]
public async Task<IActionResult> DeleteStaff(int id)
{
    // Check if user is logged in as admin
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var staff = await _context.Staff
            .FirstOrDefaultAsync(s => s.StaffId == id);

        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }

        // Check if staff member has active reservations
        var activeReservations = await _context.Reservations
            .CountAsync(r => r.ServedById == id && r.ReservationStatus == "active");

        ViewBag.ActiveReservations = activeReservations;
        return View(staff);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading staff for deletion: {Id}", id);
        TempData["Error"] = "Error loading staff member.";
        return RedirectToAction("Admin");
    }
}

// POST: Delete staff confirmed
[HttpPost, ActionName("DeleteStaff")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteStaffConfirmed(int id)
{
    // Check if user is logged in as admin
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var staff = await _context.Staff.FindAsync(id);
        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }

        // Check if staff member has active reservations
        var activeReservations = await _context.Reservations
            .CountAsync(r => r.ServedById == id && r.ReservationStatus == "active");

        if (activeReservations > 0)
        {
            TempData["Error"] = $"Cannot delete staff member. They have {activeReservations} active reservation(s). Please reassign or complete these reservations first.";
            return RedirectToAction("Admin");
        }

        // Update any tables assigned to this staff member to unassigned
        var assignedTables = await _context.RestaurantTables
            .Where(t => t.ServedById == id)
            .ToListAsync();
        

        _context.Staff.Remove(staff);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Staff member deleted: {Name} {Surname} by {User}", 
            staff.Name, staff.Surname, GetCurrentUsername());

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully deleted.";
        return RedirectToAction("Admin");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error deleting staff member: {Id}", id);
        TempData["Error"] = "An error occurred while deleting the staff member. Please try again.";
        return RedirectToAction("Admin");
    }
}

// API: Get staff statistics
[HttpGet]
public async Task<IActionResult> GetStaffStatistics()
{
    // Check if user is logged in as admin
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return Json(new { success = false, message = "Unauthorized" });
    }

    try
    {
        var totalStaff = await _context.Staff.CountAsync();
        var totalWaiters = await _context.Staff.CountAsync(s => s.Job.ToLower() == "waiter");
        var totalChefs = await _context.Staff.CountAsync(s => s.Job.ToLower() == "chef");
        var totalManagers = await _context.Staff.CountAsync(s => s.Job.ToLower() == "manager");
        var totalCashiers = await _context.Staff.CountAsync(s => s.Job.ToLower() == "cashier");

        var activeReservations = await _context.Reservations
            .CountAsync(r => r.ReservationStatus == "active");

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
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting staff statistics");
        return Json(new { success = false, message = "Error retrieving statistics" });
    }
}
// Update these methods in your HomeController.cs

[HttpGet]
public async Task<IActionResult> Booking()
{
    try
    {
        // Get all tables with their server assignments
        var tables = await _context.RestaurantTables  // Changed from RestaurantTables to RestaurantTable
            .Include(t => t.ServedBy)
            .OrderBy(t => t.TableId)
            .ToListAsync();

        // Get today's reservations to show current availability
        var today = DateTime.Today;
        var todayReservations = await _context.Reservations
            .Where(r => r.ReservationDate.Date == today && r.ReservationStatus == "active")
            .Select(r => new { r.TableId, r.ReservationHour })
            .ToListAsync();

        // Group occupied tables by hour for today
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
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading booking page");
        TempData["Error"] = "Error loading booking information.";
        return View(new BookingViewModel());
    }
}

// API endpoint to get occupied tables for a specific date and time
[HttpGet]
public async Task<IActionResult> GetOccupiedTables(string date, string time)
{
    try
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
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting occupied tables for {Date} {Time}", date, time);
        return Json(new { success = false, message = "Error retrieving table availability" });
    }
}

// API endpoint to get table capacities
[HttpGet]
public async Task<IActionResult> GetTableCapacities()
{
    try
    {
        var tableCapacities = await _context.RestaurantTables  // Changed from RestaurantTables to RestaurantTable
            .OrderBy(t => t.TableId)
            .Select(t => new { t.TableId, t.MinCapacity, t.MaxCapacity })
            .ToListAsync();

        return Json(new { success = true, tableCapacities });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting table capacities");
        return Json(new { success = false, message = "Error retrieving table information" });
    }
}

}