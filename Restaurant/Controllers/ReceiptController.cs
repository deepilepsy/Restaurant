using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Models;
using System.Linq;
using System.Threading.Tasks;
using System;
using Restaurant;

public class ReceiptController : Controller
{
    private readonly RestaurantContext _context;

    public ReceiptController(RestaurantContext context)
    {
        _context = context;
    }

    // GET: Receipt (show only future or today)
    public async Task<IActionResult> Admin()
    {
        var today = DateTime.Today;
        var receipts = await _context.Reservations
            .Include(r => r.ServedBy)
            .Include(r => r.Table)
            .Where(r => r.ReservationDate >= today)
            .ToListAsync();

        return View(receipts);
    }

    // GET: Receipt/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var receipt = await _context.Reservations.FindAsync(id);
        if (receipt == null) return NotFound();

        // For dropdowns you might want to pass staff and tables here
        ViewData["StaffList"] = await _context.Staff.ToListAsync();
        ViewData["TableList"] = await _context.RestaurantTables.ToListAsync();

        return View(receipt);
    }

    // POST: Receipt/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Reservation reservation)
    {
        if (id != reservation.ReservationId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(reservation);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Reservations.Any(e => e.ReservationId == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Admin));
        }

        ViewData["StaffList"] = await _context.Staff.ToListAsync();
        ViewData["TableList"] = await _context.RestaurantTables.ToListAsync();
        return View(reservation);
    }
}
