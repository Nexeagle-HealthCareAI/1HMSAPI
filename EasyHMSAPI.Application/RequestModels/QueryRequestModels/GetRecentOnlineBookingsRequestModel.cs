using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetRecentOnlineBookingsRequestModel : IRequest<GetRecentOnlineBookingsResponseModel>
    {
        [Required]
        public Guid HospitalId { get; set; }

        /// <summary>
        /// Cursor from the previous response's ServerTime. Omitted on the first call, which only
        /// establishes a baseline (no items) so a freshly opened board doesn't replay old bookings.
        /// </summary>
        public DateTime? Since { get; set; }
    }
}
