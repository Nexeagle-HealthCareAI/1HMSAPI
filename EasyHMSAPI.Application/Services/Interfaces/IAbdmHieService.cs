namespace EasyHMSAPI.Application.Services.Interfaces
{
    public enum AbdmFacilityHeader
    {
        Hip,
        Hiu,
        None
    }

    /// <summary>One outbound call to ABDM's HIE-CM gateway (M2/M3).</summary>
    public sealed class AbdmHieCall
    {
        /// <summary>What the call is for, e.g. "link-token"; shown on the integrator panel.</summary>
        public string Kind { get; init; } = string.Empty;
        /// <summary>Path under the HIE-CM host, e.g. "/api/hiecm/v3/token/generate-token".</summary>
        public string Path { get; init; } = string.Empty;
        public object Body { get; init; } = new { };
        public Guid HospitalId { get; init; }
        /// <summary>What the call is about (ABHA address, care context reference), for the panel.</summary>
        public string? SubjectRef { get; init; }
        /// <summary>True when ABDM answers on a callback; a 202 then means "waiting", not "done".</summary>
        public bool ExpectsCallback { get; init; }
        public AbdmFacilityHeader FacilityHeader { get; init; } = AbdmFacilityHeader.Hip;
        public string? LinkToken { get; init; }
    }

    public sealed class AbdmHieResult
    {
        public Guid ExchangeId { get; init; }
        public string RequestId { get; init; } = string.Empty;
        public int HttpStatus { get; init; }
        public string Body { get; init; } = string.Empty;
    }

    public interface IAbdmHieService
    {
        /// <summary>Sends the call and records it as an exchange. Throws AbdmException when refused — including locally, before anything is sent, when the hospital has no HIP ID.</summary>
        Task<AbdmHieResult> SendAsync(AbdmHieCall call, CancellationToken cancellationToken);
    }
}
