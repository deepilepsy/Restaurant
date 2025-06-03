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

                // Get table using Entity Framework (simpler approach)
                var table = await _context.RestaurantTables
                    .Where(t => t.TableId == tableId.Value)
                    .Include(t => t.ServedBy)
                    .FirstOrDefaultAsync();

                if (table == null)
                {
                    TempData["Error"] = "Selected table not found.";
                    return RedirectToAction("Booking", "Home");
                }

                // Check table availability using Entity Framework
                var existingReservation = await _context.Reservations
                    .Include(r => r.ReservationDetail)
                    .Where(r => r.TableId == tableId.Value)
                    .Where(r => r.ReservationDetail.ReservationDate.Date == reservationDate.Date)
                    .Where(r => r.ReservationDetail.ReservationHour == time)
                    .Where(r => r.ReservationDetail.ReservationStatus == "active")
                    .FirstOrDefaultAsync();

                if (existingReservation != null)
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
                TempData["Error"] = "An error occurred while loading the reservation form.";
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
                    model.Table = await _context.RestaurantTables
                        .Where(t => t.TableId == model.TableId)
                        .Include(t => t.ServedBy)
                        .FirstOrDefaultAsync();
                    return View(model);
                }

                // Check availability again
                var existingReservation = await _context.Reservations
                    .Include(r => r.ReservationDetail)
                    .Where(r => r.TableId == model.TableId)
                    .Where(r => r.ReservationDetail.ReservationDate.Date == model.ReservationDate.Date)
                    .Where(r => r.ReservationDetail.ReservationHour == model.ReservationHour)
                    .Where(r => r.ReservationDetail.ReservationStatus == "active")
                    .FirstOrDefaultAsync();

                if (existingReservation != null)
                {
                    ModelState.AddModelError("", "This table is no longer available for the selected time.");
                    model.Table = await _context.RestaurantTables
                        .Where(t => t.TableId == model.TableId)
                        .Include(t => t.ServedBy)
                        .FirstOrDefaultAsync();
                    return View(model);
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Get or create customer
                    var existingCustomer = await _context.Customers
                        .Where(c => c.TelNo == model.TelNo.Trim())
                        .FirstOrDefaultAsync();

                    Customer customer;
                    if (existingCustomer != null)
                    {
                        existingCustomer.Name = model.Name?.Trim() ?? string.Empty;
                        existingCustomer.Surname = model.Surname?.Trim() ?? string.Empty;
                        existingCustomer.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
                        customer = existingCustomer;
                    }
                    else
                    {
                        customer = new Customer
                        {
                            Name = model.Name?.Trim() ?? string.Empty,
                            Surname = model.Surname?.Trim() ?? string.Empty,
                            TelNo = model.TelNo?.Trim() ?? string.Empty,
                            Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim()
                        };
                        _context.Customers.Add(customer);
                    }

                    await _context.SaveChangesAsync();

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
                        CustomerId = customer.CustomerId,
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
                ModelState.AddModelError("", "An error occurred while creating your reservation.");
                
                model.Table = await _context.RestaurantTables
                    .Where(t => t.TableId == model.TableId)
                    .Include(t => t.ServedBy)
                    .FirstOrDefaultAsync();
                
                return View(model);
            }
        }

        // GET: Show reservation confirmation
        public async Task<IActionResult> Confirmation(int id)
        {
            try
            {
                var reservation = await _context.Reservations
                    .Where(r => r.ReservationId == id)
                    .Include(r => r.Table)
                    .ThenInclude(t => t.ServedBy)
                    .Include(r => r.Customer)
                    .Include(r => r.ReservationDetail)
                    .FirstOrDefaultAsync();

                if (reservation == null)
                {
                    TempData["Error"] = "Reservation not found.";
                    return RedirectToAction("Index", "Home");
                }

                return View(reservation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading confirmation for reservation {Id}", id);
                TempData["Error"] = "An error occurred while loading the confirmation.";
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

                var existingReservation = await _context.Reservations
                    .Include(r => r.ReservationDetail)
                    .Where(r => r.TableId == tableId)
                    .Where(r => r.ReservationDetail.ReservationDate.Date == reservationDate.Date)
                    .Where(r => r.ReservationDetail.ReservationHour == time)
                    .Where(r => r.ReservationDetail.ReservationStatus == "active")
                    .FirstOrDefaultAsync();

                var available = existingReservation == null;
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