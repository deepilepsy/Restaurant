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
    Reservation? reservation = null;
    
    try
    {
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            
            var reservationSql = @"
                SELECT r.reservation_id, r.res_details_id, r.customer_id, r.table_id,
                       t.table_id as t_table_id, t.min_capacity, t.max_capacity, t.served_by_id,
                       s.staff_id, s.name as s_name, s.surname as s_surname, s.job, s.tel_no as s_tel,
                       c.customer_id as c_customer_id, c.name as c_name, c.surname as c_surname, c.tel_no as c_tel, c.email,
                       rd.res_details_id as rd_res_details_id, rd.guest_number, rd.created_at, rd.reservation_date, 
                       rd.reservation_hour, rd.reservation_status, rd.special_requests
                FROM reservations r
                INNER JOIN restaurant_tables t ON r.table_id = t.table_id
                LEFT JOIN staff s ON t.served_by_id = s.staff_id
                INNER JOIN customers c ON r.customer_id = c.customer_id
                INNER JOIN reservation_details rd ON r.res_details_id = rd.res_details_id
                WHERE r.reservation_id = @reservationId";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = reservationSql;
                command.Parameters.Add(new SqlParameter("@reservationId", SqlDbType.Int) { Value = id });
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        reservation = new Reservation
                        {
                            ReservationId = reader.GetInt32("reservation_id"),
                            ResDetailsId = reader.GetInt32("res_details_id"),
                            CustomerId = reader.GetInt32("customer_id"),
                            TableId = reader.GetInt32("table_id"),
                            
                            Table = new RestaurantTable
                            {
                                TableId = reader.GetInt32("t_table_id"),
                                MinCapacity = reader.GetInt32("min_capacity"),
                                MaxCapacity = reader.GetInt32("max_capacity"),
                                ServedById = reader.GetInt32("served_by_id"),
                                ServedBy = !reader.IsDBNull("staff_id") ? new Staff
                                {
                                    StaffId = reader.GetInt32("staff_id"),
                                    Name = reader.GetString("s_name"),
                                    Surname = reader.GetString("s_surname"),
                                    Job = reader.GetString("job"),
                                    TelNo = reader.GetString("s_tel")
                                } : null
                            },
                            
                            Customer = new Customer
                            {
                                CustomerId = reader.GetInt32("c_customer_id"),
                                Name = reader.GetString("c_name"),
                                Surname = reader.GetString("c_surname"),
                                TelNo = reader.GetString("c_tel"),
                                Email = reader.IsDBNull("email") ? null : reader.GetString("email")
                            },
                            
                            ReservationDetail = new ReservationDetail
                            {
                                ResDetailsId = reader.GetInt32("rd_res_details_id"),
                                GuestNumber = reader.GetInt32("guest_number"),
                                CreatedAt = reader.GetDateTime("created_at"),
                                ReservationDate = reader.GetDateTime("reservation_date"),
                                ReservationHour = reader.GetString("reservation_hour"),
                                ReservationStatus = reader.GetString("reservation_status"),
                                SpecialRequests = reader.IsDBNull("special_requests") ? null : reader.GetString("special_requests")
                            }
                        };
                    }
                }
            }
            
            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }
    catch (Exception ex)
    {
        TempData["Error"] = "An error occurred while loading the reservation.";
        return GoBackToPanel();
    }

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
    // Remove validation for navigation properties
    ModelState.Remove("Customer");
    ModelState.Remove("Table");
    ModelState.Remove("ReservationDetail");

    Reservation? reservation = null;
    
    try
    {
        // Load the existing reservation using SQL
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            
            var reservationSql = @"
                SELECT r.reservation_id, r.res_details_id, r.customer_id, r.table_id,
                       c.customer_id as c_customer_id, c.name as c_name, c.surname as c_surname, c.tel_no as c_tel, c.email,
                       rd.res_details_id as rd_res_details_id, rd.guest_number, rd.created_at, rd.reservation_date, 
                       rd.reservation_hour, rd.reservation_status, rd.special_requests
                FROM reservations r
                INNER JOIN customers c ON r.customer_id = c.customer_id
                INNER JOIN reservation_details rd ON r.res_details_id = rd.res_details_id
                WHERE r.reservation_id = @reservationId";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = reservationSql;
                command.Parameters.Add(new SqlParameter("@reservationId", SqlDbType.Int) { Value = model.ReservationId });
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        reservation = new Reservation
                        {
                            ReservationId = reader.GetInt32("reservation_id"),
                            ResDetailsId = reader.GetInt32("res_details_id"),
                            CustomerId = reader.GetInt32("customer_id"),
                            TableId = reader.GetInt32("table_id"),
                            
                            Customer = new Customer
                            {
                                CustomerId = reader.GetInt32("c_customer_id"),
                                Name = reader.GetString("c_name"),
                                Surname = reader.GetString("c_surname"),
                                TelNo = reader.GetString("c_tel"),
                                Email = reader.IsDBNull("email") ? null : reader.GetString("email")
                            },
                            
                            ReservationDetail = new ReservationDetail
                            {
                                ResDetailsId = reader.GetInt32("rd_res_details_id"),
                                GuestNumber = reader.GetInt32("guest_number"),
                                CreatedAt = reader.GetDateTime("created_at"),
                                ReservationDate = reader.GetDateTime("reservation_date"),
                                ReservationHour = reader.GetString("reservation_hour"),
                                ReservationStatus = reader.GetString("reservation_status"),
                                SpecialRequests = reader.IsDBNull("special_requests") ? null : reader.GetString("special_requests")
                            }
                        };
                    }
                }
            }
            
            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }
    catch (Exception ex)
    {
        TempData["Error"] = "An error occurred while loading the reservation.";
        return GoBackToPanel();
    }

    if (reservation == null)
    {
        TempData["Error"] = "Reservation not found.";
        return GoBackToPanel();
    }

    try
    {
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Update customer information
            if (reservation.Customer != null)
            {
                var customerName = Request.Form["Customer.Name"].ToString().Trim();
                var customerSurname = Request.Form["Customer.Surname"].ToString().Trim();
                var customerTelNo = Request.Form["Customer.TelNo"].ToString().Trim();
                var customerEmail = string.IsNullOrWhiteSpace(Request.Form["Customer.Email"]) ? 
                    null : Request.Form["Customer.Email"].ToString().Trim();

                var updateCustomerSql = @"
                    UPDATE customers 
                    SET name = @name, surname = @surname, tel_no = @telNo, email = @email
                    WHERE customer_id = @customerId";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = updateCustomerSql;
                    command.Parameters.Add(new SqlParameter("@name", SqlDbType.VarChar, 255) { Value = customerName });
                    command.Parameters.Add(new SqlParameter("@surname", SqlDbType.VarChar, 255) { Value = customerSurname });
                    command.Parameters.Add(new SqlParameter("@telNo", SqlDbType.VarChar, 25) { Value = customerTelNo });
                    command.Parameters.Add(new SqlParameter("@email", SqlDbType.VarChar, 255) { Value = (object)customerEmail ?? DBNull.Value });
                    command.Parameters.Add(new SqlParameter("@customerId", SqlDbType.Int) { Value = reservation.Customer.CustomerId });
                    
                    await command.ExecuteNonQueryAsync();
                }
            }

            // Update reservation details
            if (reservation.ReservationDetail != null)
            {
                var specialRequests = string.IsNullOrWhiteSpace(Request.Form["ReservationDetail.SpecialRequests"]) ? 
                    null : Request.Form["ReservationDetail.SpecialRequests"].ToString().Trim();

                // Only update reservation details if status is active
                if (reservation.ReservationDetail.ReservationStatus == "active")
                {
                    // Parse form values
                    if (!int.TryParse(Request.Form["ReservationDetail.GuestNumber"].ToString().Trim(), out int guestNumber))
                    {
                        TempData["Error"] = "Invalid guest number format.";
                        await connection.CloseAsync();
                        return View(reservation);
                    }

                    // Get table ID
                    int tableId = reservation.TableId; // Default to current table
                    var tableIdFormValue = Request.Form["TableId"].ToString().Trim();
                    
                    if (string.IsNullOrEmpty(tableIdFormValue))
                    {
                        var currentTableIdValue = Request.Form["CurrentTableId"].ToString().Trim();
                        if (!string.IsNullOrEmpty(currentTableIdValue))
                        {
                            if (!int.TryParse(currentTableIdValue, out tableId))
                            {
                                TempData["Error"] = $"Invalid current table ID format. Received: '{currentTableIdValue}'";
                                await connection.CloseAsync();
                                return View(reservation);
                            }
                        }
                    }
                    else
                    {
                        if (!int.TryParse(tableIdFormValue, out tableId))
                        {
                            TempData["Error"] = $"Invalid table ID format. Received: '{tableIdFormValue}'";
                            await connection.CloseAsync();
                            return View(reservation);
                        }
                    }

                    if (!DateTime.TryParse(Request.Form["ReservationDetail.ReservationDate"].ToString().Trim(), out DateTime reservationDate))
                    {
                        TempData["Error"] = "Invalid reservation date format.";
                        await connection.CloseAsync();
                        return View(reservation);
                    }

                    var reservationHour = Request.Form["ReservationDetail.ReservationHour"].ToString().Trim();

                    // Validate the selected table can accommodate the guest count
                    RestaurantTable? selectedTable = null;
                    var tableSql = @"
                        SELECT t.table_id, t.min_capacity, t.max_capacity, t.served_by_id,
                               s.staff_id, s.name, s.surname, s.job, s.tel_no
                        FROM restaurant_tables t
                        LEFT JOIN staff s ON t.served_by_id = s.staff_id
                        WHERE t.table_id = @tableId";

                    using (var tableCommand = connection.CreateCommand())
                    {
                        tableCommand.CommandText = tableSql;
                        tableCommand.Parameters.Add(new SqlParameter("@tableId", SqlDbType.Int) { Value = tableId });
                        
                        using (var tableReader = await tableCommand.ExecuteReaderAsync())
                        {
                            if (await tableReader.ReadAsync())
                            {
                                selectedTable = new RestaurantTable
                                {
                                    TableId = tableReader.GetInt32("table_id"),
                                    MinCapacity = tableReader.GetInt32("min_capacity"),
                                    MaxCapacity = tableReader.GetInt32("max_capacity"),
                                    ServedById = tableReader.GetInt32("served_by_id")
                                };

                                if (!tableReader.IsDBNull("staff_id"))
                                {
                                    selectedTable.ServedBy = new Staff
                                    {
                                        StaffId = tableReader.GetInt32("staff_id"),
                                        Name = tableReader.GetString("name"),
                                        Surname = tableReader.GetString("surname"),
                                        Job = tableReader.GetString("job"),
                                        TelNo = tableReader.GetString("tel_no")
                                    };
                                }
                            }
                        }
                    }

                    if (selectedTable == null)
                    {
                        TempData["Error"] = "Selected table not found.";
                        await connection.CloseAsync();
                        return View(reservation);
                    }

                    if (guestNumber < selectedTable.MinCapacity || guestNumber > selectedTable.MaxCapacity)
                    {
                        TempData["Error"] = $"Table {tableId} capacity is {selectedTable.MinCapacity}-{selectedTable.MaxCapacity} people. You selected {guestNumber} guests.";
                        await connection.CloseAsync();
                        return View(reservation);
                    }

                    // Check if the table is available for the selected date/time (excluding current reservation)
                    var availabilitySql = @"
                        SELECT COUNT(*)
                        FROM reservations r
                        INNER JOIN reservation_details rd ON r.res_details_id = rd.res_details_id
                        WHERE r.table_id = @tableId 
                        AND CAST(rd.reservation_date AS DATE) = CAST(@reservationDate AS DATE)
                        AND rd.reservation_hour = @reservationHour
                        AND rd.reservation_status = 'active'
                        AND r.reservation_id != @currentReservationId";

                    using (var availCommand = connection.CreateCommand())
                    {
                        availCommand.CommandText = availabilitySql;
                        availCommand.Parameters.Add(new SqlParameter("@tableId", SqlDbType.Int) { Value = tableId });
                        availCommand.Parameters.Add(new SqlParameter("@reservationDate", SqlDbType.DateTime) { Value = reservationDate });
                        availCommand.Parameters.Add(new SqlParameter("@reservationHour", SqlDbType.VarChar, 10) { Value = reservationHour });
                        availCommand.Parameters.Add(new SqlParameter("@currentReservationId", SqlDbType.Int) { Value = model.ReservationId });
                        
                        var conflictCount = (int)await availCommand.ExecuteScalarAsync();
                        
                        if (conflictCount > 0)
                        {
                            TempData["Error"] = $"Table {tableId} is already reserved for {reservationDate.ToShortDateString()} at {reservationHour}.";
                            await connection.CloseAsync();
                            return View(reservation);
                        }
                    }

                    // Check if date is not in the past
                    if (reservationDate.Date < DateTime.Today)
                    {
                        TempData["Error"] = "Cannot set reservation date to a past date.";
                        await connection.CloseAsync();
                        return View(reservation);
                    }

                    // Update reservation details and table assignment
                    var updateReservationDetailSql = @"
                        UPDATE reservation_details 
                        SET guest_number = @guestNumber, reservation_date = @reservationDate, 
                            reservation_hour = @reservationHour, special_requests = @specialRequests
                        WHERE res_details_id = @resDetailsId";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = updateReservationDetailSql;
                        command.Parameters.Add(new SqlParameter("@guestNumber", SqlDbType.Int) { Value = guestNumber });
                        command.Parameters.Add(new SqlParameter("@reservationDate", SqlDbType.DateTime) { Value = reservationDate });
                        command.Parameters.Add(new SqlParameter("@reservationHour", SqlDbType.VarChar, 10) { Value = reservationHour });
                        command.Parameters.Add(new SqlParameter("@specialRequests", SqlDbType.Text) { Value = (object)specialRequests ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@resDetailsId", SqlDbType.Int) { Value = reservation.ReservationDetail.ResDetailsId });
                        
                        await command.ExecuteNonQueryAsync();
                    }

                    // Update table assignment if it changed
                    if (tableId != reservation.TableId)
                    {
                        var updateTableSql = "UPDATE reservations SET table_id = @tableId WHERE reservation_id = @reservationId";
                        
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = updateTableSql;
                            command.Parameters.Add(new SqlParameter("@tableId", SqlDbType.Int) { Value = tableId });
                            command.Parameters.Add(new SqlParameter("@reservationId", SqlDbType.Int) { Value = model.ReservationId });
                            
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
                else
                {
                    // Only update special requests for non-active reservations
                    var updateSpecialRequestsSql = @"
                        UPDATE reservation_details 
                        SET special_requests = @specialRequests
                        WHERE res_details_id = @resDetailsId";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = updateSpecialRequestsSql;
                        command.Parameters.Add(new SqlParameter("@specialRequests", SqlDbType.Text) { Value = (object)specialRequests ?? DBNull.Value });
                        command.Parameters.Add(new SqlParameter("@resDetailsId", SqlDbType.Int) { Value = reservation.ReservationDetail.ResDetailsId });
                        
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }

            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }

        TempData["Success"] = $"Reservation #{model.ReservationId} has been updated successfully.";
    }
    catch (Exception ex)
    {
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

    try
    {
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Check if reservation exists and get its status
            var checkSql = @"
                SELECT rd.reservation_status, rd.res_details_id
                FROM reservations r
                INNER JOIN reservation_details rd ON r.res_details_id = rd.res_details_id
                WHERE r.reservation_id = @reservationId";

            string? reservationStatus = null;
            int? resDetailsId = null;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = checkSql;
                command.Parameters.Add(new SqlParameter("@reservationId", SqlDbType.Int) { Value = id });
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        reservationStatus = reader.GetString("reservation_status");
                        resDetailsId = reader.GetInt32("res_details_id");
                    }
                }
            }

            if (reservationStatus == null || resDetailsId == null)
            {
                TempData["Error"] = "Reservation not found.";
                await connection.CloseAsync();
                return GoBackToPanel();
            }

            if (reservationStatus != "active")
            {
                TempData["Error"] = "Only active reservations can be deleted.";
                await connection.CloseAsync();
                return RedirectToAction("Edit", new { id = id });
            }

            // Delete reservation first (to avoid foreign key constraint)
            var deleteReservationSql = "DELETE FROM reservations WHERE reservation_id = @reservationId";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = deleteReservationSql;
                command.Parameters.Add(new SqlParameter("@reservationId", SqlDbType.Int) { Value = id });
                
                await command.ExecuteNonQueryAsync();
            }

            // Delete reservation details
            var deleteDetailsSql = "DELETE FROM reservation_details WHERE res_details_id = @resDetailsId";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = deleteDetailsSql;
                command.Parameters.Add(new SqlParameter("@resDetailsId", SqlDbType.Int) { Value = resDetailsId.Value });
                
                await command.ExecuteNonQueryAsync();
            }

            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }

        TempData["Success"] = $"Reservation #{id} has been permanently deleted.";
    }
    catch (Exception ex)
    {
        TempData["Error"] = "An error occurred while deleting the reservation. Please try again.";
    }

    return GoBackToPanel();
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
public async Task<IActionResult> EditStaff(int id)
{
    if (HttpContext.Session.GetString("admin") != "true")
    {
        return RedirectToAction("Login");
    }

    Staff? staff = null;
    
    try
    {
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            
            var staffSql = "SELECT staff_id, name, surname, job, tel_no FROM staff WHERE staff_id = @staffId";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = staffSql;
                command.Parameters.Add(new SqlParameter("@staffId", SqlDbType.Int) { Value = id });
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        staff = new Staff
                        {
                            StaffId = reader.GetInt32("staff_id"),
                            Name = reader.GetString("name"),
                            Surname = reader.GetString("surname"),
                            Job = reader.GetString("job"),
                            TelNo = reader.GetString("tel_no")
                        };
                    }
                }
            }
            
            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }
    catch (Exception ex)
    {
        TempData["Error"] = "An error occurred while loading the staff member.";
        return RedirectToAction("Admin");
    }

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

    try
    {
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Check if phone number exists for another staff member
            var phoneCheckSql = "SELECT COUNT(*) FROM staff WHERE tel_no = @telNo AND staff_id != @staffId";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = phoneCheckSql;
                command.Parameters.Add(new SqlParameter("@telNo", SqlDbType.VarChar, 25) { Value = staff.TelNo });
                command.Parameters.Add(new SqlParameter("@staffId", SqlDbType.Int) { Value = staff.StaffId });
                
                var existingCount = (int)await command.ExecuteScalarAsync();
                
                if (existingCount > 0)
                {
                    ModelState.AddModelError("TelNo", "Another staff member with this phone number already exists.");
                    await connection.CloseAsync();
                    return View(staff);
                }
            }

            // Check if job is valid
            var validJobs = new[] { "waiter", "chef", "manager", "cashier", "dishwasher", "cleaner" };
            if (!validJobs.Contains(staff.Job.ToLower()))
            {
                ModelState.AddModelError("Job", "Please select a valid job position.");
                await connection.CloseAsync();
                return View(staff);
            }

            // Update staff member
            var updateStaffSql = @"
                UPDATE staff 
                SET name = @name, surname = @surname, job = @job, tel_no = @telNo 
                WHERE staff_id = @staffId";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = updateStaffSql;
                command.Parameters.Add(new SqlParameter("@name", SqlDbType.VarChar, 255) { Value = staff.Name });
                command.Parameters.Add(new SqlParameter("@surname", SqlDbType.VarChar, 255) { Value = staff.Surname });
                command.Parameters.Add(new SqlParameter("@job", SqlDbType.VarChar, 25) { Value = staff.Job });
                command.Parameters.Add(new SqlParameter("@telNo", SqlDbType.VarChar, 25) { Value = staff.TelNo });
                command.Parameters.Add(new SqlParameter("@staffId", SqlDbType.Int) { Value = staff.StaffId });
                
                await command.ExecuteNonQueryAsync();
            }

            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }

        TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully updated.";
        return RedirectToAction("Admin");
    }
    catch (Exception ex)
    {
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

    Staff? staff = null;
    int activeReservations = 0;

    try
    {
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            
            // Get staff details
            var staffSql = "SELECT staff_id, name, surname, job, tel_no FROM staff WHERE staff_id = @staffId";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = staffSql;
                command.Parameters.Add(new SqlParameter("@staffId", SqlDbType.Int) { Value = id });
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        staff = new Staff
                        {
                            StaffId = reader.GetInt32("staff_id"),
                            Name = reader.GetString("name"),
                            Surname = reader.GetString("surname"),
                            Job = reader.GetString("job"),
                            TelNo = reader.GetString("tel_no")
                        };
                    }
                }
            }

            if (staff == null)
            {
                TempData["Error"] = "Staff member not found.";
                await connection.CloseAsync();
                return RedirectToAction("Admin");
            }

            // Check if staff has active reservations (tables served by this staff member)
            var activeReservationsSql = @"
                SELECT COUNT(*)
                FROM reservations r
                INNER JOIN restaurant_tables t ON r.table_id = t.table_id
                INNER JOIN reservation_details rd ON r.res_details_id = rd.res_details_id
                WHERE t.served_by_id = @staffId AND rd.reservation_status = 'active'";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = activeReservationsSql;
                command.Parameters.Add(new SqlParameter("@staffId", SqlDbType.Int) { Value = id });
                
                activeReservations = (int)await command.ExecuteScalarAsync();
            }

            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }
    catch (Exception ex)
    {
        TempData["Error"] = "An error occurred while loading the staff member.";
        return RedirectToAction("Admin");
    }

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

    try
    {
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Get staff details first
            Staff? staff = null;
            var staffSql = "SELECT staff_id, name, surname, job, tel_no FROM staff WHERE staff_id = @staffId";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = staffSql;
                command.Parameters.Add(new SqlParameter("@staffId", SqlDbType.Int) { Value = id });
                
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        staff = new Staff
                        {
                            StaffId = reader.GetInt32("staff_id"),
                            Name = reader.GetString("name"),
                            Surname = reader.GetString("surname"),
                            Job = reader.GetString("job"),
                            TelNo = reader.GetString("tel_no")
                        };
                    }
                }
            }

            if (staff == null)
            {
                TempData["Error"] = "Staff member not found.";
                await connection.CloseAsync();
                return RedirectToAction("Admin");
            }

            // Check if staff has active reservations
            var activeReservationsSql = @"
                SELECT COUNT(*)
                FROM reservations r
                INNER JOIN restaurant_tables t ON r.table_id = t.table_id
                INNER JOIN reservation_details rd ON r.res_details_id = rd.res_details_id
                WHERE t.served_by_id = @staffId AND rd.reservation_status = 'active'";

            int activeReservations;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = activeReservationsSql;
                command.Parameters.Add(new SqlParameter("@staffId", SqlDbType.Int) { Value = id });
                
                activeReservations = (int)await command.ExecuteScalarAsync();
            }

            if (activeReservations > 0)
            {
                TempData["Error"] = $"Cannot delete staff member. They have {activeReservations} active reservation(s). Please reassign or complete these reservations first.";
                await connection.CloseAsync();
                return RedirectToAction("Admin");
            }

            // Delete staff member
            var deleteStaffSql = "DELETE FROM staff WHERE staff_id = @staffId";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = deleteStaffSql;
                command.Parameters.Add(new SqlParameter("@staffId", SqlDbType.Int) { Value = id });
                
                await command.ExecuteNonQueryAsync();
            }

            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }

            TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully deleted.";
        }
    }
    catch (Exception ex)
    {
        TempData["Error"] = "An error occurred while deleting the staff member.";
    }

    return RedirectToAction("Admin");
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
        using (var connection = _context.Database.GetDbConnection())
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Check if user already exists
            var userExistsSql = "SELECT COUNT(*) FROM user_credentials WHERE username = @username";
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = userExistsSql;
                command.Parameters.Add(new SqlParameter("@username", SqlDbType.VarChar, 16) { Value = username });
                
                var existingCount = (int)await command.ExecuteScalarAsync();
                
                if (existingCount > 0)
                {
                    TempData["UserExists"] = "User already exists.";
                    await connection.CloseAsync();
                    return RedirectToAction("CreateUser");
                }
            }

            // Create new user
            var createUserSql = @"
                INSERT INTO user_credentials (username, password, user_type)
                VALUES (@username, @password, @userType)";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = createUserSql;
                command.Parameters.Add(new SqlParameter("@username", SqlDbType.VarChar, 16) { Value = username });
                command.Parameters.Add(new SqlParameter("@password", SqlDbType.VarChar, 16) { Value = password });
                command.Parameters.Add(new SqlParameter("@userType", SqlDbType.VarChar, 10) { Value = type });
                
                await command.ExecuteNonQueryAsync();
            }

            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }

            TempData["Success"] = $"User {username} has been successfully created.";
        }
    }
    catch (Exception ex)
    {
        TempData["Error"] = "An error occurred while creating the user.";
    }

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
