using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Restaurant.Models;
using System.Data;

namespace Restaurant.Controllers
{
    public class ReservationController : Controller
    {
        private readonly ILogger<ReservationController> _logger;
        private readonly RestaurantContext _context;

        public ReservationController(ILogger<ReservationController> logger, RestaurantContext context)
        {
            _logger = logger;
            _context = context;
        }

        // Simple test method to verify database connectivity
        [HttpGet]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                // Test basic database connection using Entity Framework
                var tableCount = await _context.RestaurantTables.CountAsync();
                var staffCount = await _context.Staff.CountAsync();

                return Json(new { 
                    success = true, 
                    message = $"Database connected. Tables: {tableCount}, Staff: {staffCount}",
                    tableCount = tableCount,
                    staffCount = staffCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = ex.Message, 
                    innerException = ex.InnerException?.Message
                });
            }
        }

        // GET: Display reservation form with pre-populated data
        [HttpGet]
        public async Task<IActionResult> Create(int? tableId, int customerId = 0, string? date = null, string? time = null, int? guests = null)
        {
            try
            {
                _logger.LogInformation("Starting reservation creation for table {TableId}", tableId);

                // Validate required parameters
                if (!tableId.HasValue || string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time) || !guests.HasValue)
                {
                    TempData["Error"] = "Missing reservation details. Please select a table first.";
                    return RedirectToAction("Booking", "Home");
                }

                // Parse date
                if (!DateTime.TryParse(date, out DateTime reservationDate))
                {
                    TempData["Error"] = "Invalid date format.";
                    return RedirectToAction("Booking", "Home");
                }

                // Check date is not in past
                if (reservationDate.Date < DateTime.Today)
                {
                    TempData["Error"] = "Cannot make reservations for past dates.";
                    return RedirectToAction("Booking", "Home");
                }

                // Load table using Entity Framework without raw SQL first
                var table = await _context.RestaurantTables
                    .Where(t => t.TableId == tableId.Value)
                    .FirstOrDefaultAsync();

                if (table == null)
                {
                    TempData["Error"] = "Selected table not found.";
                    return RedirectToAction("Booking", "Home");
                }

                // Load server separately
                var server = await _context.Staff
                    .Where(s => s.StaffId == table.ServedById)
                    .FirstOrDefaultAsync();

                table.ServedBy = server;

                // Check table availability using Entity Framework instead of raw SQL
                var existingReservationsCount = await _context.Reservations
                    .Join(_context.ReservationDetails,
                        r => r.ResDetailsId,
                        rd => rd.ResDetailsId,
                        (r, rd) => new { r, rd })
                    .Where(x => x.r.TableId == tableId.Value &&
                               x.rd.ReservationDate.Date == reservationDate.Date &&
                               x.rd.ReservationHour == time &&
                               x.rd.ReservationStatus == "active")
                    .CountAsync();

                if (existingReservationsCount > 0)
                {
                    TempData["Error"] = "This table is already reserved for the selected time.";
                    return RedirectToAction("Booking", "Home");
                }

                // Check capacity
                if (guests.Value < table.MinCapacity || guests.Value > table.MaxCapacity)
                {
                    TempData["Error"] = $"Table {tableId} capacity is {table.MinCapacity}-{table.MaxCapacity} people. You selected {guests} guests.";
                    return RedirectToAction("Booking", "Home");
                }

                // Create view model
                var viewModel = new ReservationCreateViewModel
                {
                    TableId = tableId.Value,
                    CustomerId = customerId,
                    ReservationDate = reservationDate,
                    ReservationHour = time,
                    GuestNumber = guests.Value,
                    Table = table,
                    Name = string.Empty,
                    Surname = string.Empty,
                    TelNo = string.Empty,
                    Email = string.Empty,
                    SpecialRequests = string.Empty
                };

                // Pre-populate customer data if available
                if (customerId > 0)
                {
                    var customer = await _context.Customers
                        .Where(c => c.CustomerId == customerId)
                        .FirstOrDefaultAsync();
                    
                    if (customer != null)
                    {
                        viewModel.Name = customer.Name;
                        viewModel.Surname = customer.Surname;
                        viewModel.TelNo = customer.TelNo;
                        viewModel.Email = customer.Email ?? string.Empty;
                    }
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in reservation creation GET");
                TempData["Error"] = "An error occurred while loading the reservation form: " + ex.Message;
                return RedirectToAction("Booking", "Home");
            }
        }

        // POST: Handle reservation creation
        [HttpPost]
        public async Task<IActionResult> Create(ReservationCreateViewModel model)
        {
            try
            {
                // Remove validation for optional fields
                ModelState.Remove("Email");
                ModelState.Remove("SpecialRequests");
                ModelState.Remove("Table");

                if (!ModelState.IsValid)
                {
                    // Reload table for view
                    var table = await _context.RestaurantTables
                        .Where(t => t.TableId == model.TableId)
                        .FirstOrDefaultAsync();

                    if (table != null)
                    {
                        var server = await _context.Staff
                            .Where(s => s.StaffId == table.ServedById)
                            .FirstOrDefaultAsync();
                        table.ServedBy = server;
                    }

                    model.Table = table;
                    return View(model);
                }

                // Check availability again using Entity Framework
                var existingReservationsCount = await _context.Reservations
                    .Join(_context.ReservationDetails,
                        r => r.ResDetailsId,
                        rd => rd.ResDetailsId,
                        (r, rd) => new { r, rd })
                    .Where(x => x.r.TableId == model.TableId &&
                               x.rd.ReservationDate.Date == model.ReservationDate.Date &&
                               x.rd.ReservationHour == model.ReservationHour &&
                               x.rd.ReservationStatus == "active")
                    .CountAsync();

                if (existingReservationsCount > 0)
                {
                    ModelState.AddModelError("", "This table is no longer available for the selected time.");
                    
                    // Reload table for view
                    var table = await _context.RestaurantTables
                        .Where(t => t.TableId == model.TableId)
                        .FirstOrDefaultAsync();

                    if (table != null)
                    {
                        var server = await _context.Staff
                            .Where(s => s.StaffId == table.ServedById)
                            .FirstOrDefaultAsync();
                        table.ServedBy = server;
                    }

                    model.Table = table;
                    return View(model);
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Get or create customer
                    var existingCustomer = await _context.Customers
                        .Where(c => c.TelNo == model.TelNo.Trim())
                        .FirstOrDefaultAsync();

                    int customerId;
                    if (existingCustomer != null)
                    {
                        // Update existing customer
                        existingCustomer.Name = model.Name?.Trim() ?? string.Empty;
                        existingCustomer.Surname = model.Surname?.Trim() ?? string.Empty;
                        existingCustomer.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
                        
                        await _context.SaveChangesAsync();
                        customerId = existingCustomer.CustomerId;
                    }
                    else
                    {
                        // Create new customer
                        var newCustomer = new Customer
                        {
                            Name = model.Name?.Trim() ?? string.Empty,
                            Surname = model.Surname?.Trim() ?? string.Empty,
                            TelNo = model.TelNo?.Trim() ?? string.Empty,
                            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim()
                        };

                        _context.Customers.Add(newCustomer);
                        await _context.SaveChangesAsync();
                        customerId = newCustomer.CustomerId;
                    }

                    // Create reservation details
                    var reservationDetail = new ReservationDetail
                    {
                        GuestNumber = model.GuestNumber,
                        CreatedAt = DateTime.Now,
                        ReservationDate = model.ReservationDate,
                        ReservationHour = model.ReservationHour,
                        ReservationStatus = "active",
                        SpecialRequests = string.IsNullOrWhiteSpace(model.SpecialRequests) ? null : model.SpecialRequests.Trim()
                    };

                    _context.ReservationDetails.Add(reservationDetail);
                    await _context.SaveChangesAsync();

                    // Create reservation
                    var reservation = new Reservation
                    {
                        ResDetailsId = reservationDetail.ResDetailsId,
                        CustomerId = customerId,
                        TableId = model.TableId
                    };

                    _context.Reservations.Add(reservation);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    TempData["Success"] = $"Your reservation has been confirmed! Reservation ID: {reservation.ReservationId}";
                    return RedirectToAction("Confirmation", new { id = reservation.ReservationId });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction error during reservation creation");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reservation");
                ModelState.AddModelError("", "An error occurred while creating your reservation: " + ex.Message);
                
                // Reload table for view
                var table = await _context.RestaurantTables
                    .Where(t => t.TableId == model.TableId)
                    .FirstOrDefaultAsync();

                if (table != null)
                {
                    var server = await _context.Staff
                        .Where(s => s.StaffId == table.ServedById)
                        .FirstOrDefaultAsync();
                    table.ServedBy = server;
                }

                model.Table = table;
                return View(model);
            }
        }

        // GET: Show reservation confirmation
        public async Task<IActionResult> Confirmation(int id)
        {
            try
            {
                // Load reservation using standard EF queries
                var reservation = await _context.Reservations
                    .Where(r => r.ReservationId == id)
                    .FirstOrDefaultAsync();
                
                if (reservation == null)
                {
                    TempData["Error"] = "Reservation not found.";
                    return RedirectToAction("Index", "Home");
                }

                // Load related entities separately
                reservation.Customer = await _context.Customers
                    .Where(c => c.CustomerId == reservation.CustomerId)
                    .FirstOrDefaultAsync();

                reservation.ReservationDetail = await _context.ReservationDetails
                    .Where(rd => rd.ResDetailsId == reservation.ResDetailsId)
                    .FirstOrDefaultAsync();

                reservation.Table = await _context.RestaurantTables
                    .Where(t => t.TableId == reservation.TableId)
                    .FirstOrDefaultAsync();

                if (reservation.Table != null)
                {
                    reservation.Table.ServedBy = await _context.Staff
                        .Where(s => s.StaffId == reservation.Table.ServedById)
                        .FirstOrDefaultAsync();
                }

                return View(reservation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading confirmation for reservation {Id}", id);
                TempData["Error"] = "An error occurred while loading the confirmation: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Check table availability (AJAX endpoint)
        [HttpGet]
        public async Task<IActionResult> CheckAvailability(int tableId, string date, string time)
        {
            try
            {
                if (!DateTime.TryParse(date, out DateTime reservationDate))
                {
                    return Json(new { available = false, message = "Invalid date format" });
                }

                var existingReservationsCount = await _context.Reservations
                    .Join(_context.ReservationDetails,
                        r => r.ResDetailsId,
                        rd => rd.ResDetailsId,
                        (r, rd) => new { r, rd })
                    .Where(x => x.r.TableId == tableId &&
                               x.rd.ReservationDate.Date == reservationDate.Date &&
                               x.rd.ReservationHour == time &&
                               x.rd.ReservationStatus == "active")
                    .CountAsync();

                var available = existingReservationsCount == 0;
                var message = available ? "Table is available" : "Table is already reserved";

                return Json(new { available, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking availability for table {TableId}", tableId);
                return Json(new { available = false, message = "Error checking availability" });
            }
        }
    }
}