using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdatePrescriptionSettingsRequestModel : IRequest<UpdatePrescriptionSettingsResponseModel>
    {
        public Guid DoctorId { get; set; }
        public PrescriptionSettingsDataModel Settings { get; set; } = null!;
    }

    [ExcludeFromCodeCoverage]
    public class PrescriptionSettingsDataModel
    {
        public PageLayoutDataModel? PageLayout { get; set; }
        public bool UseLetterhead { get; set; }
        public LetterheadSettingsDataModel? LetterheadSettings { get; set; }
        public bool UseHeaderSettings { get; set; }
        public HeaderSettingsDataModel? HeaderSettings { get; set; }
        public bool UseFooterSettings { get; set; }
        public FooterSettingsDataModel? FooterSettings { get; set; }
        public bool UseDoctorSetting { get; set; }
        public DoctorSettingDataModel? DoctorSetting { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PageLayoutDataModel
    {
        public string? Orientation { get; set; }
        public MarginDataModel? Margin { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MarginDataModel
    {
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public int Left { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class LetterheadSettingsDataModel
    {
        public int HeaderHeight { get; set; }
        public int FooterHeight { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class HeaderSettingsDataModel
    {
        public int Height { get; set; }
        public int Width { get; set; }
        public bool ShowImage { get; set; }
        public bool ShowOnAllPages { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class FooterSettingsDataModel
    {
        public int Height { get; set; }
        public int Width { get; set; }
        public bool ShowImage { get; set; }
        public bool ShowOnAllPages { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DoctorSettingDataModel
    {
        public bool ShowSignature { get; set; }
        public int SignatureHeight { get; set; }
        public int SignatureWidth { get; set; }
        public string? DoctorName { get; set; }
    }
}
