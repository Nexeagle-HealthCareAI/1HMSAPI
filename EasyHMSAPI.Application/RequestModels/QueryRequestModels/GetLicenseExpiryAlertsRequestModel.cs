using MediatR;
using System;
using System.Collections.Generic;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetLicenseExpiryAlertsRequestModel : IRequest<GetLicenseExpiryAlertsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
