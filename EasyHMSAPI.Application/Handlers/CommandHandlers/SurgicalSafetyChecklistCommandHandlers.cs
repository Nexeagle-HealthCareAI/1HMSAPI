using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// WHO Surgical Safety Checklist — one row per SurgeryCase (get-or-create), 3 phases recorded
    /// in sequence: Sign-In before anaesthesia, Time-Out before incision, Sign-Out before leaving
    /// the theatre. A phase can only be recorded with EVERY item of that phase confirmed, in the right
    /// stage of the case, and exactly once: a completed phase is never overwritten. Corrections are
    /// appended as attributed addenda.
    /// </summary>
    public class SurgicalSafetyChecklistCommandHandlers :
        IRequestHandler<RecordSignInRequestModel, RecordSignInResponseModel>,
        IRequestHandler<RecordTimeOutRequestModel, RecordTimeOutResponseModel>,
        IRequestHandler<RecordSignOutRequestModel, RecordSignOutResponseModel>,
        IRequestHandler<AddChecklistAddendumRequestModel, AddChecklistAddendumResponseModel>
    {
        private enum Phase { SignIn, TimeOut, SignOut }

        private static readonly string[] SignInStages = { IpdConstants.SurgeryStatus.Scheduled, IpdConstants.SurgeryStatus.PreOp };
        private static readonly string[] TheatreStage = { IpdConstants.SurgeryStatus.InTheatre };

        private readonly AppDbContext _context;

        public SurgicalSafetyChecklistCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        private static IReadOnlyList<(string Key, string Label)> ItemsFor(Phase phase) => phase switch
        {
            Phase.SignIn => IpdConstants.WhoChecklistItems.SignIn,
            Phase.TimeOut => IpdConstants.WhoChecklistItems.TimeOut,
            _ => IpdConstants.WhoChecklistItems.SignOut,
        };

        private static string Name(Phase phase) => phase switch { Phase.SignIn => "Sign-In", Phase.TimeOut => "Time-Out", _ => "Sign-Out" };

        /// <summary>Null when the posted items are exactly the phase's WHO items, all confirmed.</summary>
        private static string? ValidateItems(Phase phase, Dictionary<string, bool>? items)
        {
            var expected = ItemsFor(phase);
            items ??= new Dictionary<string, bool>();

            var unknown = items.Keys.Where(k => expected.All(e => e.Key != k)).ToList();
            if (unknown.Count > 0)
                return $"{Name(phase)} contains items that are not on the WHO checklist: {string.Join(", ", unknown)}.";

            var unconfirmed = expected.Where(e => !items.TryGetValue(e.Key, out var ticked) || !ticked).Select(e => e.Label).ToList();
            if (unconfirmed.Count > 0)
                return $"{Name(phase)} cannot be recorded until every item is confirmed. Not confirmed: {string.Join("; ", unconfirmed)}.";

            return null;
        }

        private async Task<SurgicalSafetyChecklist> GetOrCreateAsync(Guid hospitalId, Guid surgeryCaseId, CancellationToken cancellationToken)
        {
            var checklist = await _context.SurgicalSafetyChecklist
                .FirstOrDefaultAsync(c => c.SurgeryCaseId == surgeryCaseId && c.HospitalId == hospitalId, cancellationToken);
            if (checklist != null)
                return checklist;

            var now = DateTime.UtcNow;
            checklist = new SurgicalSafetyChecklist
            {
                ChecklistId = Guid.NewGuid(),
                HospitalId = hospitalId,
                SurgeryCaseId = surgeryCaseId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _context.SurgicalSafetyChecklist.Add(checklist);
            return checklist;
        }

        /// <summary>
        /// Shared recording path. Returns a failure message, or null on success. Order of checks: case exists and is
        /// in the right stage, the previous phase is done, this phase is not already done, all items confirmed.
        /// </summary>
        private async Task<string?> RecordAsync(
            Phase phase, Guid hospitalId, Guid surgeryCaseId, Dictionary<string, bool>? items, string? notes, string? by, CancellationToken cancellationToken)
        {
            if (hospitalId == Guid.Empty || surgeryCaseId == Guid.Empty)
                return "HospitalId and SurgeryCaseId are required.";

            var surgeryCase = await _context.SurgeryCase
                .FirstOrDefaultAsync(s => s.SurgeryCaseId == surgeryCaseId && s.HospitalId == hospitalId, cancellationToken);
            if (surgeryCase == null)
                return "Surgery case not found.";

            var allowed = phase == Phase.SignIn ? SignInStages : TheatreStage;
            if (!allowed.Contains(surgeryCase.StatusCode))
            {
                var window = phase == Phase.SignIn ? "while the case is scheduled or in pre-op" : "while the patient is in theatre";
                return $"{Name(phase)} can only be recorded {window} (this case is {surgeryCase.StatusCode}).";
            }

            var existing = await _context.SurgicalSafetyChecklist
                .FirstOrDefaultAsync(c => c.SurgeryCaseId == surgeryCaseId && c.HospitalId == hospitalId, cancellationToken);

            if (phase == Phase.TimeOut && existing?.SignInCompletedAt == null)
                return "Sign-In must be completed before Time-Out.";
            if (phase == Phase.SignOut && existing?.TimeOutCompletedAt == null)
                return "Time-Out must be completed before Sign-Out.";

            var doneAt = phase switch { Phase.SignIn => existing?.SignInCompletedAt, Phase.TimeOut => existing?.TimeOutCompletedAt, _ => existing?.SignOutCompletedAt };
            if (doneAt != null)
            {
                var doneBy = phase switch { Phase.SignIn => existing!.SignInCompletedBy, Phase.TimeOut => existing!.TimeOutCompletedBy, _ => existing!.SignOutCompletedBy };
                return $"{Name(phase)} was already completed by {(string.IsNullOrWhiteSpace(doneBy) ? "a team member" : doneBy)} at {doneAt:yyyy-MM-dd HH:mm} UTC and cannot be changed. Add an addendum to correct or clarify it.";
            }

            var itemProblem = ValidateItems(phase, items);
            if (itemProblem != null)
                return itemProblem;

            var checklist = existing ?? await GetOrCreateAsync(hospitalId, surgeryCaseId, cancellationToken);
            var now = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(items);
            var cleanNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            switch (phase)
            {
                case Phase.SignIn:
                    checklist.SignInItemsJson = json; checklist.SignInNotes = cleanNotes; checklist.SignInCompletedAt = now; checklist.SignInCompletedBy = by;
                    break;
                case Phase.TimeOut:
                    checklist.TimeOutItemsJson = json; checklist.TimeOutNotes = cleanNotes; checklist.TimeOutCompletedAt = now; checklist.TimeOutCompletedBy = by;
                    break;
                default:
                    checklist.SignOutItemsJson = json; checklist.SignOutNotes = cleanNotes; checklist.SignOutCompletedAt = now; checklist.SignOutCompletedBy = by;
                    break;
            }
            checklist.UpdatedAt = now;
            checklist.UpdatedBy = by;

            await _context.SaveChangesAsync(cancellationToken);
            return null;
        }

        public async Task<RecordSignInResponseModel> Handle(RecordSignInRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var problem = await RecordAsync(Phase.SignIn, request.HospitalId, request.SurgeryCaseId, request.Items, request.Notes, request.LoggedInUserName, cancellationToken);
                return problem == null
                    ? new RecordSignInResponseModel { Success = true, Message = "Sign-In recorded." }
                    : new RecordSignInResponseModel { Success = false, Message = problem };
            }
            catch (Exception)
            {
                return new RecordSignInResponseModel { Success = false, Message = "Error recording Sign-In." };
            }
        }

        public async Task<RecordTimeOutResponseModel> Handle(RecordTimeOutRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var problem = await RecordAsync(Phase.TimeOut, request.HospitalId, request.SurgeryCaseId, request.Items, request.Notes, request.LoggedInUserName, cancellationToken);
                return problem == null
                    ? new RecordTimeOutResponseModel { Success = true, Message = "Time-Out recorded." }
                    : new RecordTimeOutResponseModel { Success = false, Message = problem };
            }
            catch (Exception)
            {
                return new RecordTimeOutResponseModel { Success = false, Message = "Error recording Time-Out." };
            }
        }

        public async Task<RecordSignOutResponseModel> Handle(RecordSignOutRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var problem = await RecordAsync(Phase.SignOut, request.HospitalId, request.SurgeryCaseId, request.Items, request.Notes, request.LoggedInUserName, cancellationToken);
                return problem == null
                    ? new RecordSignOutResponseModel { Success = true, Message = "Sign-Out recorded." }
                    : new RecordSignOutResponseModel { Success = false, Message = problem };
            }
            catch (Exception)
            {
                return new RecordSignOutResponseModel { Success = false, Message = "Error recording Sign-Out." };
            }
        }

        /// <summary>
        /// Appends a timestamped, attributed correction/clarification to a COMPLETED phase's notes. The confirmed items and the
        /// original sign-off are never touched.
        /// </summary>
        public async Task<AddChecklistAddendumResponseModel> Handle(AddChecklistAddendumRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                    return new AddChecklistAddendumResponseModel { Success = false, Message = "HospitalId and SurgeryCaseId are required." };

                var text = request.Text?.Trim();
                if (string.IsNullOrEmpty(text) || text.Length < 5)
                    return new AddChecklistAddendumResponseModel { Success = false, Message = "The addendum needs at least 5 characters." };

                if (!Enum.TryParse<Phase>(request.Phase?.Replace("-", "").Replace("_", ""), true, out var phase))
                    return new AddChecklistAddendumResponseModel { Success = false, Message = "Phase must be SignIn, TimeOut or SignOut." };

                var checklist = await _context.SurgicalSafetyChecklist
                    .FirstOrDefaultAsync(c => c.SurgeryCaseId == request.SurgeryCaseId && c.HospitalId == request.HospitalId, cancellationToken);
                var completed = phase switch { Phase.SignIn => checklist?.SignInCompletedAt, Phase.TimeOut => checklist?.TimeOutCompletedAt, _ => checklist?.SignOutCompletedAt };
                if (checklist == null || completed == null)
                    return new AddChecklistAddendumResponseModel { Success = false, Message = $"{Name(phase)} has not been completed, so there is nothing to add an addendum to." };

                var now = DateTime.UtcNow;
                var line = $"[Addendum {now:yyyy-MM-dd HH:mm} UTC, {(string.IsNullOrWhiteSpace(request.LoggedInUserName) ? "unknown user" : request.LoggedInUserName)}] {text}";
                string Append(string? existing) => string.IsNullOrWhiteSpace(existing) ? line : existing + "\n" + line;

                switch (phase)
                {
                    case Phase.SignIn: checklist.SignInNotes = Append(checklist.SignInNotes); break;
                    case Phase.TimeOut: checklist.TimeOutNotes = Append(checklist.TimeOutNotes); break;
                    default: checklist.SignOutNotes = Append(checklist.SignOutNotes); break;
                }
                checklist.UpdatedAt = now;
                checklist.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new AddChecklistAddendumResponseModel { Success = true, Message = "Addendum added." };
            }
            catch (Exception)
            {
                return new AddChecklistAddendumResponseModel { Success = false, Message = "Error adding the addendum." };
            }
        }
    }
}
