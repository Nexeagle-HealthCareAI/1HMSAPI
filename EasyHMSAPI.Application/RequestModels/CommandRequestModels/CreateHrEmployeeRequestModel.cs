using MediatR;
using System;
using System.ComponentModel.DataAnnotations;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class CreateHrEmployeeRequestModel : IRequest<CreateHrEmployeeResponseModel>
    {
        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Gender { get; set; } = null!;

        [Required]
        public DateOnly DateOfBirth { get; set; }

        [Required]
        [MaxLength(20)]
        public string ContactNumber { get; set; } = null!;

        [MaxLength(150)]
        public string? Email { get; set; }

        [Required]
        [MaxLength(50)]
        public string EmploymentType { get; set; } = null!; // FULL_TIME_SALARIED, VISITING_CONSULTANT, etc.

        public Guid DepartmentId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Designation { get; set; } = null!;

        [Required]
        public DateOnly DateOfJoining { get; set; }

        [Required]
        [MaxLength(20)]
        public string PanNumber { get; set; } = null!;

        [Required]
        [MaxLength(30)]
        public string PayrollTrack { get; set; } = "TRACK_A_SALARIED"; // TRACK_A_SALARIED | TRACK_B_CONSULTANT

        [MaxLength(100)]
        public string? BankName { get; set; }

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; }

        [MaxLength(20)]
        public string? BankIfsc { get; set; }
    }
}
