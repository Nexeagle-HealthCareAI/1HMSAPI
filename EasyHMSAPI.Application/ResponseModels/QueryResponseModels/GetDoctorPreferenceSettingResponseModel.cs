using MediatR;
using System;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetDoctorPreferenceSettingResponseModel
    {
        public DoctorSectionPreference? Preference { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}