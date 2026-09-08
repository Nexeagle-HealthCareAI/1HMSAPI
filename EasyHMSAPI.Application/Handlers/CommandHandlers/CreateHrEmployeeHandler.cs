using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CreateHrEmployeeHandler : IRequestHandler<CreateHrEmployeeRequestModel, CreateHrEmployeeResponseModel>
    {
        private readonly AppDbContext _context;

        public CreateHrEmployeeHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateHrEmployeeResponseModel> Handle(CreateHrEmployeeRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // Verify hospital exists
                var hospitalExists = await _context.Hospitals.AnyAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
                if (!hospitalExists)
                {
                    return new CreateHrEmployeeResponseModel
                    {
                        Success = false,
                        Message = "Hospital not found",
                        Errors = new List<string> { "Invalid Hospital ID" }
                    };
                }

                // Verify user exists (the user performing the creation)
                var userExists = await _context.Users.AnyAsync(u => u.UserID == request.UserId, cancellationToken);
                if (!userExists)
                {
                    return new CreateHrEmployeeResponseModel
                    {
                        Success = false,
                        Message = "User not found",
                        Errors = new List<string> { "Invalid User ID" }
                    };
                }

                // Generate Employee Code (e.g., EMP-2026-0042)
                var currentYear = DateTime.UtcNow.Year;
                var latestEmployee = await _context.HrEmployee
                    .Where(e => e.HospitalId == request.HospitalId && e.EmployeeCode.StartsWith($"EMP-{currentYear}-"))
                    .OrderByDescending(e => e.EmployeeCode)
                    .FirstOrDefaultAsync(cancellationToken);

                int nextSeq = 1;
                if (latestEmployee != null)
                {
                    var parts = latestEmployee.EmployeeCode.Split('-');
                    if (parts.Length == 3 && int.TryParse(parts[2], out int lastSeq))
                    {
                        nextSeq = lastSeq + 1;
                    }
                }

                var employeeCode = $"EMP-{currentYear}-{nextSeq:D4}";

                var newEmployee = new HrEmployee
                {
                    HrEmployeeId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    EmployeeCode = employeeCode,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Gender = request.Gender,
                    DateOfBirth = request.DateOfBirth,
                    ContactNumber = request.ContactNumber,
                    Email = request.Email,
                    EmploymentType = request.EmploymentType,
                    DepartmentId = request.DepartmentId,
                    Designation = request.Designation,
                    DateOfJoining = request.DateOfJoining,
                    PanNumber = request.PanNumber,
                    PayrollTrack = request.PayrollTrack,
                    BankName = request.BankName,
                    BankAccountNumber = request.BankAccountNumber,
                    BankIfsc = request.BankIfsc,
                    IsActive = true,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.UserId.ToString(),
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.UserId.ToString()
                };

                _context.HrEmployee.Add(newEmployee);
                await _context.SaveChangesAsync(cancellationToken);

                return new CreateHrEmployeeResponseModel
                {
                    Success = true,
                    Message = "HR Employee created successfully",
                    HrEmployeeId = newEmployee.HrEmployeeId,
                    EmployeeCode = newEmployee.EmployeeCode
                };
            }
            catch (Exception ex)
            {
                return new CreateHrEmployeeResponseModel
                {
                    Success = false,
                    Message = "Error occurred while creating HR Employee",
                    Errors = new List<string> { ex.Message }
                };
            }
        }
    }
}
