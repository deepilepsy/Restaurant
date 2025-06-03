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
        private readonly ILogger<HomeController> _logger;

        public HomeController(RestaurantContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Menu()
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
        public async Task<IActionResult> Login(string username, string password)
        {
            try
            {
                // Use raw SQL instead of LINQ
                string sql = @"SELECT * FROM [user_credentials] 
                              WHERE [username] = {0} AND [password] = {1}";
                
                var users = await _context.UserCredentials
                    .FromSqlRaw(sql, username, password)
                    .ToListAsync();
                
                var user = users.FirstOrDefault();

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
                // Get staff members using raw SQL
                string staffSql = @"SELECT * FROM [staff]";
                var staffMembers = await _context.Staff
                    .FromSqlRaw(staffSql)
                    .ToListAsync();

                // Calculate statistics using raw SQL - avoid SqlQueryRaw<int> with COUNT
                // Get total staff count
                string totalStaffSql = @"SELECT [staff_id] FROM [staff]";
                var allStaffIds = await _context.Database
                    .SqlQueryRaw<int>(totalStaffSql)
                    .ToListAsync();
                var totalStaff = allStaffIds.Count;

                // Get waiter count
                string waitersSql = @"SELECT [staff_id] FROM [staff] WHERE LOWER([job]) = 'waiter'";
                var waiterIds = await _context.Database
                    .SqlQueryRaw<int>(waitersSql)
                    .ToListAsync();
                var totalWaiters = waiterIds.Count;

                // Get active reservations count
                string activeReservationsSql = @"SELECT [res_details_id] FROM [reservation_details] WHERE [reservation_status] = 'active'";
                var activeReservationIds = await _context.Database
                    .SqlQueryRaw<int>(activeReservationsSql)
                    .ToListAsync();
                var activeReservations = activeReservationIds.Count;

                var viewModel = new AdminPanelView
                {
                    StaffMembers = staffMembers,
                    TotalStaff = totalStaff,
                    TotalWaiters = totalWaiters,
                    ActiveReservations = activeReservations,
                    UpcomingReservations = new List<Reservation>()
                };

                // Get active reservations using separate loading approach
                var today = DateTime.Today;
                string reservationsSql = @"
                    SELECT * FROM [reservations] r
                    WHERE EXISTS (
                        SELECT 1 FROM [reservation_details] rd 
                        WHERE rd.res_details_id = r.res_details_id 
                        AND rd.reservation_status = 'active' 
                        AND rd.reservation_date >= {0}
                    )";

                var activeReservationsList = await _context.Reservations
                    .FromSqlRaw(reservationsSql, today)
                    .ToListAsync();

                // Load related data for each reservation
                foreach (var reservation in activeReservationsList)
                {
                    // Load customer
                    string customerSql = @"SELECT * FROM [customers] WHERE [customer_id] = {0}";
                    var customers = await _context.Customers
                        .FromSqlRaw(customerSql, reservation.CustomerId)
                        .ToListAsync();
                    reservation.Customer = customers.FirstOrDefault();

                    // Load reservation details
                    string detailsSql = @"SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
                    var details = await _context.ReservationDetails
                        .FromSqlRaw(detailsSql, reservation.ResDetailsId)
                        .ToListAsync();
                    reservation.ReservationDetail = details.FirstOrDefault();

                    // Load table
                    string tableSql = @"SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
                    var tables = await _context.RestaurantTables
                        .FromSqlRaw(tableSql, reservation.TableId)
                        .ToListAsync();
                    reservation.Table = tables.FirstOrDefault();

                    // Load server
                    if (reservation.Table != null)
                    {
                        string staffSql2 = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                        var staff = await _context.Staff
                            .FromSqlRaw(staffSql2, reservation.Table.ServedById)
                            .ToListAsync();
                        reservation.Table.ServedBy = staff.FirstOrDefault();
                    }
                }

                // Sort manually after retrieval
                activeReservationsList = activeReservationsList
                    .Where(r => r.ReservationDetail != null)
                    .OrderBy(r => r.ReservationDetail.ReservationDate)
                    .ThenBy(r => r.ReservationDetail.ReservationHour)
                    .ToList();

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
                // Get active reservations using separate loading approach
                var today = DateTime.Today;
                string reservationsSql = @"
                    SELECT * FROM [reservations] r
                    WHERE EXISTS (
                        SELECT 1 FROM [reservation_details] rd 
                        WHERE rd.res_details_id = r.res_details_id 
                        AND rd.reservation_status = 'active' 
                        AND rd.reservation_date >= {0}
                    )";

                var activeReservations = await _context.Reservations
                    .FromSqlRaw(reservationsSql, today)
                    .ToListAsync();

                // Load related data for each reservation
                foreach (var reservation in activeReservations)
                {
                    // Load customer
                    string customerSql = @"SELECT * FROM [customers] WHERE [customer_id] = {0}";
                    var customers = await _context.Customers
                        .FromSqlRaw(customerSql, reservation.CustomerId)
                        .ToListAsync();
                    reservation.Customer = customers.FirstOrDefault();

                    // Load reservation details
                    string detailsSql = @"SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
                    var details = await _context.ReservationDetails
                        .FromSqlRaw(detailsSql, reservation.ResDetailsId)
                        .ToListAsync();
                    reservation.ReservationDetail = details.FirstOrDefault();

                    // Load table
                    string tableSql = @"SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
                    var tables = await _context.RestaurantTables
                        .FromSqlRaw(tableSql, reservation.TableId)
                        .ToListAsync();
                    reservation.Table = tables.FirstOrDefault();

                    // Load server
                    if (reservation.Table != null)
                    {
                        string staffSql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                        var staff = await _context.Staff
                            .FromSqlRaw(staffSql, reservation.Table.ServedById)
                            .ToListAsync();
                        reservation.Table.ServedBy = staff.FirstOrDefault();
                    }
                }

                // Sort manually after retrieval
                activeReservations = activeReservations
                    .Where(r => r.ReservationDetail != null)
                    .OrderBy(r => r.ReservationDetail.ReservationDate)
                    .ThenBy(r => r.ReservationDetail.ReservationHour)
                    .ToList();

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
                // Load tables and staff separately for better reliability
                string tablesSql = @"SELECT * FROM [restaurant_tables]";
                var tables = await _context.RestaurantTables
                    .FromSqlRaw(tablesSql)
                    .ToListAsync();

                // Load staff for each table
                foreach (var table in tables)
                {
                    string staffSql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                    var staffList = await _context.Staff
                        .FromSqlRaw(staffSql, table.ServedById)
                        .ToListAsync();
                    table.ServedBy = staffList.FirstOrDefault();
                }

                // Sort manually after retrieval
                tables = tables.OrderBy(t => t.TableId).ToList();

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
                _logger.LogError(ex, "Error loading booking page");
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
                // Load reservation using separate queries
                string reservationSql = @"SELECT * FROM [reservations] WHERE [reservation_id] = {0}";
                var reservations = await _context.Reservations
                    .FromSqlRaw(reservationSql, reservationId)
                    .ToListAsync();

                var reservation = reservations.FirstOrDefault();

                if (reservation == null)
                {
                    TempData["Error"] = "Reservation not found";
                    return RedirectToAction("Staff");
                }

                // Load related data separately
                // Load customer
                string customerSql = @"SELECT * FROM [customers] WHERE [customer_id] = {0}";
                var customers = await _context.Customers
                    .FromSqlRaw(customerSql, reservation.CustomerId)
                    .ToListAsync();
                reservation.Customer = customers.FirstOrDefault();

                // Load reservation details
                string detailsSql = @"SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
                var details = await _context.ReservationDetails
                    .FromSqlRaw(detailsSql, reservation.ResDetailsId)
                    .ToListAsync();
                reservation.ReservationDetail = details.FirstOrDefault();

                // Load table
                string tableSql = @"SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
                var tables = await _context.RestaurantTables
                    .FromSqlRaw(tableSql, reservation.TableId)
                    .ToListAsync();
                reservation.Table = tables.FirstOrDefault();

                // Load server
                if (reservation.Table != null)
                {
                    string staffSql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                    var staff = await _context.Staff
                        .FromSqlRaw(staffSql, reservation.Table.ServedById)
                        .ToListAsync();
                    reservation.Table.ServedBy = staff.FirstOrDefault();
                }

                // Get all menu items using raw SQL
                string menuSql = @"SELECT * FROM [menu_items]";
                var menuItems = await _context.MenuItems
                    .FromSqlRaw(menuSql)
                    .ToListAsync();

                // Sort manually after retrieval
                menuItems = menuItems
                    .OrderBy(m => m.Category)
                    .ThenBy(m => m.Subcategory ?? "")
                    .ThenBy(m => m.ItemName)
                    .ToList();

                // Check for existing receipt
                string receiptSql = @"SELECT * FROM [receipts] WHERE [reservation_id] = {0}";
                var existingReceipts = await _context.Receipts
                    .FromSqlRaw(receiptSql, reservationId)
                    .ToListAsync();

                var existingReceipt = existingReceipts.FirstOrDefault();

                // Load receipt items if receipt exists
                if (existingReceipt != null)
                {
                    string receiptItemsSql = @"SELECT * FROM [receipt_items] WHERE [receipt_id] = {0}";
                    var receiptItems = await _context.ReceiptItems
                        .FromSqlRaw(receiptItemsSql, existingReceipt.ReceiptId)
                        .ToListAsync();

                    // Load menu items for each receipt item
                    foreach (var item in receiptItems)
                    {
                        string menuItemSql = @"SELECT * FROM [menu_items] WHERE [item_id] = {0}";
                        var menuItemList = await _context.MenuItems
                            .FromSqlRaw(menuItemSql, item.ItemId)
                            .ToListAsync();
                        item.MenuItem = menuItemList.FirstOrDefault();
                    }

                    existingReceipt.ReceiptItems = receiptItems;
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

                // Check if receipt already exists using raw SQL
                string checkReceiptSql = @"SELECT * FROM [receipts] WHERE [reservation_id] = {0}";
                var existingReceipts = await _context.Receipts
                    .FromSqlRaw(checkReceiptSql, orderData.ReservationId)
                    .ToListAsync();

                var existingReceipt = existingReceipts.FirstOrDefault();

                if (existingReceipt != null)
                {
                    // Update existing receipt
                    string updateReceiptSql = @"UPDATE [receipts] SET [total_amount] = {0} WHERE [receipt_id] = {1}";
                    await _context.Database.ExecuteSqlRawAsync(updateReceiptSql, orderData.TotalAmount, existingReceipt.ReceiptId);
                    
                    // Delete existing receipt items
                    string deleteItemsSql = @"DELETE FROM [receipt_items] WHERE [receipt_id] = {0}";
                    await _context.Database.ExecuteSqlRawAsync(deleteItemsSql, existingReceipt.ReceiptId);

                    // Add new receipt items
                    foreach (var item in orderData.Items)
                    {
                        string insertItemSql = @"
                            INSERT INTO [receipt_items] ([receipt_id], [item_id], [quantity], [unit_price], [special_notes])
                            VALUES ({0}, {1}, {2}, {3}, {4})";
                        
                        await _context.Database.ExecuteSqlRawAsync(insertItemSql, 
                            existingReceipt.ReceiptId, 
                            item.ItemId, 
                            item.Quantity, 
                            item.UnitPrice, 
                            orderData.SpecialNotes ?? "");
                    }

                    TempData["Success"] = "Order updated successfully!";
                }
                else
                {
                    // Create new receipt using INSERT with OUTPUT
                    string insertReceiptSql = @"
                        INSERT INTO [receipts] ([reservation_id], [staff_id], [total_amount], [created_at])
                        OUTPUT INSERTED.receipt_id
                        VALUES ({0}, {1}, {2}, {3})";

                    var receiptIds = await _context.Database
                        .SqlQueryRaw<int>(insertReceiptSql, orderData.ReservationId, orderData.StaffId, orderData.TotalAmount, DateTime.Now)
                        .ToListAsync();

                    var newReceiptId = receiptIds.First();

                    // Add receipt items
                    foreach (var item in orderData.Items)
                    {
                        string insertItemSql = @"
                            INSERT INTO [receipt_items] ([receipt_id], [item_id], [quantity], [unit_price], [special_notes])
                            VALUES ({0}, {1}, {2}, {3}, {4})";
                        
                        await _context.Database.ExecuteSqlRawAsync(insertItemSql, 
                            newReceiptId, 
                            item.ItemId, 
                            item.Quantity, 
                            item.UnitPrice, 
                            orderData.SpecialNotes ?? "");
                    }

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
                string sql = @"SELECT * FROM [restaurant_tables]";
                var tables = await _context.RestaurantTables
                    .FromSqlRaw(sql)
                    .ToListAsync();
                
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
                _logger.LogError(ex, "Error getting table capacities");
                return Json(new { success = false, message = ex.Message });
            }
        } 
        
[HttpGet]
public async Task<IActionResult> Edit(int id)
{
    try
    {
        // Load reservation using separate queries
        string reservationSql = @"SELECT * FROM [reservations] WHERE [reservation_id] = {0}";
        var reservations = await _context.Reservations
            .FromSqlRaw(reservationSql, id)
            .ToListAsync();

        var reservation = reservations.FirstOrDefault();

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return GoBackToPanel();
        }

        // Load related data separately
        // Load customer
        string customerSql = @"SELECT * FROM [customers] WHERE [customer_id] = {0}";
        var customers = await _context.Customers
            .FromSqlRaw(customerSql, reservation.CustomerId)
            .ToListAsync();
        reservation.Customer = customers.FirstOrDefault();

        // Load reservation details
        string detailsSql = @"SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
        var details = await _context.ReservationDetails
            .FromSqlRaw(detailsSql, reservation.ResDetailsId)
            .ToListAsync();
        reservation.ReservationDetail = details.FirstOrDefault();

        // Load table
        string tableSql = @"SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
        var tables = await _context.RestaurantTables
            .FromSqlRaw(tableSql, reservation.TableId)
            .ToListAsync();
        reservation.Table = tables.FirstOrDefault();

        // Load server
        if (reservation.Table != null)
        {
            string staffSql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
            var staff = await _context.Staff
                .FromSqlRaw(staffSql, reservation.Table.ServedById)
                .ToListAsync();
            reservation.Table.ServedBy = staff.FirstOrDefault();
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
        // Load the existing reservation using separate queries
        string loadReservationSql = @"SELECT * FROM [reservations] WHERE [reservation_id] = {0}";
        var reservations = await _context.Reservations
            .FromSqlRaw(loadReservationSql, model.ReservationId)
            .ToListAsync();

        var reservation = reservations.FirstOrDefault();

        if (reservation == null)
        {
            TempData["Error"] = "Reservation not found.";
            return GoBackToPanel();
        }

        // Load reservation details
        string detailsSql = @"SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
        var details = await _context.ReservationDetails
            .FromSqlRaw(detailsSql, reservation.ResDetailsId)
            .ToListAsync();
        reservation.ReservationDetail = details.FirstOrDefault();

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

        // Update customer information using raw SQL
        string updateCustomerSql = @"
            UPDATE [customers] 
            SET [name] = {0}, [surname] = {1}, [tel_no] = {2}, [email] = {3}
            WHERE [customer_id] = {4}";

        await _context.Database.ExecuteSqlRawAsync(updateCustomerSql, 
            customerName, customerSurname, customerTelNo, customerEmail, reservation.CustomerId);

        // Update special requests
        var specialRequests = Request.Form["ReservationDetail.SpecialRequests"].ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(specialRequests))
        {
            specialRequests = null;
        }

        string updateSpecialRequestsSql = @"
            UPDATE [reservation_details] 
            SET [special_requests] = {0}
            WHERE [res_details_id] = {1}";

        await _context.Database.ExecuteSqlRawAsync(updateSpecialRequestsSql, specialRequests, reservation.ResDetailsId);

        // Only update other reservation details if status is active
        if (reservation.ReservationDetail?.ReservationStatus == "active")
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

            // Validate the selected table exists using raw SQL
            string checkTableSql = @"SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
            var selectedTables = await _context.RestaurantTables
                .FromSqlRaw(checkTableSql, tableId)
                .ToListAsync();

            var selectedTable = selectedTables.FirstOrDefault(); // FIXED: Added this missing line

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

            // Check if the table is available for the selected date/time (excluding current reservation) using raw SQL
            string conflictCheckSql = @"
                SELECT [reservation_id] FROM [reservations] r
                WHERE r.table_id = {0} 
                AND EXISTS (
                    SELECT 1 FROM [reservation_details] rd 
                    WHERE rd.res_details_id = r.res_details_id
                    AND CAST(rd.reservation_date AS DATE) = CAST({1} AS DATE)
                    AND rd.reservation_hour = {2}
                    AND rd.reservation_status = 'active'
                )
                AND r.reservation_id != {3}";

            var conflictingReservationIds = await _context.Database
                .SqlQueryRaw<int>(conflictCheckSql, tableId, reservationDate, reservationHour, model.ReservationId)
                .ToListAsync();

            if (conflictingReservationIds.Any())
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

            // Update reservation details using raw SQL
            string updateReservationDetailsSql = @"
                UPDATE [reservation_details] 
                SET [guest_number] = {0}, [reservation_date] = {1}, [reservation_hour] = {2}
                WHERE [res_details_id] = {3}";

            await _context.Database.ExecuteSqlRawAsync(updateReservationDetailsSql, 
                guestNumber, reservationDate, reservationHour, reservation.ResDetailsId);

            // Update table assignment if it changed
            if (tableId != reservation.TableId)
            {
                string updateTableSql = @"UPDATE [reservations] SET [table_id] = {0} WHERE [reservation_id] = {1}";
                await _context.Database.ExecuteSqlRawAsync(updateTableSql, tableId, model.ReservationId);
            }
        }

        TempData["Success"] = $"Reservation #{model.ReservationId} has been updated successfully.";
        return GoBackToPanel();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in Edit POST: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        TempData["Error"] = $"An error occurred while updating the reservation: {ex.Message}";
        
        // Reload the reservation for the view using separate queries
        string reloadSql = @"SELECT * FROM [reservations] WHERE [reservation_id] = {0}";
        var reservationForView = await _context.Reservations
            .FromSqlRaw(reloadSql, model.ReservationId)
            .FirstOrDefaultAsync();

        if (reservationForView != null)
        {
            // Load related data
            string customerSql = @"SELECT * FROM [customers] WHERE [customer_id] = {0}";
            var customers = await _context.Customers
                .FromSqlRaw(customerSql, reservationForView.CustomerId)
                .ToListAsync();
            reservationForView.Customer = customers.FirstOrDefault();

            string detailsSql = @"SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
            var details = await _context.ReservationDetails
                .FromSqlRaw(detailsSql, reservationForView.ResDetailsId)
                .ToListAsync();
            reservationForView.ReservationDetail = details.FirstOrDefault();

            string tableSql = @"SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
            var tables = await _context.RestaurantTables
                .FromSqlRaw(tableSql, reservationForView.TableId)
                .ToListAsync();
            reservationForView.Table = tables.FirstOrDefault();

            if (reservationForView.Table != null)
            {
                string staffSql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var staff = await _context.Staff
                    .FromSqlRaw(staffSql, reservationForView.Table.ServedById)
                    .ToListAsync();
                reservationForView.Table.ServedBy = staff.FirstOrDefault();
            }
        }
            
        return View(reservationForView ?? new Reservation());
    }
}

        // Delete method using raw SQL
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CheckUserLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                // Load reservation using separate queries
                string reservationSql = @"SELECT * FROM [reservations] WHERE [reservation_id] = {0}";
                var reservations = await _context.Reservations
                    .FromSqlRaw(reservationSql, id)
                    .ToListAsync();

                var reservation = reservations.FirstOrDefault();

                if (reservation == null)
                {
                    TempData["Error"] = "Reservation not found.";
                    return GoBackToPanel();
                }

                // Load reservation details
                string detailsSql = @"SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
                var details = await _context.ReservationDetails
                    .FromSqlRaw(detailsSql, reservation.ResDetailsId)
                    .ToListAsync();
                reservation.ReservationDetail = details.FirstOrDefault();

                if (reservation.ReservationDetail?.ReservationStatus != "active")
                {
                    TempData["Error"] = "Only active reservations can be deleted.";
                    return RedirectToAction("Edit", new { id = id });
                }

                // Delete reservation and its details using raw SQL
                string deleteReservationSql = @"DELETE FROM [reservations] WHERE [reservation_id] = {0}";
                await _context.Database.ExecuteSqlRawAsync(deleteReservationSql, id);

                string deleteDetailsSql = @"DELETE FROM [reservation_details] WHERE [res_details_id] = {0}";
                await _context.Database.ExecuteSqlRawAsync(deleteDetailsSql, reservation.ResDetailsId);

                TempData["Success"] = $"Reservation #{id} has been permanently deleted.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting reservation: {ex.Message}");
                TempData["Error"] = "An error occurred while deleting the reservation. Please try again.";
            }

            return GoBackToPanel();
        }

        // Update EditStaff methods to use raw SQL
        [HttpGet]
        public async Task<IActionResult> EditStaff(int id)
        {
            if (HttpContext.Session.GetString("admin") != "true")
            {
                return RedirectToAction("Login");
            }

            try
            {
                string sql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var staffList = await _context.Staff
                    .FromSqlRaw(sql, id)
                    .ToListAsync();

                var staff = staffList.FirstOrDefault();

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
                // Check if phone number exists for another staff member using raw SQL
                string checkPhoneSql = @"SELECT * FROM [staff] WHERE [tel_no] = {0} AND [staff_id] != {1}";
                var existingStaffList = await _context.Staff
                    .FromSqlRaw(checkPhoneSql, staff.TelNo, staff.StaffId)
                    .ToListAsync();

                if (existingStaffList.Any())
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

                // Update staff member using raw SQL
                string updateSql = @"
                    UPDATE [staff] 
                    SET [name] = {0}, [surname] = {1}, [job] = {2}, [tel_no] = {3}
                    WHERE [staff_id] = {4}";

                await _context.Database.ExecuteSqlRawAsync(updateSql, 
                    staff.Name, staff.Surname, staff.Job, staff.TelNo, staff.StaffId);

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
                string staffSql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var staffList = await _context.Staff
                    .FromSqlRaw(staffSql, id)
                    .ToListAsync();

                var staff = staffList.FirstOrDefault();

                if (staff == null)
                {
                    TempData["Error"] = "Staff member not found.";
                    return RedirectToAction("Admin");
                }

                // Check if staff has active reservations using raw SQL - avoid COUNT
                string activeReservationsSql = @"
                    SELECT [reservation_id] FROM [reservations] r
                    WHERE EXISTS (
                        SELECT 1 FROM [restaurant_tables] rt 
                        WHERE rt.table_id = r.table_id AND rt.served_by_id = {0}
                    )
                    AND EXISTS (
                        SELECT 1 FROM [reservation_details] rd 
                        WHERE rd.res_details_id = r.res_details_id AND rd.reservation_status = 'active'
                    )";

                var activeReservationIds = await _context.Database
                    .SqlQueryRaw<int>(activeReservationsSql, id)
                    .ToListAsync();

                ViewBag.ActiveReservations = activeReservationIds.Count;
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
                string staffSql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var staffList = await _context.Staff
                    .FromSqlRaw(staffSql, id)
                    .ToListAsync();

                var staff = staffList.FirstOrDefault();

                if (staff == null)
                {
                    TempData["Error"] = "Staff member not found.";
                    return RedirectToAction("Admin");
                }

                // Check if staff has active reservations using raw SQL - avoid COUNT
                string activeReservationsSql = @"
                    SELECT [reservation_id] FROM [reservations] r
                    WHERE EXISTS (
                        SELECT 1 FROM [restaurant_tables] rt 
                        WHERE rt.table_id = r.table_id AND rt.served_by_id = {0}
                    )
                    AND EXISTS (
                        SELECT 1 FROM [reservation_details] rd 
                        WHERE rd.res_details_id = r.res_details_id AND rd.reservation_status = 'active'
                    )";

                var activeReservationIds = await _context.Database
                    .SqlQueryRaw<int>(activeReservationsSql, id)
                    .ToListAsync();

                if (activeReservationIds.Count > 0)
                {
                    TempData["Error"] = $"Cannot delete staff member. They have {activeReservationIds.Count} active reservation(s). Please reassign or complete these reservations first.";
                    return RedirectToAction("Admin");
                }

                // Delete staff member using raw SQL
                string deleteSql = @"DELETE FROM [staff] WHERE [staff_id] = {0}";
                await _context.Database.ExecuteSqlRawAsync(deleteSql, id);

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
                // Check if user already exists using raw SQL
                string checkUserSql = @"SELECT * FROM [user_credentials] WHERE [username] = {0}";
                var existingUsers = await _context.UserCredentials
                    .FromSqlRaw(checkUserSql, username)
                    .ToListAsync();

                if (existingUsers.Any())
                {
                    TempData["UserExists"] = "User already exists.";
                    return RedirectToAction("CreateUser");
                }

                // Create new user using raw SQL
                string insertUserSql = @"
                    INSERT INTO [user_credentials] ([username], [password], [user_type])
                    VALUES ({0}, {1}, {2})";

                await _context.Database.ExecuteSqlRawAsync(insertUserSql, username, password, type);

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
        
        // Fixed Receipt method - proper error handling and raw SQL that works
        [HttpGet] // Changed from POST to GET to match the form
        public async Task<IActionResult> Receipt(string receiptSearch)
        {
            if (string.IsNullOrWhiteSpace(receiptSearch))
            {
                TempData["Error"] = "Please enter a reservation ID to search.";
                return RedirectToAction("Index");
            }

            if (!int.TryParse(receiptSearch.Trim(), out int reservationId))
            {
                TempData["Error"] = "Please enter a valid reservation ID (numbers only).";
                return RedirectToAction("Index");
            }

            try
            {
                // Use FromSqlRaw instead of SqlQueryRaw to avoid column naming issues
                string sql = @"SELECT * FROM [reservations] WHERE [reservation_id] = {0}";
                var reservations = await _context.Reservations
                    .FromSqlRaw(sql, reservationId)
                    .ToListAsync();

                if (reservations.Any())
                {
                    return RedirectToAction("Confirmation", "Reservation", new { id = reservationId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for reservation {ReservationId}", reservationId);
                TempData["Error"] = "An error occurred while searching for the reservation. Please try again.";
                return RedirectToAction("Index");
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

            // Check if phone number already exists using raw SQL
            string checkPhoneSql = @"SELECT * FROM [staff] WHERE [tel_no] = {0}";
            var existingStaffList = await _context.Staff
                .FromSqlRaw(checkPhoneSql, staff.TelNo)
                .ToListAsync();

            if (existingStaffList.Any())
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

            // Insert staff using raw SQL
            string insertSql = @"
                INSERT INTO [staff] ([name], [surname], [job], [tel_no])
                VALUES ({0}, {1}, {2}, {3})";

            await _context.Database.ExecuteSqlRawAsync(insertSql, staff.Name, staff.Surname, staff.Job, staff.TelNo);

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
                
                // Get occupied tables using raw SQL - avoid COUNT
                string sql = @"
                    SELECT DISTINCT r.table_id
                    FROM [reservations] r
                    WHERE EXISTS (
                        SELECT 1 FROM [reservation_details] rd 
                        WHERE rd.res_details_id = r.res_details_id
                        AND CAST(rd.reservation_date AS DATE) = CAST({0} AS DATE)
                        AND rd.reservation_hour = {1}
                        AND rd.reservation_status = 'active'
                    )";

                var occupiedTables = await _context.Database
                    .SqlQueryRaw<int>(sql, reservationDate, time)
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
                string sql = @"SELECT * FROM [staff]";
                var staff = await _context.Staff
                    .FromSqlRaw(sql)
                    .ToListAsync();
                
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
                _logger.LogError(ex, "Error getting staff members");
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

                // Get all tables first
                string allTablesSql = @"SELECT * FROM [restaurant_tables]";
                var allTables = await _context.RestaurantTables
                    .FromSqlRaw(allTablesSql)
                    .ToListAsync();

                // Load staff for each table
                var allTablesWithStaff = new List<object>();
                foreach (var table in allTables)
                {
                    string staffSql = @"SELECT * FROM [staff] WHERE [staff_id] = {0}";
                    var staffList = await _context.Staff
                        .FromSqlRaw(staffSql, table.ServedById)
                        .ToListAsync();
                    
                    var staff = staffList.FirstOrDefault();
                    
                    allTablesWithStaff.Add(new
                    {
                        tableId = table.TableId,
                        minCapacity = table.MinCapacity,
                        maxCapacity = table.MaxCapacity,
                        serverName = staff != null ? $"{staff.Name} {staff.Surname}" : "No Server"
                    });
                }

                // Get occupied table IDs for the specific date/time using raw SQL - avoid COUNT
                string occupiedTablesSql = @"
                    SELECT DISTINCT r.table_id
                    FROM [reservations] r
                    WHERE EXISTS (
                        SELECT 1 FROM [reservation_details] rd 
                        WHERE rd.res_details_id = r.res_details_id
                        AND CAST(rd.reservation_date AS DATE) = CAST({0} AS DATE)
                        AND rd.reservation_hour = {1}
                        AND rd.reservation_status = 'active'
                    )";

                List<int> occupiedTableIds;

                if (currentReservationId.HasValue)
                {
                    occupiedTablesSql += " AND r.reservation_id != {2}";
                    occupiedTableIds = await _context.Database
                        .SqlQueryRaw<int>(occupiedTablesSql, reservationDate, time, currentReservationId.Value)
                        .ToListAsync();
                }
                else
                {
                    occupiedTableIds = await _context.Database
                        .SqlQueryRaw<int>(occupiedTablesSql, reservationDate, time)
                        .ToListAsync();
                }

                // Filter out occupied tables
                var availableTables = allTablesWithStaff
                    .Where(t => !occupiedTableIds.Contains((int)t.GetType().GetProperty("tableId").GetValue(t)))
                    .ToList();

                return Json(new { success = true, tables = availableTables });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available tables");
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