using QuickMynth1.Services;
using QuickMynth1.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickMynth1.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using QuickMynth1.Data;
using QuickMynth1.Models;
namespace QuickMynth1.Controllers
{
    [Authorize]
   

    public class QuickMynthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public QuickMynthController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new RegisterViewModel
            {
                Email = user.Email,
                ManagerEmail = user.ManagerEmail // assuming this is a custom property in your ApplicationUser
            };

            return View(model);
        }
    }

}
