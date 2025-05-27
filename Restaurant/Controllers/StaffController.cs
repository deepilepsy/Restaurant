using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Restaurant.Models;
using System.Threading.Tasks;

public class StaffController : Controller
{
    private readonly RestaurantContext _context;

    public StaffController(RestaurantContext context)
    {
        _context = context;
    }

    // GET: Staff
    public async Task<IActionResult> Admin()
    {
        return View(await _context.Staff.ToListAsync());
    }

    // GET: Staff/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Staff/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Staff staff)
    {
        if (ModelState.IsValid)
        {
            _context.Add(staff);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Admin));
        }
        return View(staff);
    }

    // GET: Staff/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var staff = await _context.Staff.FindAsync(id);
        if (staff == null) return NotFound();

        return View(staff);
    }

    // POST: Staff/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Staff staff)
    {
        if (id != staff.StaffId) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(staff);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StaffExists(staff.StaffId)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Admin));
        }
        return View(staff);
    }

    // GET: Staff/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var staff = await _context.Staff
            .FirstOrDefaultAsync(m => m.StaffId == id);
        if (staff == null) return NotFound();

        return View(staff);
    }

    // POST: Staff/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var staff = await _context.Staff.FindAsync(id);
        _context.Staff.Remove(staff);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Admin));
    }

    private bool StaffExists(int id)
    {
        return _context.Staff.Any(e => e.StaffId == id);
    }
}
