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
       


        public IActionResult Index()
        {
            return View();
        }


    }

}
