using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Restaurant.Models;
using System.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Restaurant.Controllers
{
    public class HomeController : Controller
    {
        private readonly RestaurantContext _context;

        public HomeController(RestaurantContext context)
        {
            _context = context;
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
        public async Task<IActionResult> Login(string username, string password)
        {
            try
            {
                // Use Entity Framework instead of raw SQL to avoid connection issues
                var user = await _context.UserCredentials
                    .Where(u => u.Username == username && u.Password == password)
                    .FirstOrDefaultAsync();

                if (user != null)
                {
                    if (user.UserType == "admin")
                    {
                        HttpContext.Session.SetString("admin", "true");
                        HttpContext.Session.SetString("username", user.Username);
                    }
                    else if (user.UserType == "staff")
                    {
                        HttpContext.Session.SetString("staff", "true");
                        HttpContext.Session.SetString("username", user.Username);
                    }
                    return RedirectToAction("Index");
                }

                ViewData["Error"] = "Invalid username or password";
                return View();
            }
            catch (Exception ex)
            {
                ViewData["Error"] = "Login failed: " + ex.Message;
                return View();
            }
        }

        public async Task<IActionResult> Admin()
        {
            if (HttpContext.Session.GetString("admin")!= "true")
            {
                return RedirectToAction("Login");
            }

            try
            {
                // Get staff members using Entity Framework
                var staffMembers = await _context.Staff.ToListAsync();
                



                // Calculate statistics using Entity Framework
                var totalStaff = await _context.Staff.CountAsync();
                var totalWaiters = await _context.Staff.Where(s => s.Job.ToLower() == "waiter").CountAsync();
                var activeReservations = await _context.ReservationDetails.Where(rd => rd.ReservationStatus == "active").CountAsync();

                var viewModel = new AdminPanelView
                {
                    StaffMembers = staffMembers,
                    TotalStaff = totalStaff,
                    TotalWaiters = totalWaiters,
                    ActiveReservations = activeReservations,
                    UpcomingReservations = new List<Reservation>()
                };

                // Get active reservations using Entity Framework
                var today = DateTime.Today;
                var activeReservationsList = await _context.Reservations
                    .Include(r => r.Table)
                    .ThenInclude(t => t.ServedBy)
                    .Include(r => r.Customer)
                    .Include(r => r.ReservationDetail)
                    .Where(r => r.ReservationDetail.ReservationStatus == "active" && r.ReservationDetail.ReservationDate >= today)
                    .ToListAsync();
                    
                // Sort manually after retrieval
                activeReservationsList.Sort((r1, r2) => {
                    int dateComparison = r1.ReservationDetail.ReservationDate.CompareTo(r2.ReservationDetail.ReservationDate);
                    return dateComparison != 0 ? dateComparison : string.Compare(r1.ReservationDetail.ReservationHour, r2.ReservationDetail.ReservationHour, StringComparison.OrdinalIgnoreCase);
                });

                viewModel.UpcomingReservations = activeReservationsList;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading admin panel: " + ex.Message;
                return View(new AdminPanelView 
                { 
                    StaffMembers = new List<Staff>(),
                    UpcomingReservations = new List<Reservation>()
                });
            }
        }

        public async Task<IActionResult> Staff()
        {
            if (HttpContext.Session.GetString("staff") != "true")
            {
                return RedirectToAction("Login");
            }

            try
            {
                // Get active reservations using Entity Framework
                var today = DateTime.Today;
                var activeReservations = await _context.Reservations
                    .Include(r => r.Table)
                    .ThenInclude(t => t.ServedBy)
                    .Include(r => r.Customer)
                    .Include(r => r.ReservationDetail)
                    .Where(r => r.ReservationDetail.ReservationStatus == "active" && r.ReservationDetail.ReservationDate >= today)
                    .ToListAsync();
                    
                // Sort manually after retrieval
                activeReservations.Sort((r1, r2) => {
                    int dateComparison = r1.ReservationDetail.ReservationDate.CompareTo(r2.ReservationDetail.ReservationDate);
                    return dateComparison != 0 ? dateComparison : string.Compare(r1.ReservationDetail.ReservationHour, r2.ReservationDetail.ReservationHour, StringComparison.OrdinalIgnoreCase);
                });

                var viewModel = new AdminPanelView
                {
                    UpcomingReservations = activeReservations,
                    StaffMembers = new List<Staff>()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading staff panel: " + ex.Message;
                return View(new AdminPanelView 
                { 
                    UpcomingReservations = new List<Reservation>(),
                    StaffMembers = new List<Staff>()
                });
            }
        }

        public async Task<IActionResult> Booking(int? tableId, string? date, string? time, int? guests)
        {
            try
            {
                // Get tables using Entity Framework
                var tables = await _context.RestaurantTables
                    .Include(t => t.ServedBy)
                    .ToListAsync();
                    
                // Sort manually after retrieval
                tables.Sort((t1, t2) => t1.TableId.CompareTo(t2.TableId));

                var bookingViewModel = new BookingViewModel
                {
                    Tables = tables,
                    SelectedTableId = tableId,
                    SelectedDate = date,
                    SelectedTime = time,
                    SelectedGuests = guests
                };

                return View(bookingViewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error loading booking page: " + ex.Message;
                return View(new BookingViewModel { Tables = new List<RestaurantTable>() });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateReservationStatus(int reservationId, string status)
        {
            try
            {
                // Validate status parameter
                if (string.IsNullOrEmpty(status))
                {
                    return Json(new { success = false, message = "Status parameter is required" });
                }

                // Clean the status parameter (remove quotes if present)
                status = status.Trim().Trim('\'', '"');
        
                if (status != "cancelled" && status != "checked-in")
                {
                    return Json(new { success = false, message = $"Invalid status value: {status}. Expected 'cancelled' or 'checked-in'" });
                }

                // Update the reservation status using raw SQL
                var updateQuery = @"
            UPDATE reservation_details 
            SET reservation_status = {0} 
            WHERE res_details_id = (
                SELECT r.res_details_id 
                FROM reservations r 
                WHERE r.reservation_id = {1}
            )";

                var rowsAffected = await _context.Database.ExecuteSqlRawAsync(updateQuery, status, reservationId);

                if (rowsAffected > 0)
                {
                    TempData["Success"] = $"Reservation {status} successfully!";
                    return Json(new { success = true });
                }
                else
                {
                    return Json(new { success = false, message = "Reservation not found or status not updated" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error updating reservation: {ex.Message}" });
            }
        }
        public async Task<IActionResult> OrderManagement(int reservationId)
        {
            try
            {
                // Get reservation with related data using Entity Framework
                var reservation = await _context.Reservations
                    .Include(r => r.Table)
                    .ThenInclude(t => t.ServedBy)
                    .Include(r => r.Customer)
                    .Include(r => r.ReservationDetail)
                    .Where(r => r.ReservationId == reservationId)
                    .FirstOrDefaultAsync();

                if (reservation == null)
                {
                    TempData["Error"] = "Reservation not found";
                    return RedirectToAction("Staff");
                }

                // Get all menu items using Entity Framework
                var menuItems = await _context.MenuItems.ToListAsync();
                    
                // Sort manually after retrieval
                menuItems.Sort((m1, m2) => {
                    int categoryComparison = string.Compare(m1.Category, m2.Category, StringComparison.OrdinalIgnoreCase);
                    if (categoryComparison != 0) return categoryComparison;
                    
                    int subcategoryComparison = string.Compare(m1.Subcategory ?? "", m2.Subcategory ?? "", StringComparison.OrdinalIgnoreCase);
                    if (subcategoryComparison != 0) return subcategoryComparison;
                    
                    return string.Compare(m1.ItemName, m2.ItemName, StringComparison.OrdinalIgnoreCase);
                });

                // Check for existing receipt
                var existingReceipt = await _context.Receipts
                    .Include(r => r.ReceiptItems)
                    .ThenInclude(ri => ri.MenuItem)
                    .Where(r => r.ReservationId == reservationId)
                    .FirstOrDefaultAsync();

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
                TempData["Error"] = "Error loading order management: " + ex.Message;
                return RedirectToAction("Staff");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessOrder([FromBody] OrderSubmissionModel orderData)
        {
            try
            {
                if (orderData?.Items == null || orderData.Items.Count == 0)
                {
                    return Json(new { success = false, message = "No items in order" });
                }

                // Check if receipt already exists
                var existingReceipt = await _context.Receipts
                    .Where(r => r.ReservationId == orderData.ReservationId)
                    .FirstOrDefaultAsync();

                if (existingReceipt != null)
                {
                    // Update existing receipt
                    existingReceipt.TotalAmount = orderData.TotalAmount;
                    
                    // Delete existing receipt items
                    var existingItems = await _context.ReceiptItems
                        .Where(ri => ri.ReceiptId == existingReceipt.ReceiptId)
                        .ToListAsync();
                    
                    _context.ReceiptItems.RemoveRange(existingItems);
                    await _context.SaveChangesAsync();

                    // Add new receipt items
                    foreach (var item in orderData.Items)
                    {
                        var receiptItem = new ReceiptItem
                        {
                            ReceiptId = existingReceipt.ReceiptId,
                            ItemId = item.ItemId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            SpecialNotes = orderData.SpecialNotes ?? ""
                        };
                        _context.ReceiptItems.Add(receiptItem);
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Order updated successfully!";
                }
                else
                {
                    // Create new receipt
                    var receipt = new Receipt
                    {
                        ReservationId = orderData.ReservationId,
                        StaffId = orderData.StaffId,
                        TotalAmount = orderData.TotalAmount,
                        CreatedAt = DateTime.Now
                    };

                    _context.Receipts.Add(receipt);
                    await _context.SaveChangesAsync();

                    // Add receipt items
                    foreach (var item in orderData.Items)
                    {
                        var receiptItem = new ReceiptItem
                        {
                            ReceiptId = receipt.ReceiptId,
                            ItemId = item.ItemId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            SpecialNotes = orderData.SpecialNotes ?? ""
                        };
                        _context.ReceiptItems.Add(receiptItem);
                    }

                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Order created successfully!";
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // API Methods for Booking Page JavaScript
        [HttpGet]
        public async Task<IActionResult> GetTableCapacities()
        {
            try
            {
                var tables = await _context.RestaurantTables.ToListAsync();
                
                var tableCapacities = new List<object>();
                foreach (var table in tables)
                {
                    tableCapacities.Add(new
                    {
                        tableId = table.TableId,
                        minCapacity = table.MinCapacity,
                        maxCapacity = table.MaxCapacity,
                        servedById = table.ServedById
                    });
                }

                return Json(new { success = true, tableCapacities = tableCapacities });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        } 
        
        [HttpGet]
public async Task<IActionResult> Edit(int id)
{
    try
    {
        // Use Entity Framework instead of raw SQL
        var reservation = await _context.Reservations
            .Include(r => r.Table)
            .ThenInclude(t => t.ServedBy)
            .Include(r => r.Customer)
            .Include(r => r.ReservationDetail)
            .FirstOrDefaultAsync(r => r.ReservationId == id);

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return GoBackToPanel();
        }

        return View(reservation);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in Edit GET: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        TempData["Error"] = $"An error occurred while loading the reservation: {ex.Message}";
        return GoBackToPanel();
    }
}

[HttpPost]
public async Task<IActionResult> Edit(Reservation model)
{
    // Remove validation for navigation properties
    ModelState.Remove("Customer");
    ModelState.Remove("Table");
    ModelState.Remove("ReservationDetail");

    try
    {
        // Load the existing reservation using Entity Framework
        var reservation = await _context.Reservations
            .Include(r => r.Customer)
            .Include(r => r.ReservationDetail)
            .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return GoBackToPanel();
        }

        // Get form values with null checks and trimming
        var customerName = Request.Form["Customer.Name"].ToString()?.Trim() ?? "";
        var customerSurname = Request.Form["Customer.Surname"].ToString()?.Trim() ?? "";
        var customerTelNo = Request.Form["Customer.TelNo"].ToString()?.Trim() ?? "";
        var customerEmail = Request.Form["Customer.Email"].ToString()?.Trim();
        
        // Set empty email to null
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            customerEmail = null;
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(customerName))
        {
            TempData["Error"] = "Customer name is required.";
            return View(reservation);
        }
        
        if (string.IsNullOrWhiteSpace(customerSurname))
        {
            TempData["Error"] = "Customer surname is required.";
            return View(reservation);
        }
        
        if (string.IsNullOrWhiteSpace(customerTelNo))
        {
            TempData["Error"] = "Customer phone number is required.";
            return View(reservation);
        }

        // Update customer information
        if (reservation.Customer != null)
        {
            reservation.Customer.Name = customerName;
            reservation.Customer.Surname = customerSurname;
            reservation.Customer.TelNo = customerTelNo;
            reservation.Customer.Email = customerEmail;
        }

        // Update reservation details
        if (reservation.ReservationDetail != null)
        {
            var specialRequests = Request.Form["ReservationDetail.SpecialRequests"].ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(specialRequests))
            {
                specialRequests = null;
            }

            // Always update special requests
            reservation.ReservationDetail.SpecialRequests = specialRequests;

            // Only update other reservation details if status is active
            if (reservation.ReservationDetail.ReservationStatus == "active")
            {
                // Parse and validate form values
                var guestNumberStr = Request.Form["ReservationDetail.GuestNumber"].ToString()?.Trim() ?? "";
                if (!int.TryParse(guestNumberStr, out int guestNumber) || guestNumber < 1)
                {
                    TempData["Error"] = $"Invalid guest number format. Received: '{guestNumberStr}'";
                    return View(reservation);
                }

                // Get table ID with better error handling
                int tableId = reservation.TableId; // Default to current table
                var tableIdFormValue = Request.Form["TableId"].ToString()?.Trim() ?? "";
                
                if (string.IsNullOrEmpty(tableIdFormValue))
                {
                    var currentTableIdValue = Request.Form["CurrentTableId"].ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(currentTableIdValue))
                    {
                        if (!int.TryParse(currentTableIdValue, out tableId))
                        {
                            TempData["Error"] = $"Invalid current table ID format. Received: '{currentTableIdValue}'";
                            return View(reservation);
                        }
                    }
                }
                else
                {
                    if (!int.TryParse(tableIdFormValue, out tableId))
                    {
                        TempData["Error"] = $"Invalid table ID format. Received: '{tableIdFormValue}'";
                        return View(reservation);
                    }
                }

                var reservationDateStr = Request.Form["ReservationDetail.ReservationDate"].ToString()?.Trim() ?? "";
                if (!DateTime.TryParse(reservationDateStr, out DateTime reservationDate))
                {
                    TempData["Error"] = $"Invalid reservation date format. Received: '{reservationDateStr}'";
                    return View(reservation);
                }

                var reservationHour = Request.Form["ReservationDetail.ReservationHour"].ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(reservationHour))
                {
                    TempData["Error"] = "Reservation hour is required.";
                    return View(reservation);
                }

                // Validate the selected table exists and can accommodate the guest count
                var selectedTable = await _context.RestaurantTables
                    .Include(t => t.ServedBy)
                    .FirstOrDefaultAsync(t => t.TableId == tableId);

                if (selectedTable == null)
                {
                    TempData["Error"] = $"Selected table {tableId} not found.";
                    return View(reservation);
                }

                if (guestNumber < selectedTable.MinCapacity || guestNumber > selectedTable.MaxCapacity)
                {
                    TempData["Error"] = $"Table {tableId} capacity is {selectedTable.MinCapacity}-{selectedTable.MaxCapacity} people. You selected {guestNumber} guests.";
                    return View(reservation);
                }

                // Check if the table is available for the selected date/time (excluding current reservation)
                var conflictingReservation = await _context.Reservations
                    .Include(r => r.ReservationDetail)
                    .FirstOrDefaultAsync(r => 
                        r.TableId == tableId &&
                        r.ReservationDetail.ReservationDate.Date == reservationDate.Date &&
                        r.ReservationDetail.ReservationHour == reservationHour &&
                        r.ReservationDetail.ReservationStatus == "active" &&
                        r.ReservationId != model.ReservationId);

                if (conflictingReservation != null)
                {
                    TempData["Error"] = $"Table {tableId} is already reserved for {reservationDate.ToShortDateString()} at {reservationHour}.";
                    return View(reservation);
                }

                // Check if date is not in the past
                if (reservationDate.Date < DateTime.Today)
                {
                    TempData["Error"] = "Cannot set reservation date to a past date.";
                    return View(reservation);
                }

                // Update reservation details
                reservation.ReservationDetail.GuestNumber = guestNumber;
                reservation.ReservationDetail.ReservationDate = reservationDate;
                reservation.ReservationDetail.ReservationHour = reservationHour;

                // Update table assignment if it changed
                if (tableId != reservation.TableId)
                {
                    reservation.TableId = tableId;
                }
            }
        }

        // Save all changes using Entity Framework
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Reservation #{model.ReservationId} has been updated successfully.";
        return GoBackToPanel();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in Edit POST: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        TempData["Error"] = $"An error occurred while updating the reservation: {ex.Message}";
        
        // Reload the reservation for the view
        var reservationForView = await _context.Reservations
            .Include(r => r.Table)
            .ThenInclude(t => t.ServedBy)
            .Include(r => r.Customer)
            .Include(r => r.ReservationDetail)
            .FirstOrDefaultAsync(r => r.ReservationId == model.ReservationId);
            
        return View(reservationForView ?? new Reservation());
    }
}

// Also update the Delete method to use Entity Framework
[HttpGet]
public async Task<IActionResult> Delete(int id)
{
    if (!CheckUserLoggedIn())
    {
        return RedirectToAction("Login");
    }

    try
    {
        // Use Entity Framework instead of raw SQL
        var reservation = await _context.Reservations
            .Include(r => r.ReservationDetail)
            .FirstOrDefaultAsync(r => r.ReservationId == id);

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return GoBackToPanel();
        }

        if (reservation.ReservationDetail?.ReservationStatus != "active")
        {
            TempData["Error"] = "Only active reservations can be deleted.";
            return RedirectToAction("Edit", new { id = id });
        }

        // Delete reservation and its details using Entity Framework
        if (reservation.ReservationDetail != null)
        {
            _context.ReservationDetails.Remove(reservation.ReservationDetail);
        }
        _context.Reservations.Remove(reservation);
        
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Reservation #{id} has been permanently deleted.";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error deleting reservation: {ex.Message}");
        TempData["Error"] = "An error occurred while deleting the reservation. Please try again.";
    }

    return GoBackToPanel();
}

// Update EditStaff methods to use Entity Framework
[HttpGet]
public async Task<IActionResult> EditStaff(int id)
{
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.StaffId == id);

        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }
        
        return View(staff);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading staff: {ex.Message}");
        TempData["Error"] = "An error occurred while loading the staff member.";
        return RedirectToAction("Admin");
    }
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

    try
    {
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

        // Update staff member using Entity Framework
        _context.Staff.Update(staff);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully updated.";
        return RedirectToAction("Admin");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating staff: {ex.Message}");
        TempData["Error"] = "An error occurred while updating the staff member.";
        return View(staff);
    }
}

[HttpGet]
public async Task<IActionResult> DeleteStaff(int id)
{
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.StaffId == id);

        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }

        // Check if staff has active reservations using Entity Framework
        var activeReservations = await _context.Reservations
            .Include(r => r.Table)
            .Include(r => r.ReservationDetail)
            .CountAsync(r => 
                r.Table.ServedById == id && 
                r.ReservationDetail.ReservationStatus == "active");

        ViewBag.ActiveReservations = activeReservations;
        return View(staff);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading staff for deletion: {ex.Message}");
        TempData["Error"] = "An error occurred while loading the staff member.";
        return RedirectToAction("Admin");
    }
}

[HttpPost, ActionName("DeleteStaff")]
public async Task<IActionResult> DeleteStaffConfirmed(int id)
{
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        var staff = await _context.Staff.FirstOrDefaultAsync(s => s.StaffId == id);

        if (staff == null)
        {
            TempData["Error"] = "Staff member not found.";
            return RedirectToAction("Admin");
        }

        // Check if staff has active reservations
        var activeReservations = await _context.Reservations
            .Include(r => r.Table)
            .Include(r => r.ReservationDetail)
            .CountAsync(r => 
                r.Table.ServedById == id && 
                r.ReservationDetail.ReservationStatus == "active");

        if (activeReservations > 0)
        {
            TempData["Error"] = $"Cannot delete staff member. They have {activeReservations} active reservation(s). Please reassign or complete these reservations first.";
            return RedirectToAction("Admin");
        }

        // Delete staff member using Entity Framework
        _context.Staff.Remove(staff);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully deleted.";
        return RedirectToAction("Admin");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error deleting staff: {ex.Message}");
        TempData["Error"] = "An error occurred while deleting the staff member.";
        return RedirectToAction("Admin");
    }
}

[HttpGet]
public async Task<IActionResult> CreateUser()
{
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }
    return View();
}

[HttpPost]
public async Task<IActionResult> CreateUser(string username, string password, string type)
{
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    try
    {
        // Check if user already exists using Entity Framework
        var existingUser = await _context.UserCredentials
            .FirstOrDefaultAsync(u => u.Username == username);

        if (existingUser != null)
        {
            TempData["UserExists"] = "User already exists.";
            return RedirectToAction("CreateUser");
        }

        // Create new user using Entity Framework
        var newUser = new UserCredential
        {
            Username = username,
            Password = password,
            UserType = type
        };

        _context.UserCredentials.Add(newUser);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"User {username} has been successfully created.";
        return RedirectToAction("Admin");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating user: {ex.Message}");
        TempData["Error"] = "An error occurred while creating the user.";
        return RedirectToAction("Admin");
    }
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
                    .ThenInclude(t => t.ServedBy)
                    .Include(r => r.Customer)
                    .Include(r => r.ReservationDetail)
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId);

                if (reservation != null)
                {
                    return RedirectToAction("Confirmation", "Reservation", new { id = reservationId });
                }
            }

            TempData["Error"] = $"No reservation found with ID: {receiptSearch}";
            return RedirectToAction("Index");
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
        public async Task<IActionResult> GetOccupiedTables(string date, string time)
        {
            try
            {
                if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time))
                {
                    return Json(new { success = true, occupiedTables = new List<int>() });
                }

                var reservationDate = DateTime.Parse(date);
                var occupiedTables = await _context.Reservations
                    .Include(r => r.ReservationDetail)
                    .Where(r => r.ReservationDetail.ReservationDate.Date == reservationDate.Date)
                    .Where(r => r.ReservationDetail.ReservationHour == time)
                    .Where(r => r.ReservationDetail.ReservationStatus == "active")
                    .Select(r => r.TableId)
                    .Distinct()
                    .ToListAsync();

                return Json(new { success = true, occupiedTables = occupiedTables });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStaffMembers()
        {
            try
            {
                var staff = await _context.Staff.ToListAsync();
                
                var staffList = new List<object>();
                foreach (var member in staff)
                {
                    staffList.Add(new
                    {
                        staffId = member.StaffId,
                        name = $"{member.Name} {member.Surname}"
                    });
                }

                return Json(new { success = true, staff = staffList });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableTables(string date, string time, int? currentReservationId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time))
                {
                    return Json(new { success = false, message = "Date and time are required" });
                }

                var reservationDate = DateTime.Parse(date);

                // Get all tables
                var allTables = await _context.RestaurantTables
                    .Include(t => t.ServedBy)
                    .Select(t => new
                    {
                        tableId = t.TableId,
                        minCapacity = t.MinCapacity,
                        maxCapacity = t.MaxCapacity,
                        serverName = t.ServedBy != null ? t.ServedBy.Name + " " + t.ServedBy.Surname : "No Server"
                    })
                    .ToListAsync();

                // Get occupied table IDs for the specific date/time
                var occupiedQuery = _context.Reservations
                    .Include(r => r.ReservationDetail)
                    .Where(r => r.ReservationDetail.ReservationDate.Date == reservationDate.Date)
                    .Where(r => r.ReservationDetail.ReservationHour == time)
                    .Where(r => r.ReservationDetail.ReservationStatus == "active");

                // Add condition to exclude current reservation if editing
                if (currentReservationId.HasValue)
                {
                    occupiedQuery = occupiedQuery.Where(r => r.ReservationId != currentReservationId.Value);
                }

                var occupiedTableIds = await occupiedQuery
                    .Select(r => r.TableId)
                    .ToListAsync();

                // Filter out occupied tables
                var availableTables = allTables.Where(t => !occupiedTableIds.Contains(t.tableId)).ToList();

                return Json(new { success = true, tables = availableTables });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }

}
