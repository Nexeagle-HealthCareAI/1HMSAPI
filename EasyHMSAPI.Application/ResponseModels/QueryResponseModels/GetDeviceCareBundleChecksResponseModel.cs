using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDeviceCareBundleChecksResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        // Canonical item keys/labels for this device's type (IpdConstants.CareBundleItems),
        // so the frontend checklist form doesn't need to duplicate the list.
        public List<CareBundleItemDefItem> CanonicalItems { get; set; } = new();
        public List<DeviceCareBundleCheckItem> Checks { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class CareBundleItemDefItem
    {
        public string Key { get; set; } = null!;
        public string Label { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class DeviceCareBundleCheckItem
    {
        public Guid CheckId { get; set; }
        public List<CareBundleItemResultItem> Items { get; set; } = new();
        public int CompliantCount { get; set; }
        public int TotalItems { get; set; }
        public bool AllCompliant { get; set; }
        public string? Notes { get; set; }
        public string CheckedBy { get; set; } = null!;
        public DateTime CheckedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CareBundleItemResultItem
    {
        public string Key { get; set; } = null!;
        public bool Compliant { get; set; }
    }
}
