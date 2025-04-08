using QuickMynth1.Services;
using QuickMynth1.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuickMynth1.Controllers
{
    [Authorize]
    public class QuickMynthController : Controller
    {
        private readonly IQuickMynthervice _QuickMynthervice;
        public QuickMynthController(IQuickMynthervice QuickMynthervice)
        {
            _QuickMynthervice = QuickMynthervice;
        }
        //[Authorize]
        //[Authorize(Roles =Helper.Admin)] //the role (e.g.Admin) must be of type const
        public IActionResult Index()
        {
            ViewBag.DoctorList = _QuickMynthervice.GetDoctorList();
            ViewBag.PatientList = _QuickMynthervice.GetPatientList();
            ViewBag.Duration = Helper.GetTimeDropDown();
            // Session variable (a global variable accessable throughout the app)
            HttpContext.Session.SetString("UserName", "Haris");
            return View();
        }
    }
}
