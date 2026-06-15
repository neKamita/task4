using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Task4.Data;
using Task4.Models;

namespace Task4.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly AppDb _db;

    public UsersController(AppDb db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var currentId = GetCurrentUserId();

        return View(new UsersPageViewModel
        {
            Users = await _db.GetUsersAsync(),
            CurrentUserId = currentId,
            HasUnverifiedUsers = await _db.HasUnverifiedUsersAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(int[] selectedIds)
    {
        if (selectedIds.Length == 0)
        {
            TempData["Warning"] = "Select at least one user.";
            return RedirectToAction("Index");
        }

        var currentId = GetCurrentUserId();
        var count = await _db.BlockAsync(selectedIds);

        if (selectedIds.Contains(currentId))
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login", "Account", new { message = "blocked" });
        }

        TempData["Success"] = $"Blocked users: {count}.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock(int[] selectedIds)
    {
        if (selectedIds.Length == 0)
        {
            TempData["Warning"] = "Select at least one user.";
            return RedirectToAction("Index");
        }

        TempData["Success"] = $"Unblocked users: {await _db.UnblockAsync(selectedIds)}.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int[] selectedIds)
    {
        if (selectedIds.Length == 0)
        {
            TempData["Warning"] = "Select at least one user.";
            return RedirectToAction("Index");
        }

        var currentId = GetCurrentUserId();
        var count = await _db.DeleteAsync(selectedIds);

        if (selectedIds.Contains(currentId))
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login", "Account", new { message = "deleted" });
        }

        TempData["Success"] = $"Deleted users: {count}.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUnverified()
    {
        var currentId = GetCurrentUserId();
        var selfDeleted = await _db.IsUnverifiedAsync(currentId);
        var count = await _db.DeleteUnverifiedAsync();

        if (selfDeleted)
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login", "Account", new { message = "deleted" });
        }

        TempData["Success"] = $"Deleted unverified users: {count}.";
        return RedirectToAction("Index");
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    }
}
