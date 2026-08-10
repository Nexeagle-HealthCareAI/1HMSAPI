using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class CallNextPatientHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IWhatsAppQueueNotifier> _notifierMock = null!;
        private CallNextPatientHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _doctorId;
        private DateTime _tokenDate;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _notifierMock = new Mock<IWhatsAppQueueNotifier>();
            _handler = new CallNextPatientHandler(_context, _notifierMock.Object);
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

        private AppointmentToken SeedWaitingToken(int tokenNo, int queueSequence, DateTime? apptStartAt = null)
        {
            var apptId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment
            {
                ApptId = apptId,
                HospitalId = _hospitalId,
                DoctorId = _doctorId,
                PatientId = "PT001",
                ApptDate = _tokenDate,
                StartAt = apptStartAt ?? DateTime.UtcNow,
                EndAt = (apptStartAt ?? DateTime.UtcNow).AddMinutes(15),
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
                Status = AppConstants.QueueTokenStatus_Waiting,
                IsManual = false,
                CreatedAt = DateTime.UtcNow,
            };
            _context.AppointmentTokens.Add(token);
            _context.SaveChanges();
            return token;
        }

        [Test]
        public async Task Handle_CallsLowestQueueSequence_SetsCurrentServing()
        {
            SeedWaitingToken(tokenNo: 2, queueSequence: 2);
            var first = SeedWaitingToken(tokenNo: 1, queueSequence: 1);
            _context.DoctorQueues.Add(new DoctorQueue { HospitalId = _hospitalId, DoctorId = _doctorId, TokenDate = _tokenDate, NextTokenNo = 3, TokenStrategy = AppConstants.TokenStrategy_Sequential });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new CallNextPatientRequestModel { HospitalId = _hospitalId, DoctorId = _doctorId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.TokenNo, Is.EqualTo(1));
            Assert.That(response.AppointmentId, Is.EqualTo(first.ApptId));

            var reloaded = await _context.AppointmentTokens.FirstAsync(t => t.TokenId == first.TokenId);
            Assert.That(reloaded.Status, Is.EqualTo(AppConstants.QueueTokenStatus_Called));
            Assert.That(reloaded.CalledAt, Is.Not.Null);

            var queue = await _context.DoctorQueues.FirstAsync(q => q.HospitalId == _hospitalId && q.DoctorId == _doctorId && q.TokenDate == _tokenDate);
            Assert.That(queue.CurrentServingTokenNo, Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_NoWaitingPatients_ReturnsFailure()
        {
            var response = await _handler.Handle(new CallNextPatientRequestModel { HospitalId = _hospitalId, DoctorId = _doctorId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_NotifiesAllWaitingAndCalledAppointments()
        {
            var a = SeedWaitingToken(tokenNo: 1, queueSequence: 1);
            var b = SeedWaitingToken(tokenNo: 2, queueSequence: 2);

            await _handler.Handle(new CallNextPatientRequestModel { HospitalId = _hospitalId, DoctorId = _doctorId }, CancellationToken.None);

            _notifierMock.Verify(n => n.NotifyTokenCalledAsync(a.ApptId, 1, null, It.IsAny<CancellationToken>()), Times.Once);
            _notifierMock.Verify(n => n.NotifyTokenCalledAsync(b.ApptId, 1, null, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
