using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // Backs the anonymous "deliver e-prescription" link for the structured EPrescriptionPad
    // flow -- distinct from public-prescription/{attachmentId} (InkRx/manual uploads, keyed by
    // PrescriptionAttachment.AttachmentId). This flow never creates an attachment row at all; the
    // rendered PDF lives on Appointment.PdfUrl, so AppointmentId (already an accepted anonymous
    // lookup key elsewhere in this controller family) is the natural identifier here too.
    [ExcludeFromCodeCoverage]
    public class GetPublicVisitSummaryRequestModel : IRequest<GetPublicVisitSummaryResponseModel>
    {
        public Guid AppointmentId { get; set; }
    }
}
