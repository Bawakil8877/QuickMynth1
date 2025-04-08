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
            // _roleManager.RoleExistsAsync checks whether the value (Helper.Admin) exists in database or not
            if (!_roleManager.RoleExistsAsync(Helper.Admin).GetAwaiter().GetResult())
            {
                await _roleManager.CreateAsync(new IdentityRole(Helper.Admin));
                await _roleManager.CreateAsync(new IdentityRole(Helper.Employee));
                await _roleManager.CreateAsync(new IdentityRole(Helper.Employer));
            }
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check the role selected
                if (model.RoleName == "Employee")
                {
                    // Validate employee-specific fields
                    if (string.IsNullOrEmpty(model.Phone) || string.IsNullOrEmpty(model.SSN))
                    {
                        ModelState.AddModelError("", "Employee fields must be filled.");
                        return View(model);
                    }
                }
                else if (model.RoleName == "Employer")
                {
                    // Validate employer-specific fields
                    if (string.IsNullOrEmpty(model.EmployerName) || string.IsNullOrEmpty(model.CompanySize))
                    {
                        ModelState.AddModelError("", "Employer fields must be filled.");
                        return View(model);
                    }
                }

                // Proceed with user registration
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email, Name = model.Name };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Assign the correct role
                    await _userManager.AddToRoleAsync(user, model.RoleName);

                    return RedirectToAction("Index", "Home"); // Or wherever you want to redirect after successful registration
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
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