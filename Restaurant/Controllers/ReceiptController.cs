using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Models;
using System.Linq;
using System.Threading.Tasks;
using System;

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
        var receipts = await _context.Receipts
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

        var receipt = await _context.Receipts.FindAsync(id);
        if (receipt == null) return NotFound();

        // For dropdowns you might want to pass staff and tables here
        ViewData["StaffList"] = await _context.Staff.ToListAsync();
        ViewData["TableList"] = await _context.Tables.ToListAsync();

        return View(receipt);
    }

    // POST: Receipt/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Receipt receipt)
    {
        if (id != receipt.ReceiptId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(receipt);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Receipts.Any(e => e.ReceiptId == id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Admin));
        }

        ViewData["StaffList"] = await _context.Staff.ToListAsync();
        ViewData["TableList"] = await _context.Tables.ToListAsync();
        return View(receipt);
    }
}
