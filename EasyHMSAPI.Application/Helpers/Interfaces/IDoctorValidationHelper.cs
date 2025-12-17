namespace EasyHMSAPI.Application.Helpers.Interfaces
{
    public interface IDoctorValidationHelper
    {
        public Task<bool> ValidateDoctorAsync(Guid hospitalId, Guid doctorId, CancellationToken cancellationToken);
    }
}
