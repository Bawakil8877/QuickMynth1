using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuickMynth1.Utility
{
    public static class Helper
    {
        public const string Admin = "Admin";
        public static string Employee = "Employee";
        public static string Employer = "Employer";
        public static string QuickMynthAdded = "QuickMynth added successfully.";
        public static string QuickMynthUpdated = "QuickMynth updated successfully.";
        public static string QuickMynthDeleted = "QuickMynth deleted successfully.";
        public static string QuickMynthExists = "QuickMynth for selected date and time already exists.";
        public static string QuickMynthNotExists = "QuickMynth not exists.";
        public static string meetingConfirm = "Meeting confirm successfully.";
        public static string meetingConfirmError = "Error while confirming meeting.";
        public static string QuickMynthAddError = "Something went wront, Please try again.";
        public static string QuickMynthUpdatError = "Something went wront, Please try again.";
        public static string somethingWentWrong = "Something went wront, Please try again.";
        public static int success_code = 1;
        public static int failure_code = 0;

        public static List<SelectListItem> GetRolesForDropDown(bool isAdmin)
        {
            if(isAdmin)
            {
                return new List<SelectListItem>
                {
                    new SelectListItem{Value=Helper.Admin,Text=Helper.Admin}
                };
            }
            else
            {
                return new List<SelectListItem>
                {
                    new SelectListItem{Value=Helper.Employee,Text=Helper.Employee},
                    new SelectListItem{Value=Helper.Employer,Text=Helper.Employer}
                };
            }   
        }

        public static List<SelectListItem> GetTimeDropDown()
        {
            int minute = 60;
            List<SelectListItem> duration = new List<SelectListItem>();
            for (int i = 1; i <= 12; i++)
            {
                duration.Add(new SelectListItem { Value = minute.ToString(), Text = i + " Hr" });
                minute = minute + 30;
                duration.Add(new SelectListItem { Value = minute.ToString(), Text = i + " Hr 30 min" });
                minute = minute + 30;
            }
            return duration;
        }

    }
}
