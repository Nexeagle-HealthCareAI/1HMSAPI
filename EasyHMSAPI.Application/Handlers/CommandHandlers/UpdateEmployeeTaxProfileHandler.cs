using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Records which tax regime an employee has chosen and (old regime) their declared annual deductions. Applies to the employee's active
    /// salary structure, so the next payroll run uses it; TDS for the rest of the year re-spreads automatically. The employee must belong to
    /// the hospital in the request (the API checks the caller's membership of that hospital).
    /// </summary>
    public class UpdateEmployeeTaxProfileHandler : IRequestHandler<UpdateEmployeeTaxProfileRequestModel, UpdateEmployeeTaxProfileResponseModel>
    {
        // Declared deductions above this are almost certainly a typo (80C alone is capped at 1.5L; this leaves room for 80D, interest etc.).
        private const decimal MaxDeclared = 10_000_000m;
        private readonly AppDbContext _context;

        public UpdateEmployeeTaxProfileHandler(AppDbContext context) => _context = context;

        public async Task<UpdateEmployeeTaxProfileResponseModel> Handle(UpdateEmployeeTaxProfileRequestModel request, CancellationToken cancellationToken)
        {
            var regimeRaw = request.TaxRegime?.Trim();
            if (!string.Equals(regimeRaw, IncomeTaxCalculator.NewRegime, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(regimeRaw, IncomeTaxCalculator.OldRegime, StringComparison.OrdinalIgnoreCase))
                return new UpdateEmployeeTaxProfileResponseModel { Success = false, Message = "Tax regime must be NEW or OLD." };
            if (request.AnnualDeclaredDeductions < 0 || request.AnnualDeclaredDeductions > MaxDeclared)
                return new UpdateEmployeeTaxProfileResponseModel { Success = false, Message = "Declared deductions must be between 0 and 1,00,00,000." };

            var employee = await _context.HrEmployee
                .FirstOrDefaultAsync(e => e.HrEmployeeId == request.HrEmployeeId && e.HospitalId == request.HospitalId, cancellationToken);
            if (employee == null)
                return new UpdateEmployeeTaxProfileResponseModel { Success = false, Message = "Employee not found." };

            var structure = await _context.HrSalaryStructure
                .Where(s => s.HrEmployeeId == employee.HrEmployeeId && s.IsActive)
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);
            if (structure == null)
                return new UpdateEmployeeTaxProfileResponseModel { Success = false, Message = "This employee has no active salary structure." };

            var regime = IncomeTaxCalculator.NormaliseRegime(regimeRaw);
            structure.TaxRegime = regime;
            // Declared deductions only mean something under the old regime; store 0 under the new one so stale figures are not carried.
            structure.AnnualDeclaredDeductions = regime == IncomeTaxCalculator.OldRegime ? Math.Round(request.AnnualDeclaredDeductions, 2) : 0m;
            structure.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return new UpdateEmployeeTaxProfileResponseModel
            {
                Success = true,
                Message = "Tax profile updated. It applies from the next payroll run.",
                TaxRegime = structure.TaxRegime,
                AnnualDeclaredDeductions = structure.AnnualDeclaredDeductions,
            };
        }
    }
}
