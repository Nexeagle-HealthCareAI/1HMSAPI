using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class ProcessBiometricPunchRequestModel : IRequest<ProcessBiometricPunchResponseModel>
    {
        /// <summary>Employee codes (EMP-YYYY-NNNN) repeat across hospitals, so a punch is only meaningful
        /// together with the hospital it belongs to.</summary>
        public Guid HospitalId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public DateTime PunchTime { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        /// <summary>
        /// IN or OUT
        /// </summary>
        public string PunchType { get; set; } = string.Empty;
    }
}
