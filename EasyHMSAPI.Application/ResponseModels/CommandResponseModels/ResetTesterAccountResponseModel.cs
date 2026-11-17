using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ResetTesterAccountResponseModel
    {
        public bool Success { get; set; }
        /// <summary>HTTP status the controller answers with (200, 400, 404, 409, 500).</summary>
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public bool DryRun { get; set; }
        /// <summary>False when the number had no account: there was nothing to reset.</summary>
        public bool AccountFound { get; set; }
        /// <summary>Names of the hospitals this tester registered (deleted with everything under them).</summary>
        public List<string> Hospitals { get; set; } = new();
        /// <summary>How many accounts go: the tester's own plus staff who belonged only to those hospitals.</summary>
        public int Users { get; set; }
        /// <summary>Rows deleted (or, on a dry run, that would be deleted) per table.</summary>
        public List<TesterResetTableCount> Tables { get; set; } = new();
        public int TotalRows { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TesterResetTableCount
    {
        public string Table { get; set; } = string.Empty;
        public int Rows { get; set; }
    }
}
