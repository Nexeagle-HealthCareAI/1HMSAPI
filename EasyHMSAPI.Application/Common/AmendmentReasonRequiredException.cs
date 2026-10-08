namespace EasyHMSAPI.Application.Common
{
    /// <summary>Thrown when a result that has already been reported is changed without saying why.</summary>
    public class AmendmentReasonRequiredException : InvalidOperationException
    {
        public AmendmentReasonRequiredException()
            : base("This result has already been reported. Enter a reason (at least 5 characters) to amend it.") { }
    }
}
