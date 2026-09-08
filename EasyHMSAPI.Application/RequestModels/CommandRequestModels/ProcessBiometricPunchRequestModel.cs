using MediatR;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class ProcessBiometricPunchRequestModel : IRequest<ProcessBiometricPunchResponseModel>
    {
        public string EmployeeCode { get; set; } = string.Empty;
        public DateTime PunchTime { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        /// <summary>
        /// IN or OUT
        /// </summary>
        public string PunchType { get; set; } = string.Empty;
    }
}
