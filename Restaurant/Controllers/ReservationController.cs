using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Models;
using System.Globalization;

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
public async Task<IActionResult> Create(int? tableId, int customerId, string? date, string? time, int? guests)
{
    try
    {
        _logger.LogInformation("Reservation Create GET called with: tableId={TableId}, date={Date}, time={Time}, guests={Guests}", 
            tableId, date, time, guests);

        // Validate that we have the required parameters
        if (!tableId.HasValue || string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time) || !guests.HasValue)
        {
            _logger.LogWarning("Missing required parameters for reservation");
            TempData["Error"] = "Missing reservation details. Please select a table first.";
            return RedirectToAction("Booking", "Home");
        }

        // Verify table exists and get its details
        var table = await _context.RestaurantTables
            .Include(t => t.ServedBy)
            .FirstOrDefaultAsync(t => t.TableId == tableId.Value);

        if (table == null)
        {
            _logger.LogWarning("Table {TableId} not found", tableId.Value);
            TempData["Error"] = "Selected table not found.";
            return RedirectToAction("Booking", "Home");
        }

        // Parse and validate date
        if (!DateTime.TryParse(date, out DateTime reservationDate))
        {
            _logger.LogWarning("Invalid date format: {Date}", date);
            TempData["Error"] = "Invalid date format.";
            return RedirectToAction("Booking", "Home");
        }

        // Check if the date is not in the past
        if (reservationDate.Date < DateTime.Today)
        {
            _logger.LogWarning("Reservation date {Date} is in the past", reservationDate);
            TempData["Error"] = "Cannot make reservations for past dates.";
            return RedirectToAction("Booking", "Home");
        }

        // Check if the table is available at the selected time
        var existingReservation = await _context.Reservations
            .Where(r => r.TableId == tableId.Value && 
                       r.ReservationDate.Date == reservationDate.Date && 
                       r.ReservationHour == time &&
                       r.ReservationStatus == "active")
            .FirstOrDefaultAsync();

        if (existingReservation != null)
        {
            _logger.LogWarning("Table {TableId} already reserved for {Date} at {Time}", 
                tableId.Value, reservationDate, time);
            TempData["Error"] = "This table is already reserved for the selected time.";
            return RedirectToAction("Booking", "Home");
        }

        // Check table capacity
        if (guests.Value < table.MinCapacity || guests.Value > table.MaxCapacity)
        {
            _logger.LogWarning("Guest count {Guests} outside table {TableId} capacity {MinCap}-{MaxCap}", 
                guests.Value, tableId.Value, table.MinCapacity, table.MaxCapacity);
            TempData["Error"] = $"Table {tableId} capacity is {table.MinCapacity}-{table.MaxCapacity} people. You selected {guests} guests.";
            return RedirectToAction("Booking", "Home");
        }

        // Create view model with the reservation details
        var viewModel = new ReservationCreateViewModel
        {
            TableId = tableId.Value,
            CustomerId = customerId,  // Fixed: comma instead of semicolon
            ReservationDate = reservationDate,
            ReservationHour = time,
            GuestNumber = guests.Value,
            Table = table,
            // Initialize empty strings to avoid null reference errors
            Name = string.Empty,
            Surname = string.Empty,
            TelNo = string.Empty,
            Email = string.Empty,
            SpecialRequests = string.Empty
        };

        // Pre-populate customer data if customerId is provided and valid
        if (customerId > 0)
        {
            var existingCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
            
            if (existingCustomer != null)
            {
                viewModel.Name = existingCustomer.Name;
                viewModel.Surname = existingCustomer.Surname ?? string.Empty;
                viewModel.TelNo = existingCustomer.TelNo;
                viewModel.Email = existingCustomer.Email ?? string.Empty;
            }
        }

        _logger.LogInformation("Successfully loaded reservation form for table {TableId}", tableId.Value);
        return View(viewModel);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading reservation form for tableId={TableId}, date={Date}, time={Time}, guests={Guests}", 
            tableId, date, time, guests);
        TempData["Error"] = "An error occurred while loading the reservation form. Please try again.";
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
                ModelState.Remove("Surname");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model state invalid for reservation creation");
                    
                    // Log validation errors
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning("Validation error: {Error}", error.ErrorMessage);
                    }

                    // Reload table information for the view
                    model.Table = await _context.RestaurantTables
                        .Include(t => t.ServedBy)
                        .FirstOrDefaultAsync(t => t.TableId == model.TableId);
                    
                    return View(model);
                }

                // Double-check table availability
                var existingReservation = await _context.Reservations
                    .Where(r => r.TableId == model.TableId && 
                               r.ReservationDate.Date == model.ReservationDate.Date && 
                               r.ReservationHour == model.ReservationHour &&
                               r.ReservationStatus == "active")
                    .FirstOrDefaultAsync();

                if (existingReservation != null)
                {
                    _logger.LogWarning("Table {TableId} no longer available during creation", model.TableId);
                    ModelState.AddModelError("", "This table is no longer available for the selected time.");
                    model.Table = await _context.RestaurantTables
                        .Include(t => t.ServedBy)
                        .FirstOrDefaultAsync(t => t.TableId == model.TableId);
                    return View(model);
                }

                // Get the table to find the assigned server
                var table = await _context.RestaurantTables
                    .FirstOrDefaultAsync(t => t.TableId == model.TableId);

                if (table == null)
                {
                    _logger.LogError("Table {TableId} not found during reservation creation", model.TableId);
                    ModelState.AddModelError("", "Selected table not found.");
                    return View(model);
                }

                // Create new reservation
                // Check if customer already exists by phone or email
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.TelNo == model.TelNo.Trim());

                Customer customer;
                if (existingCustomer != null)
                {
                    // Update existing customer info if needed
                    existingCustomer.Name = model.Name?.Trim() ?? string.Empty;
                    existingCustomer.Surname = model.Surname?.Trim();
                    existingCustomer.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
                    customer = existingCustomer;
                }
                else
                {
                    // Create new customer
                    customer = new Customer
                    {
                        Name = model.Name?.Trim() ?? string.Empty,
                        Surname = model.Surname?.Trim(),
                        TelNo = model.TelNo?.Trim() ?? string.Empty,
                        Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim()
                    };
                    _context.Customers.Add(customer);
                }

                await _context.SaveChangesAsync();

// Create reservation
                var reservation = new Reservation
                {
                    CustomerId = customer.CustomerId,
                    SpecialRequests = string.IsNullOrWhiteSpace(model.SpecialRequests) ? null : model.SpecialRequests.Trim(),
                    GuestNumber = model.GuestNumber,
                    TableId = model.TableId,
                    ServedById = table.ServedById,
                    ReservationDate = model.ReservationDate,
                    ReservationHour = model.ReservationHour,
                    ReservationStatus = "active",
                    CreatedAt = DateTime.Now
                };
                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Reservation {ReservationId} created successfully for {Name} {Surname} on {Date} at {Time}", 
                    reservation.ReservationId, model.Name, model.Surname, model.ReservationDate.ToShortDateString(), model.ReservationHour);

                TempData["Success"] = $"Your reservation has been confirmed! Reservation ID: {reservation.ReservationId}";
                return RedirectToAction("Confirmation", new { id = reservation.ReservationId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reservation for table {TableId}", model.TableId);
                ModelState.AddModelError("", "An error occurred while creating your reservation. Please try again.");
                
                // Reload table information for the view
                model.Table = await _context.RestaurantTables
                    .Include(t => t.ServedBy)
                    .FirstOrDefaultAsync(t => t.TableId == model.TableId);
                
                return View(model);
            }
        }

        // GET: Show reservation confirmation
        public async Task<IActionResult> Confirmation(int id)
        {
            try
            {
                var reservation = await _context.Reservations
                    .Include(r => r.Table)
                    .Include(r => r.ServedBy)
                    .FirstOrDefaultAsync(r => r.ReservationId == id);

                if (reservation == null)
                {
                    _logger.LogWarning("Reservation {ReservationId} not found for confirmation", id);
                    TempData["Error"] = "Reservation not found.";
                    return RedirectToAction("Index", "Home");
                }

                return View(reservation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reservation confirmation for ID: {ReservationId}", id);
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
                    .Where(r => r.TableId == tableId && 
                               r.ReservationDate.Date == reservationDate.Date && 
                               r.ReservationHour == time &&
                               r.ReservationStatus == "active")
                    .FirstOrDefaultAsync();

                var available = existingReservation == null;
                var message = available ? "Table is available" : "Table is already reserved";

                return Json(new { available, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking table availability for table {TableId}", tableId);
                return Json(new { available = false, message = "Error checking availability" });
            }
        }
    }
}