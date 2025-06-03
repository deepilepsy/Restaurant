using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Restaurant.Models;
using System.Data;

namespace Restaurant.Controllers
{
    public class ReservationController : Controller
    {
        private readonly RestaurantContext _context;

        public ReservationController(RestaurantContext context)
        {
            _context = context;
        }

        // Simple test method to verify database connectivity
        [HttpGet]
        public async Task<IActionResult> TestConnection()
        {
            // Test basic database connection using raw SQL
            string tableCountSql = "SELECT COUNT(*) FROM [restaurant_tables]";
            var tableCountResult = await _context.Database.SqlQueryRaw<int>(tableCountSql).ToListAsync();
            var tableCount = tableCountResult.FirstOrDefault();

            string staffCountSql = "SELECT COUNT(*) FROM [staff]";
            var staffCountResult = await _context.Database.SqlQueryRaw<int>(staffCountSql).ToListAsync();
            var staffCount = staffCountResult.FirstOrDefault();

            return Json(new { 
                success = true, 
                message = $"Database connected. Tables: {tableCount}, Staff: {staffCount}",
                tableCount = tableCount,
                staffCount = staffCount
            });
        }

        // Helper method to load table with server
        private async Task<RestaurantTable> LoadTableWithServer(int tableId)
        {
            string tableSql = "SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
            var tables = await _context.RestaurantTables.FromSqlRaw(tableSql, tableId).ToListAsync();
            var table = tables.FirstOrDefault();

            if (table != null)
            {
                string serverSql = "SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var servers = await _context.Staff.FromSqlRaw(serverSql, table.ServedById).ToListAsync();
                table.ServedBy = servers.FirstOrDefault();
            }

            return table;
        }

        // Helper method to check table availability
        private async Task<bool> IsTableAvailable(int tableId, DateTime date, string time)
        {
            string availabilitySql = @"
                SELECT COUNT(*) FROM [reservations] r
                WHERE r.table_id = {0}
                AND EXISTS (
                    SELECT 1 FROM [reservation_details] rd 
                    WHERE rd.res_details_id = r.res_details_id
                    AND CAST(rd.reservation_date AS DATE) = CAST({1} AS DATE)
                    AND rd.reservation_hour = {2}
                    AND rd.reservation_status = 'active'
                )";

            var countResult = await _context.Database.SqlQueryRaw<int>(availabilitySql, tableId, date, time).ToListAsync();
            var existingCount = countResult.FirstOrDefault();
            
            return existingCount == 0;
        }

        // Helper method to load customer by phone
        private async Task<Customer> LoadCustomerByPhone(string phoneNumber)
        {
            string customerSql = "SELECT * FROM [customers] WHERE [tel_no] = {0}";
            var customers = await _context.Customers.FromSqlRaw(customerSql, phoneNumber).ToListAsync();
            return customers.FirstOrDefault();
        }

        // GET: Display reservation form with pre-populated data
        [HttpGet]
        public async Task<IActionResult> Create(int? tableId, int customerId = 0, string? date = null, string? time = null, int? guests = null)
        {
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

            // Load table with server
            var table = await LoadTableWithServer(tableId.Value);

            if (table == null)
            {
                TempData["Error"] = "Selected table not found.";
                return RedirectToAction("Booking", "Home");
            }

            // Check table availability
            bool isAvailable = await IsTableAvailable(tableId.Value, reservationDate, time);

            if (!isAvailable)
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
                string customerSql = "SELECT * FROM [customers] WHERE [customer_id] = {0}";
                var customers = await _context.Customers.FromSqlRaw(customerSql, customerId).ToListAsync();
                var customer = customers.FirstOrDefault();
                
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

        // POST: Handle reservation creation
        [HttpPost]
        public async Task<IActionResult> Create(ReservationCreateViewModel model)
        {
            // Remove validation for optional fields
            ModelState.Remove("Email");
            ModelState.Remove("SpecialRequests");
            ModelState.Remove("Table");

            if (!ModelState.IsValid)
            {
                // Reload table for view
                model.Table = await LoadTableWithServer(model.TableId);
                return View(model);
            }

            // Check availability again
            bool isAvailable = await IsTableAvailable(model.TableId, model.ReservationDate, model.ReservationHour);

            if (!isAvailable)
            {
                ModelState.AddModelError("", "This table is no longer available for the selected time.");
                model.Table = await LoadTableWithServer(model.TableId);
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            // Get or create customer
            var existingCustomer = await LoadCustomerByPhone(model.TelNo.Trim());

            int customerId;
            if (existingCustomer != null)
            {
                // Update existing customer using raw SQL
                string updateCustomerSql = @"
                    UPDATE [customers] 
                    SET [name] = {0}, [surname] = {1}, [email] = {2}
                    WHERE [customer_id] = {3}";

                await _context.Database.ExecuteSqlRawAsync(updateCustomerSql, 
                    model.Name?.Trim() ?? string.Empty,
                    model.Surname?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                    existingCustomer.CustomerId);

                customerId = existingCustomer.CustomerId;
            }
            else
            {
                // Create new customer using raw SQL
                string insertCustomerSql = @"
                    INSERT INTO [customers] ([name], [surname], [tel_no], [email])
                    OUTPUT INSERTED.customer_id
                    VALUES ({0}, {1}, {2}, {3})";

                var customerIds = await _context.Database.SqlQueryRaw<int>(insertCustomerSql,
                    model.Name?.Trim() ?? string.Empty,
                    model.Surname?.Trim() ?? string.Empty,
                    model.TelNo?.Trim() ?? string.Empty,
                    string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim()).ToListAsync();

                customerId = customerIds.First();
            }

            // Create reservation details using raw SQL
            string insertReservationDetailSql = @"
                INSERT INTO [reservation_details] ([guest_number], [created_at], [reservation_date], [reservation_hour], [reservation_status], [special_requests])
                OUTPUT INSERTED.res_details_id
                VALUES ({0}, {1}, {2}, {3}, {4}, {5})";

            var resDetailsIds = await _context.Database.SqlQueryRaw<int>(insertReservationDetailSql,
                model.GuestNumber,
                DateTime.Now,
                model.ReservationDate,
                model.ReservationHour,
                "active",
                string.IsNullOrWhiteSpace(model.SpecialRequests) ? null : model.SpecialRequests.Trim()).ToListAsync();

            var resDetailsId = resDetailsIds.First();

            // Create reservation using raw SQL
            string insertReservationSql = @"
                INSERT INTO [reservations] ([res_details_id], [customer_id], [table_id])
                OUTPUT INSERTED.reservation_id
                VALUES ({0}, {1}, {2})";

            var reservationIds = await _context.Database.SqlQueryRaw<int>(insertReservationSql,
                resDetailsId,
                customerId,
                model.TableId).ToListAsync();

            var reservationId = reservationIds.First();

            await transaction.CommitAsync();

            TempData["Success"] = $"Your reservation has been confirmed! Reservation ID: {reservationId}";
            return RedirectToAction("Confirmation", new { id = reservationId });
        }

        // Helper method to load complete reservation
        private async Task<Reservation> LoadCompleteReservation(int reservationId)
        {
            // Load reservation
            string reservationSql = "SELECT * FROM [reservations] WHERE [reservation_id] = {0}";
            var reservations = await _context.Reservations.FromSqlRaw(reservationSql, reservationId).ToListAsync();
            var reservation = reservations.FirstOrDefault();

            if (reservation == null) return null;

            // Load customer
            string customerSql = "SELECT * FROM [customers] WHERE [customer_id] = {0}";
            var customers = await _context.Customers.FromSqlRaw(customerSql, reservation.CustomerId).ToListAsync();
            reservation.Customer = customers.FirstOrDefault();

            // Load reservation details
            string detailsSql = "SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
            var details = await _context.ReservationDetails.FromSqlRaw(detailsSql, reservation.ResDetailsId).ToListAsync();
            reservation.ReservationDetail = details.FirstOrDefault();

            // Load table with server
            reservation.Table = await LoadTableWithServer(reservation.TableId);

            return reservation;
        }

        // GET: Show reservation confirmation
        public async Task<IActionResult> Confirmation(int id)
        {
            var reservation = await LoadCompleteReservation(id);
            
            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return RedirectToAction("Index", "Home");
            }

            return View(reservation);
        }

        // GET: Check table availability (AJAX endpoint)
        [HttpGet]
        public async Task<IActionResult> CheckAvailability(int tableId, string date, string time)
        {
            if (!DateTime.TryParse(date, out DateTime reservationDate))
            {
                return Json(new { available = false, message = "Invalid date format" });
            }

            bool available = await IsTableAvailable(tableId, reservationDate, time);
            string message = available ? "Table is available" : "Table is already reserved";

            return Json(new { available, message });
        }
    }
}