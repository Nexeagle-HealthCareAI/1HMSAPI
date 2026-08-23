using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class SkipCurrentPatientHandlerTests
    {
        private AppDbContext _context = null!;
        private SkipCurrentPatientHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _doctorId;
        private DateTime _tokenDate;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SkipCurrentPatientHandler(_context);
            _hospitalId = Guid.NewGuid();
            _doctorId = Guid.NewGuid();
            _tokenDate = DateTime.UtcNow.AddMinutes(330).Date;
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private AppointmentToken SeedToken(int tokenNo, int queueSequence, string status, int skipCount = 0)
        {
            var apptId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment
            {
                ApptId = apptId,
                HospitalId = _hospitalId,
                DoctorId = _doctorId,
                PatientId = "PT001",
                ApptDate = _tokenDate,
                StartAt = DateTime.UtcNow,
                EndAt = DateTime.UtcNow.AddMinutes(15),
                CurrentStatusCode = "PRE_APPOINTMENT",
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
            var token = new AppointmentToken
            {
                TokenId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = _doctorId,
                ApptId = apptId,
                TokenDate = _tokenDate,
                TokenNo = tokenNo,
                QueueSequence = queueSequence,
                Status = status,
                SkipCount = skipCount,
                IsManual = false,
                CreatedAt = DateTime.UtcNow,
            };
            _context.AppointmentTokens.Add(token);
            _context.SaveChanges();
            return token;
        }

        private void SeedDoctorQueue(int? currentServingTokenNo)
        {
            _context.DoctorQueues.Add(new DoctorQueue
            {
                HospitalId = _hospitalId,
                DoctorId = _doctorId,
                TokenDate = _tokenDate,
                NextTokenNo = 10,
                TokenStrategy = AppConstants.TokenStrategy_Sequential,
                CurrentServingTokenNo = currentServingTokenNo,
            });
            _context.SaveChanges();
        }

        [Test]
        public async Task Handle_SkipsCalledPatient_RequeuesThreePositionsLater()
        {
            var called = SeedToken(tokenNo: 1, queueSequence: 1, status: AppConstants.QueueTokenStatus_Called);
            SeedToken(tokenNo: 2, queueSequence: 2, status: AppConstants.QueueTokenStatus_Waiting);
            SeedToken(tokenNo: 3, queueSequence: 3, status: AppConstants.QueueTokenStatus_Waiting);
            SeedToken(tokenNo: 4, queueSequence: 4, status: AppConstants.QueueTokenStatus_Waiting);
            SeedToken(tokenNo: 5, queueSequence: 5, status: AppConstants.QueueTokenStatus_Waiting);
            SeedDoctorQueue(currentServingTokenNo: 1);

            var response = await _handler.Handle(new SkipCurrentPatientRequestModel { HospitalId = _hospitalId, DoctorId = _doctorId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);

            var reloaded = await _context.AppointmentTokens.FirstAsync(t => t.TokenId == called.TokenId);
            Assert.That(reloaded.Status, Is.EqualTo(AppConstants.QueueTokenStatus_Waiting));
            Assert.That(reloaded.SkipCount, Is.EqualTo(1));
            // 3 waiting patients (2,3,4) were ahead of it after re-sequencing (position+3), so its
            // new QueueSequence should now be 4th among the 4 waiting tokens.
            Assert.That(reloaded.QueueSequence, Is.EqualTo(4));

            var queue = await _context.DoctorQueues.FirstAsync(q => q.HospitalId == _hospitalId && q.DoctorId == _doctorId && q.TokenDate == _tokenDate);
            Assert.That(queue.CurrentServingTokenNo, Is.Null);
        }

        [Test]
        public async Task Handle_FewerThanThreeWaiting_RequeuesAtEnd()
        {
            var called = SeedToken(tokenNo: 1, queueSequence: 1, status: AppConstants.QueueTokenStatus_Called);
            SeedToken(tokenNo: 2, queueSequence: 2, status: AppConstants.QueueTokenStatus_Waiting);
            SeedDoctorQueue(currentServingTokenNo: 1);

            await _handler.Handle(new SkipCurrentPatientRequestModel { HospitalId = _hospitalId, DoctorId = _doctorId }, CancellationToken.None);

            var reloaded = await _context.AppointmentTokens.FirstAsync(t => t.TokenId == called.TokenId);
            Assert.That(reloaded.QueueSequence, Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_AlreadySkippedTwice_RejectsAsHardStop()
        {
            SeedToken(tokenNo: 1, queueSequence: 1, status: AppConstants.QueueTokenStatus_Called, skipCount: 2);
            SeedDoctorQueue(currentServingTokenNo: 1);

            var response = await _handler.Handle(new SkipCurrentPatientRequestModel { HospitalId = _hospitalId, DoctorId = _doctorId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_NoOneCurrentlyCalled_Rejects()
        {
            var response = await _handler.Handle(new SkipCurrentPatientRequestModel { HospitalId = _hospitalId, DoctorId = _doctorId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
