using QuickMynth1.Data;
using QuickMynth1.Models;
using QuickMynth1.Models.ViewModels;
using QuickMynth1.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace QuickMynth1.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        //UserManager is used as the database class for AspNetUser (CRUD Operations)
        UserManager<ApplicationUser> _userManager;
        //SignInManager contains the build in functions for login, logout, registartion, etc. 
        SignInManager<ApplicationUser> _signInManager;
        //RoleManager is used as the database class for AspNetRoles (CRUD Operations)
        RoleManager<IdentityRole> _roleManager;

        public AccountController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager)
        {
            _db = db;  
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task <IActionResult> Login(LoginViewModel model)
        {
            if(ModelState.IsValid)
            {
                // _signInManager.PasswordSignInAsync matches credentials with database values
                var resut = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);
                if(resut.Succeeded)
                {
                    return RedirectToAction("Index", "QuickMynth");
                }
                ModelState.AddModelError("", "Login Failed");
            }
            return View(model);
        }
        public async Task<IActionResult> Register()
        {
            // Ensure roles exist (only Admin and Employee)
            if (!_roleManager.RoleExistsAsync(Helper.Admin).GetAwaiter().GetResult())
            {
                await _roleManager.CreateAsync(new IdentityRole(Helper.Admin));
            }

            if (!_roleManager.RoleExistsAsync(Helper.Employee).GetAwaiter().GetResult())
            {
                await _roleManager.CreateAsync(new IdentityRole(Helper.Employee));
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Name = model.Name,
                Phone = model.Phone,
                SSN = model.SSN,
                HomeAddress = model.HomeAddress,
                OfficeAddress = model.OfficeAddress,
                EmployerName = model.EmployerName,
                CompanySize = model.CompanySize,
                ManagerEmail = model.ManagerEmail
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, Helper.Employee);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        public async Task<IActionResult> LogOff()
        {
            // _signInManager.SignOutAsync is used to logout the user from the database
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}