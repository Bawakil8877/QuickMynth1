using QuickMynth1.Models.ViewModels;
using QuickMynth1.Services;
using QuickMynth1.Utility;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace QuickMynth1.Controllers.Api
{
    [Route("api/QuickMynth")]
    [ApiController]
    public class QuickMynthApiController : Controller
    {
        private readonly IQuickMynthervice _QuickMynthervice;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string? loginUserId;
        private readonly string? role;

        public QuickMynthApiController(IQuickMynthervice QuickMynthervice, IHttpContextAccessor httpContextAccessor)
        {
            _QuickMynthervice = QuickMynthervice;
            _httpContextAccessor = httpContextAccessor;
            loginUserId = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            role = _httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Role);
        }

        [HttpPost]
        [Route("SaveCalendarData")]
        public IActionResult SaveCalendarData(QuickMynthVM data)
        {
            CommonResponse<int> commonResponse = new CommonResponse<int>();
            try
            {
                commonResponse.status = _QuickMynthervice.AddUpdate(data).Result;
                if(commonResponse.status == 1)
                {
                    commonResponse.message = Helper.QuickMynthUpdated;
                }
                if (commonResponse.status == 2)
                {
                    commonResponse.message = Helper.QuickMynthAdded;
                }
            }
            catch(Exception e)
            {
                commonResponse.message = e.Message;
                commonResponse.status = Helper.failure_code;
            } 

            return Ok(commonResponse);
        }

        [HttpGet]
        [Route("GetCalendarData")]
        public IActionResult GetCalendarData(string doctorId)
        {
            CommonResponse<List<QuickMynthVM>> commonResponse = new CommonResponse<List<QuickMynthVM>>();
            try
            {
                if (role == Helper.Employee)
                {
                    commonResponse.dataenum = _QuickMynthervice.PatientsEventsById(loginUserId);
                    commonResponse.status = Helper.success_code;
                }
                else if (role == Helper.Employer)
                {
                    commonResponse.dataenum = _QuickMynthervice.DoctorsEventsById(loginUserId);
                    commonResponse.status = Helper.success_code;
                }
                else
                {
                    commonResponse.dataenum = _QuickMynthervice.DoctorsEventsById(doctorId);
                    commonResponse.status = Helper.success_code;
                }
            }
            catch(Exception e)
            {
                commonResponse.message = e.Message;
                commonResponse.status = Helper.failure_code;
            }
            
            return Ok(commonResponse);
        }

        [HttpGet]
        [Route("GetCalendarDataById/{id}")]
        public IActionResult GetCalendarDataById(int id)
        {
            CommonResponse<QuickMynthVM> commonResponse = new CommonResponse<QuickMynthVM>();
            try
            {
                    commonResponse.dataenum = _QuickMynthervice.GetById(id);
                    commonResponse.status = Helper.success_code;
            }
            catch (Exception e)
            {
                commonResponse.message = e.Message;
                commonResponse.status = Helper.failure_code;
            }

            return Ok(commonResponse);
        }

        [HttpGet]
        [Route("DeleteAppoinment/{id}")]
        public async Task<IActionResult> DeleteAppoinment(int id)
        {
            CommonResponse<int> commonResponse = new CommonResponse<int>();
            try
            {
                commonResponse.status = await _QuickMynthervice.Delete(id);
                commonResponse.message = commonResponse.status == 1 ? Helper.QuickMynthDeleted : Helper.somethingWentWrong;

            }
            catch (Exception e)
            {
                commonResponse.message = e.Message;
                commonResponse.status = Helper.failure_code;
            }
            return Ok(commonResponse);
        }

        [HttpGet]
        [Route("ConfirmEvent/{id}")]
        public IActionResult ConfirmEvent(int id)
        {
            CommonResponse<int> commonResponse = new CommonResponse<int>();
            try
            {
                var result = _QuickMynthervice.ConfirmEvent(id).Result;
                if (result > 0)
                {
                    commonResponse.status = Helper.success_code;
                    commonResponse.message = Helper.meetingConfirm;
                }
                else
                {

                    commonResponse.status = Helper.failure_code;
                    commonResponse.message = Helper.meetingConfirmError;
                }

            }
            catch (Exception e)
            {
                commonResponse.message = e.Message;
                commonResponse.status = Helper.failure_code;
            }
            return Ok(commonResponse);
        }
    }
}
