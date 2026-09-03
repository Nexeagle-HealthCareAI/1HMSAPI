using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SaltCompositionCommandHandlers :
        IRequestHandler<CreateMoleculeRequestModel, CreateMoleculeResponseModel>,
        IRequestHandler<CreateSaltCompositionRequestModel, CreateSaltCompositionResponseModel>
    {
        private readonly AppDbContext _context;

        public SaltCompositionCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateMoleculeResponseModel> Handle(CreateMoleculeRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return new CreateMoleculeResponseModel { Success = false, Message = "Name is required." };

                var name = request.Name.Trim();
                var existing = await _context.Molecule.FirstOrDefaultAsync(m => m.Name.ToUpper() == name.ToUpper(), cancellationToken);
                if (existing != null)
                    return new CreateMoleculeResponseModel { Success = true, Message = "Molecule already exists.", MoleculeId = existing.MoleculeId };

                var molecule = new Molecule { MoleculeId = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
                _context.Molecule.Add(molecule);
                await _context.SaveChangesAsync(cancellationToken);
                return new CreateMoleculeResponseModel { Success = true, Message = "Molecule created.", MoleculeId = molecule.MoleculeId };
            }
            catch (Exception)
            {
                return new CreateMoleculeResponseModel { Success = false, Message = "Error creating molecule." };
            }
        }

        public async Task<CreateSaltCompositionResponseModel> Handle(CreateSaltCompositionRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.DisplayName))
                    return new CreateSaltCompositionResponseModel { Success = false, Message = "DisplayName is required." };
                if (request.Components == null || request.Components.Count == 0)
                    return new CreateSaltCompositionResponseModel { Success = false, Message = "At least one molecule component is required." };

                var moleculeIds = request.Components.Select(c => c.MoleculeId).Distinct().ToList();
                var validMoleculeCount = await _context.Molecule.CountAsync(m => moleculeIds.Contains(m.MoleculeId), cancellationToken);
                if (validMoleculeCount != moleculeIds.Count)
                    return new CreateSaltCompositionResponseModel { Success = false, Message = "One or more molecules were not found." };

                var composition = new SaltComposition
                {
                    SaltCompositionId = Guid.NewGuid(),
                    DisplayName = request.DisplayName.Trim(),
                    DosageForm = string.IsNullOrWhiteSpace(request.DosageForm) ? null : request.DosageForm.Trim().ToUpperInvariant(),
                    CreatedAt = DateTime.UtcNow,
                };
                _context.SaltComposition.Add(composition);

                foreach (var c in request.Components)
                {
                    _context.SaltCompositionComponent.Add(new SaltCompositionComponent
                    {
                        SaltCompositionComponentId = Guid.NewGuid(),
                        SaltCompositionId = composition.SaltCompositionId,
                        MoleculeId = c.MoleculeId,
                        StrengthValue = c.StrengthValue,
                        StrengthUnit = c.StrengthUnit?.Trim().ToUpperInvariant() ?? "MG",
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);
                return new CreateSaltCompositionResponseModel { Success = true, Message = "Salt composition created.", SaltCompositionId = composition.SaltCompositionId };
            }
            catch (Exception)
            {
                return new CreateSaltCompositionResponseModel { Success = false, Message = "Error creating salt composition." };
            }
        }
    }
}
