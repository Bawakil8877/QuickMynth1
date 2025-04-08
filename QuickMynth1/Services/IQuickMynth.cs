using QuickMynth1.Models;
using QuickMynth1.Models.ViewModels;

namespace QuickMynth1.Services
{
    public interface IQuickMynthervice
    {
        public List<DoctorVM> GetDoctorList();
        public List<EmployeeVM> GetPatientList();
        public Task<int> AddUpdate(QuickMynthVM model);
        public List<QuickMynthVM> DoctorsEventsById(string doctorId);
        public List<QuickMynthVM> PatientsEventsById(string patientId);
        public QuickMynthVM? GetById(int id);
        public Task<int> Delete(int id);
        public Task<int> ConfirmEvent(int id);
    }
}
