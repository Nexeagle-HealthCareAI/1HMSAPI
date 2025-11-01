using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class DoctorBookedSlotsResponseModel
    {
        public Guid DoctorId { get; set; }
        public DateTime Date { get; set; }
        public List<TimeSpan> BookedSlots { get; set; } = new List<TimeSpan>();
    }
}
