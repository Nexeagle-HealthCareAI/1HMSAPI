using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Transcribes (Whisper) and structures (LLM) a doctor's voice dictation into prescription fields.
    /// The result is for the doctor to review and apply — nothing is saved here.
    /// </summary>
    public class ParseVoiceRxHandler : IRequestHandler<ParseVoiceRxRequestModel, ParseVoiceRxResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IVoiceRxService _voiceRx;

        public ParseVoiceRxHandler(AppDbContext context, IVoiceRxService voiceRx)
        {
            _context = context;
            _voiceRx = voiceRx;
        }

        public async Task<ParseVoiceRxResponseModel> Handle(ParseVoiceRxRequestModel request, CancellationToken cancellationToken)
        {
            if (request.Audio == null || request.Audio.Length == 0)
                return Fail("No audio was provided.");
            if (request.HospitalId == Guid.Empty)
                return Fail("Hospital is required.");

            // The caller must belong to the hospital they're dictating for.
            var callerIsMember = await _context.HospitalUsers
                .AnyAsync(hu => hu.UserID == request.CallerUserId && hu.HospitalID == request.HospitalId, cancellationToken);
            if (!callerIsMember)
                return Fail("You don't have access to this hospital.");

            // Read the audio into memory (never persisted).
            byte[] audioBytes;
            await using (var stream = new MemoryStream())
            {
                await request.Audio.CopyToAsync(stream, cancellationToken);
                audioBytes = stream.ToArray();
            }

            // A recorded consultation ("ambient") needs speaker separation; a dictation does not.
            var diarize = string.Equals(request.Mode, "ambient", StringComparison.OrdinalIgnoreCase);

            try
            {
                var transcript = await _voiceRx.TranscribeAsync(audioBytes, request.Audio.FileName, request.Language, diarize, cancellationToken);
                if (string.IsNullOrWhiteSpace(transcript))
                    return new ParseVoiceRxResponseModel { Success = true, Message = "Nothing was heard in the recording.", Transcript = string.Empty };

                var fields = await _voiceRx.StructureAsync(transcript, request.Mode, cancellationToken);
                return new ParseVoiceRxResponseModel
                {
                    Success = true,
                    Message = "Dictation processed.",
                    Transcript = transcript,
                    Fields = fields
                };
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message);
            }
            catch
            {
                return Fail("Could not process the dictation. Please try again.");
            }
        }

        private static ParseVoiceRxResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
