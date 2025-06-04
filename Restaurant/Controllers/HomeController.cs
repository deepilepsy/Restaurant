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
            string sql = "SELECT * FROM [user_credentials] WHERE [username] = {0} AND [password] = {1}";
            var users = await _context.UserCredentials.FromSqlRaw(sql, username, password).ToListAsync();
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

        // Helper method to load reservation with all related data
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

            // Load table
            string tableSql = "SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
            var tables = await _context.RestaurantTables.FromSqlRaw(tableSql, reservation.TableId).ToListAsync();
            reservation.Table = tables.FirstOrDefault();

            // Load server
            if (reservation.Table != null)
            {
                string staffSql = "SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var staff = await _context.Staff.FromSqlRaw(staffSql, reservation.Table.ServedById).ToListAsync();
                reservation.Table.ServedBy = staff.FirstOrDefault();
            }

            return reservation;
        }

        // Helper method to load all active reservations
        private async Task<List<Reservation>> LoadActiveReservations()
        {
            var today = DateTime.Today;
            string reservationsSql = @"
                SELECT * FROM [reservations] r
                WHERE EXISTS (
                    SELECT 1 FROM [reservation_details] rd 
                    WHERE rd.res_details_id = r.res_details_id 
                    AND rd.reservation_status = 'active' 
                    AND rd.reservation_date >= {0}
                )";

            var reservations = await _context.Reservations.FromSqlRaw(reservationsSql, today).ToListAsync();

            // Load related data for each reservation
            foreach (var reservation in reservations)
            {
                await LoadReservationRelatedData(reservation);
            }

            // Sort by date and time
            return reservations
                .Where(r => r.ReservationDetail != null)
                .OrderBy(r => r.ReservationDetail.ReservationDate)
                .ThenBy(r => r.ReservationDetail.ReservationHour)
                .ToList();
        }

        // Helper method to load related data for a single reservation
        private async Task LoadReservationRelatedData(Reservation reservation)
        {
            // Load customer
            string customerSql = "SELECT * FROM [customers] WHERE [customer_id] = {0}";
            var customers = await _context.Customers.FromSqlRaw(customerSql, reservation.CustomerId).ToListAsync();
            reservation.Customer = customers.FirstOrDefault();

            // Load reservation details
            string detailsSql = "SELECT * FROM [reservation_details] WHERE [res_details_id] = {0}";
            var details = await _context.ReservationDetails.FromSqlRaw(detailsSql, reservation.ResDetailsId).ToListAsync();
            reservation.ReservationDetail = details.FirstOrDefault();

            // Load table
            string tableSql = "SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
            var tables = await _context.RestaurantTables.FromSqlRaw(tableSql, reservation.TableId).ToListAsync();
            reservation.Table = tables.FirstOrDefault();

            // Load server
            if (reservation.Table != null)
            {
                string staffSql = "SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var staff = await _context.Staff.FromSqlRaw(staffSql, reservation.Table.ServedById).ToListAsync();
                reservation.Table.ServedBy = staff.FirstOrDefault();
            }
        }

        public async Task<IActionResult> Admin()
        {
            if (HttpContext.Session.GetString("admin") != "true")
            {
                return RedirectToAction("Login");
            }

            // Get staff members
            string staffSql = "SELECT * FROM [staff]";
            var staffMembers = await _context.Staff.FromSqlRaw(staffSql).ToListAsync();

            // Calculate statistics
            string totalStaffSql = "SELECT [staff_id] FROM [staff]";
            var allStaffIds = await _context.Database.SqlQueryRaw<int>(totalStaffSql).ToListAsync();
            var totalStaff = allStaffIds.Count;

            string waitersSql = "SELECT [staff_id] FROM [staff] WHERE LOWER([job]) = 'waiter'";
            var waiterIds = await _context.Database.SqlQueryRaw<int>(waitersSql).ToListAsync();
            var totalWaiters = waiterIds.Count;

            string activeReservationsSql = "SELECT [res_details_id] FROM [reservation_details] WHERE [reservation_status] = 'active'";
            var activeReservationIds = await _context.Database.SqlQueryRaw<int>(activeReservationsSql).ToListAsync();
            var activeReservations = activeReservationIds.Count;

            // Load upcoming reservations
            var upcomingReservations = await LoadActiveReservations();

            var viewModel = new AdminPanelView
            {
                StaffMembers = staffMembers,
                TotalStaff = totalStaff,
                TotalWaiters = totalWaiters,
                ActiveReservations = activeReservations,
                UpcomingReservations = upcomingReservations
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Staff()
        {
            if (HttpContext.Session.GetString("staff") != "true")
            {
                return RedirectToAction("Login");
            }

            var activeReservations = await LoadActiveReservations();

            var viewModel = new AdminPanelView
            {
                UpcomingReservations = activeReservations,
                StaffMembers = new List<Staff>()
            };

            return View(viewModel);
        }

        public async Task<IActionResult> Booking(int? tableId, string? date, string? time, int? guests)
        {
            // Load tables
            string tablesSql = "SELECT * FROM [restaurant_tables]";
            var tables = await _context.RestaurantTables.FromSqlRaw(tablesSql).ToListAsync();

            // Load staff for each table
            foreach (var table in tables)
            {
                string staffSql = "SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var staffList = await _context.Staff.FromSqlRaw(staffSql, table.ServedById).ToListAsync();
                table.ServedBy = staffList.FirstOrDefault();
            }

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

        [HttpPost]
        public async Task<IActionResult> UpdateReservationStatus(int reservationId, string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                return Json(new { success = false, message = "Status parameter is required" });
            }

            status = status.Trim().Trim('\'', '"');
    
            if (status != "cancelled" && status != "checked-in")
            {
                return Json(new { success = false, message = $"Invalid status value: {status}" });
            }

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
                return Json(new { success = false, message = "Reservation not found" });
            }
        }

        public async Task<IActionResult> OrderManagement(int reservationId)
        {
            var reservation = await LoadCompleteReservation(reservationId);

            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found";
                return RedirectToAction("Staff");
            }

            // Get all menu items with SQL sorting
            string menuSql = @"
        SELECT * FROM [menu_items] 
        ORDER BY [category], 
                 ISNULL([subcategory], '') ASC, 
                 [item_name]";
            var menuItems = await _context.MenuItems.FromSqlRaw(menuSql).ToListAsync();

            // Check for existing receipt
            string receiptSql = "SELECT TOP 1 * FROM [receipts] WHERE [reservation_id] = {0}";
            var existingReceipts = await _context.Receipts.FromSqlRaw(receiptSql, reservationId).ToListAsync();
            var existingReceipt = existingReceipts.FirstOrDefault();

            // Load receipt items if receipt exists
            if (existingReceipt != null)
            {
                // Get receipt items using separate SQL queries
                string receiptItemsSql = "SELECT * FROM [receipt_items] WHERE [receipt_id] = {0}";
                var receiptItems = await _context.ReceiptItems.FromSqlRaw(receiptItemsSql, existingReceipt.ReceiptId).ToListAsync();

                // Load menu items for each receipt item using SQL
                foreach (var item in receiptItems)
                {
                    string menuItemSql = "SELECT TOP 1 * FROM [menu_items] WHERE [item_id] = {0}";
                    var menuItemList = await _context.MenuItems.FromSqlRaw(menuItemSql, item.ItemId).ToListAsync();
                    item.MenuItem = menuItemList.Count > 0 ? menuItemList[0] : null;
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

        [HttpPost]
        public async Task<IActionResult> ProcessOrder([FromBody] OrderSubmissionModel orderData)
        {
            if (orderData?.Items == null || orderData.Items.Count == 0)
            {
                return Json(new { success = false, message = "No items in order" });
            }

            // Check if receipt already exists
            string checkReceiptSql = "SELECT * FROM [receipts] WHERE [reservation_id] = {0}";
            var existingReceipts = await _context.Receipts.FromSqlRaw(checkReceiptSql, orderData.ReservationId).ToListAsync();
            var existingReceipt = existingReceipts.FirstOrDefault();

            if (existingReceipt != null)
            {
                // Update existing receipt
                string updateReceiptSql = "UPDATE [receipts] SET [total_amount] = {0} WHERE [receipt_id] = {1}";
                await _context.Database.ExecuteSqlRawAsync(updateReceiptSql, orderData.TotalAmount, existingReceipt.ReceiptId);
                
                // Delete existing receipt items
                string deleteItemsSql = "DELETE FROM [receipt_items] WHERE [receipt_id] = {0}";
                await _context.Database.ExecuteSqlRawAsync(deleteItemsSql, existingReceipt.ReceiptId);

                // Add new receipt items
                foreach (var item in orderData.Items)
                {
                    string insertItemSql = @"
                        INSERT INTO [receipt_items] ([receipt_id], [item_id], [quantity], [unit_price], [special_notes])
                        VALUES ({0}, {1}, {2}, {3}, {4})";
                    
                    await _context.Database.ExecuteSqlRawAsync(insertItemSql, 
                        existingReceipt.ReceiptId, item.ItemId, item.Quantity, item.UnitPrice, orderData.SpecialNotes ?? "");
                }

                TempData["Success"] = "Order updated successfully!";
            }
            else
            {
                // Create new receipt
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
                        newReceiptId, item.ItemId, item.Quantity, item.UnitPrice, orderData.SpecialNotes ?? "");
                }

                TempData["Success"] = "Order created successfully!";
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetTableCapacities()
        {
            string sql = "SELECT * FROM [restaurant_tables]";
            var tables = await _context.RestaurantTables.FromSqlRaw(sql).ToListAsync();
            
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

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var reservation = await LoadCompleteReservation(id);

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

            var reservation = await LoadCompleteReservation(model.ReservationId);

            if (reservation == null)
            {
                TempData["Error"] = "Reservation not found.";
                return GoBackToPanel();
            }

            // Get and validate form values
            var customerName = Request.Form["Customer.Name"].ToString()?.Trim() ?? "";
            var customerSurname = Request.Form["Customer.Surname"].ToString()?.Trim() ?? "";
            var customerTelNo = Request.Form["Customer.TelNo"].ToString()?.Trim() ?? "";
            var customerEmail = Request.Form["Customer.Email"].ToString()?.Trim();
            
            if (string.IsNullOrWhiteSpace(customerEmail))
            {
                customerEmail = null;
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(customerSurname) || string.IsNullOrWhiteSpace(customerTelNo))
            {
                TempData["Error"] = "Customer name, surname, and phone number are required.";
                return View(reservation);
            }

            // Update customer information
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

            // Only update other details if status is active
            if (reservation.ReservationDetail?.ReservationStatus == "active")
            {
                // Parse and validate other form values
                var guestNumberStr = Request.Form["ReservationDetail.GuestNumber"].ToString()?.Trim() ?? "";
                if (!int.TryParse(guestNumberStr, out int guestNumber) || guestNumber < 1)
                {
                    TempData["Error"] = "Invalid guest number.";
                    return View(reservation);
                }

                int tableId = reservation.TableId;
                var tableIdFormValue = Request.Form["TableId"].ToString()?.Trim() ?? "";
                
                if (!string.IsNullOrEmpty(tableIdFormValue))
                {
                    if (!int.TryParse(tableIdFormValue, out tableId))
                    {
                        TempData["Error"] = "Invalid table ID.";
                        return View(reservation);
                    }
                }

                var reservationDateStr = Request.Form["ReservationDetail.ReservationDate"].ToString()?.Trim() ?? "";
                if (!DateTime.TryParse(reservationDateStr, out DateTime reservationDate))
                {
                    TempData["Error"] = "Invalid reservation date.";
                    return View(reservation);
                }

                var reservationHour = Request.Form["ReservationDetail.ReservationHour"].ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(reservationHour))
                {
                    TempData["Error"] = "Reservation hour is required.";
                    return View(reservation);
                }

                // Validate the selected table exists
                string checkTableSql = "SELECT * FROM [restaurant_tables] WHERE [table_id] = {0}";
                var selectedTables = await _context.RestaurantTables.FromSqlRaw(checkTableSql, tableId).ToListAsync();
                var selectedTable = selectedTables.FirstOrDefault();

                if (selectedTable == null)
                {
                    TempData["Error"] = $"Selected table {tableId} not found.";
                    return View(reservation);
                }

                if (guestNumber < selectedTable.MinCapacity || guestNumber > selectedTable.MaxCapacity)
                {
                    TempData["Error"] = $"Table {tableId} capacity is {selectedTable.MinCapacity}-{selectedTable.MaxCapacity} people.";
                    return View(reservation);
                }

                // Check for conflicts
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
                    TempData["Error"] = $"Table {tableId} is already reserved for that date and time.";
                    return View(reservation);
                }

                if (reservationDate.Date < DateTime.Today)
                {
                    TempData["Error"] = "Cannot set reservation date to a past date.";
                    return View(reservation);
                }

                // Update reservation details
                string updateReservationDetailsSql = @"
                    UPDATE [reservation_details] 
                    SET [guest_number] = {0}, [reservation_date] = {1}, [reservation_hour] = {2}
                    WHERE [res_details_id] = {3}";

                await _context.Database.ExecuteSqlRawAsync(updateReservationDetailsSql, 
                    guestNumber, reservationDate, reservationHour, reservation.ResDetailsId);

                // Update table if changed
                if (tableId != reservation.TableId)
                {
                    string updateTableSql = "UPDATE [reservations] SET [table_id] = {0} WHERE [reservation_id] = {1}";
                    await _context.Database.ExecuteSqlRawAsync(updateTableSql, tableId, model.ReservationId);
                }
            }

            TempData["Success"] = $"Reservation #{model.ReservationId} has been updated successfully.";
            return GoBackToPanel();
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CheckUserLoggedIn())
            {
                return RedirectToAction("Login");
            }

            var reservation = await LoadCompleteReservation(id);

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

            // Delete reservation and its details
            string deleteReservationSql = "DELETE FROM [reservations] WHERE [reservation_id] = {0}";
            await _context.Database.ExecuteSqlRawAsync(deleteReservationSql, id);

            string deleteDetailsSql = "DELETE FROM [reservation_details] WHERE [res_details_id] = {0}";
            await _context.Database.ExecuteSqlRawAsync(deleteDetailsSql, reservation.ResDetailsId);

            TempData["Success"] = $"Reservation #{id} has been permanently deleted.";
            return GoBackToPanel();
        }

        [HttpGet]
        public async Task<IActionResult> EditStaff(int id)
        {
            if (HttpContext.Session.GetString("admin") != "true")
            {
                return RedirectToAction("Login");
            }

            string sql = "SELECT * FROM [staff] WHERE [staff_id] = {0}";
            var staffList = await _context.Staff.FromSqlRaw(sql, id).ToListAsync();
            var staff = staffList.FirstOrDefault();

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
            string checkPhoneSql = "SELECT * FROM [staff] WHERE [tel_no] = {0} AND [staff_id] != {1}";
            var existingStaffList = await _context.Staff.FromSqlRaw(checkPhoneSql, staff.TelNo, staff.StaffId).ToListAsync();

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

            // Update staff member
            string updateSql = @"
                UPDATE [staff] 
                SET [name] = {0}, [surname] = {1}, [job] = {2}, [tel_no] = {3}
                WHERE [staff_id] = {4}";

            await _context.Database.ExecuteSqlRawAsync(updateSql, 
                staff.Name, staff.Surname, staff.Job, staff.TelNo, staff.StaffId);

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

            string staffSql = "SELECT * FROM [staff] WHERE [staff_id] = {0}";
            var staffList = await _context.Staff.FromSqlRaw(staffSql, id).ToListAsync();
            var staff = staffList.FirstOrDefault();

            if (staff == null)
            {
                TempData["Error"] = "Staff member not found.";
                return RedirectToAction("Admin");
            }

            // Check if staff has active reservations
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

        [HttpPost, ActionName("DeleteStaff")]
        public async Task<IActionResult> DeleteStaffConfirmed(int id)
        {
            if (HttpContext.Session.GetString("admin") != "true")
            {
                return RedirectToAction("Login");
            }

            string staffSql = "SELECT * FROM [staff] WHERE [staff_id] = {0}";
            var staffList = await _context.Staff.FromSqlRaw(staffSql, id).ToListAsync();
            var staff = staffList.FirstOrDefault();

            if (staff == null)
            {
                TempData["Error"] = "Staff member not found.";
                return RedirectToAction("Admin");
            }

            // Check if staff has active reservations
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
                TempData["Error"] = $"Cannot delete staff member. They have {activeReservationIds.Count} active reservation(s).";
                return RedirectToAction("Admin");
            }

            // Delete staff member
            string deleteSql = "DELETE FROM [staff] WHERE [staff_id] = {0}";
            await _context.Database.ExecuteSqlRawAsync(deleteSql, id);

            TempData["Success"] = $"Staff member {staff.Name} {staff.Surname} has been successfully deleted.";
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

            // Check if user already exists
            string checkUserSql = "SELECT * FROM [user_credentials] WHERE [username] = {0}";
            var existingUsers = await _context.UserCredentials.FromSqlRaw(checkUserSql, username).ToListAsync();

            if (existingUsers.Any())
            {
                TempData["UserExists"] = "User already exists.";
                return RedirectToAction("CreateUser");
            }

            // Create new user
            string insertUserSql = @"
                INSERT INTO [user_credentials] ([username], [password], [user_type])
                VALUES ({0}, {1}, {2})";

            await _context.Database.ExecuteSqlRawAsync(insertUserSql, username, password, type);

            TempData["Success"] = $"User {username} has been successfully created.";
            return RedirectToAction("Admin");
        }
        
        [HttpGet]
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

            string sql = "SELECT * FROM [reservations] WHERE [reservation_id] = {0}";
            var reservations = await _context.Reservations.FromSqlRaw(sql, reservationId).ToListAsync();

            if (reservations.Any())
            {
                return RedirectToAction("Confirmation", "Reservation", new { id = reservationId });
            }

            TempData["Error"] = $"No reservation found with ID: {receiptSearch}";
            return RedirectToAction("Index");
        }
        
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
            string checkPhoneSql = "SELECT * FROM [staff] WHERE [tel_no] = {0}";
            var existingStaffList = await _context.Staff.FromSqlRaw(checkPhoneSql, staff.TelNo).ToListAsync();

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

            // Insert new staff
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
            if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time))
            {
                return Json(new { success = true, occupiedTables = new List<int>() });
            }

            var reservationDate = DateTime.Parse(date);
            
            // Get occupied tables
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

            var occupiedTables = await _context.Database.SqlQueryRaw<int>(sql, reservationDate, time).ToListAsync();

            return Json(new { success = true, occupiedTables = occupiedTables });
        }

        [HttpGet]
        public async Task<IActionResult> GetStaffMembers()
        {
            string sql = "SELECT * FROM [staff]";
            var staff = await _context.Staff.FromSqlRaw(sql).ToListAsync();
            
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

        [HttpGet]
        public async Task<IActionResult> GetAvailableTables(string date, string time, int? currentReservationId = null)
        {
            if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(time))
            {
                return Json(new { success = false, message = "Date and time are required" });
            }

            var reservationDate = DateTime.Parse(date);

            // Get all tables
            string allTablesSql = "SELECT * FROM [restaurant_tables]";
            var allTables = await _context.RestaurantTables.FromSqlRaw(allTablesSql).ToListAsync();

            // Load staff for each table and create response objects
            var allTablesWithStaff = new List<object>();
            foreach (var table in allTables)
            {
                string staffSql = "SELECT * FROM [staff] WHERE [staff_id] = {0}";
                var staffList = await _context.Staff.FromSqlRaw(staffSql, table.ServedById).ToListAsync();
                var staff = staffList.FirstOrDefault();
                
                allTablesWithStaff.Add(new
                {
                    tableId = table.TableId,
                    minCapacity = table.MinCapacity,
                    maxCapacity = table.MaxCapacity,
                    serverName = staff != null ? $"{staff.Name} {staff.Surname}" : "No Server"
                });
            }

            // Get occupied table IDs
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
                .Where(t => {
                    var tableIdProperty = t.GetType().GetProperty("tableId");
                    var tableId = (int)tableIdProperty.GetValue(t);
                    return !occupiedTableIds.Contains(tableId);
                })
                .ToList();

            return Json(new { success = true, tables = availableTables });
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