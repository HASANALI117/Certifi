using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPlatform.API.Data;

namespace TrainingPlatform.MVC.Controllers;

[Authorize]
[Route("api/notifications")]
public class NotificationsController : Controller
{
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context) => _context = context;

    [HttpGet("recent")]
    public async Task<IActionResult> Recent(int take = 10)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var items = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(take, 1, 50))
            .Select(n => new
            {
                n.Id,
                n.Message,
                n.Type,
                n.CreatedAt,
                n.IsRead
            })
            .ToListAsync();

        var unread = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        return Json(new { items, unread });
    }

    [HttpPost("mark-read")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int? id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var query = _context.Notifications.Where(n => n.UserId == userId && !n.IsRead);
        if (id.HasValue) query = query.Where(n => n.Id == id.Value);

        var notifications = await query.ToListAsync();
        foreach (var n in notifications) n.IsRead = true;
        await _context.SaveChangesAsync();

        var unread = await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Json(new { unread });
    }
}
