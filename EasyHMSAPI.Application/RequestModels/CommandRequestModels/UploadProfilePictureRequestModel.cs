using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UploadProfilePictureRequestModel : IRequest<UploadProfilePictureResponseModel>
    {
        public IFormFile? File { get; set; }
        public Guid UserId { get; set; }
        // Set only when an admin is uploading a photo on behalf of a doctor other than
        // themselves, from the Public Directory tile editor — triggers HospitalAccessFilter's
        // caller-is-a-member-of-this-hospital check plus an explicit doctor-belongs-to-hospital
        // check in the handler. Left null, self-service uploads behave exactly as before.
        public Guid? HospitalId { get; set; }
    }
}
