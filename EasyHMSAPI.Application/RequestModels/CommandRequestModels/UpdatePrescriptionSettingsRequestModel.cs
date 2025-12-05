using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdatePrescriptionSettingsRequestModel : IRequest<UpdatePrescriptionSettingsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public int? HeaderHeight { get; set; }
        public int? FooterHeight { get; set; }
        public int? ContentLeftMargin { get; set; }
        public int? ContentRightMargin { get; set; }
        public bool? OverFlowPage { get; set; }
        public string? FontFamily { get; set; }
        public int? FontSize { get; set; }
        public string? FontWeight { get; set; }
        public string? TextColour { get; set; }
        public Guid LoggedInUserId { get; set; }
    }
}
