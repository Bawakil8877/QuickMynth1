using QuickMynth1.Data;
using QuickMynth1.Models;
using QuickMynth1.Models.ViewModels;
using QuickMynth1.Utility;
using QuickMynth1.Services;

namespace QuickMynth1.Services
{
    public class QuickMynthervice : IQuickMynthervice
    {
        private readonly ApplicationDbContext _db;
        private readonly EmailService _emailService;
        public QuickMynthervice(ApplicationDbContext db, EmailService emailService)
        {
            _db = db;
            _emailService = emailService;
        }

        public async Task<int> AddUpdate(QuickMynthVM model)
        {
            var startDate = DateTime.Parse(model.StartDate);
            var endDate = DateTime.Parse(model.StartDate).AddMinutes(Convert.ToDouble(model.Duration));
            var patient = _db.Users.FirstOrDefault(u => u.Id == model.PatientId);
            var doctor = _db.Users.FirstOrDefault(u => u.Id == model.DoctorId);
            if (model != null && model.Id > 0)
            {
                //Id greater than 0 means that the record has already been created i.e., this is for update
                //update
                var QuickMynth = _db.QuickMynth.FirstOrDefault(x => x.Id == model.Id);
                if (QuickMynth != null)
                {
                    QuickMynth.Title = model.Title;
                    QuickMynth.Description = model.Description;
                    QuickMynth.StartDate = startDate;
                    QuickMynth.EndDate = endDate;
                    QuickMynth.Duration = model.Duration;
                    QuickMynth.DoctorId = model.DoctorId;
                    QuickMynth.PatientId = model.PatientId;
                    QuickMynth.IsDoctorApproved = false;
                    QuickMynth.AdminId = model.AdminId;
                    await _db.SaveChangesAsync();
                }
                return 1;
            }
            else
            {
                //This is for creating a new data record
                QuickMynth QuickMynth = new QuickMynth()
                {
                    Title = model.Title,
                    Description = model.Description,
                    StartDate = startDate,
                    EndDate = endDate,
                    Duration = model.Duration,
                    DoctorId = model.DoctorId,
                    PatientId = model.PatientId,
                    AdminId = model.AdminId,
                    IsDoctorApproved = model.IsDoctorApproved
                };
                _db.QuickMynth.Add(QuickMynth);
                await _db.SaveChangesAsync();
                
                EmailModel emailModelPatient = new EmailModel();
                emailModelPatient.From = "abbasiharis1997@gmail.com";
                emailModelPatient.To = patient.Email;
                emailModelPatient.Subject = "QuickMynth Created";
                emailModelPatient.Body = $"Your QuickMynth with {doctor.Name} is created and in pending status";
                //await _emailService.SendEmailAsync(emailModelPatient);

                EmailModel emailModelDoctor = new EmailModel();
                emailModelDoctor.From = "abbasiharis1997@gmail.com";
                emailModelDoctor.To = doctor.Email;
                emailModelDoctor.Subject = "QuickMynth Created";
                emailModelDoctor.Body = $"Your QuickMynth with {patient.Name} is created and in pending status";
                //await _emailService.SendEmailAsync(emailModelDoctor);

                return 2;
            }
        }

        public async Task<int> ConfirmEvent(int id)
        {
            var QuickMynth = _db.QuickMynth.FirstOrDefault(x => x.Id == id);
            if (QuickMynth != null)
            {
                QuickMynth.IsDoctorApproved = true;
                return await _db.SaveChangesAsync();
            }
            return 0;
        }

        public async Task<int> Delete(int id)
        {
            var QuickMynth = _db.QuickMynth.FirstOrDefault(x =>x.Id == id);
            if (QuickMynth != null)
            {
                _db.QuickMynth.Remove(QuickMynth);
                return await _db.SaveChangesAsync();
            }
            return 0;
        }

        public List<QuickMynthVM> DoctorsEventsById(string doctorId)
        {
            return _db.QuickMynth.Where(x => x.DoctorId == doctorId).ToList().Select(c => new QuickMynthVM()
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                IsDoctorApproved= c.IsDoctorApproved,
                Duration = c.Duration,
                StartDate = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                EndDate = c.EndDate.HasValue ? c.EndDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
            }).ToList();
        }

        public QuickMynthVM? GetById(int id)
        {
            return _db.QuickMynth.Where(x => x.Id == id).ToList().Select(c => new QuickMynthVM()
            {
                Id = c.Id,
                Description = c.Description,
                StartDate = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                EndDate = c.EndDate.HasValue ? c.EndDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                Title = c.Title,
                Duration = c.Duration,
                IsDoctorApproved = c.IsDoctorApproved,
                PatientId = c.PatientId,
                DoctorId = c.DoctorId,
                PatientName = _db.Users.Where(x => x.Id == c.PatientId).Select(x => x.Name).FirstOrDefault(),
                DoctorName = _db.Users.Where(x => x.Id == c.DoctorId).Select(x => x.Name).FirstOrDefault(),
            }).SingleOrDefault();
        }

        public List<DoctorVM> GetDoctorList()
        {
            var doctors = (from users in _db.Users
                           join userRoles in _db.UserRoles on users.Id equals userRoles.UserId
                           join roles in _db.Roles.Where(x=>x.Name == Helper.Employer) on userRoles.RoleId equals roles.Id
                           select new DoctorVM
                           {
                               Id = users.Id,
                               Name = users.Name,
                           }).ToList();

            return doctors;
        }

        public List<EmployeeVM> GetPatientList()
        {
            var patients = (from users in _db.Users
                           join userRoles in _db.UserRoles on users.Id equals userRoles.UserId
                           join roles in _db.Roles.Where(x => x.Name == Helper.Employee) on userRoles.RoleId equals roles.Id
                           select new EmployeeVM
                           {
                               Id = users.Id,
                               Name = users.Name,
                           }).ToList();

            return patients;
        }

        public List<QuickMynthVM> PatientsEventsById(string patientId)
        {
            return _db.QuickMynth.Where(x => x.PatientId == patientId).ToList().Select(c => new QuickMynthVM()
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                IsDoctorApproved = c.IsDoctorApproved,
                Duration = c.Duration,
                StartDate = c.StartDate.HasValue ? c.StartDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                EndDate = c.EndDate.HasValue ? c.EndDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
            }).ToList();
        }


    }
}
