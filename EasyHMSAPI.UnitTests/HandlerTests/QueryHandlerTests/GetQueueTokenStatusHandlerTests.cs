using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetQueueTokenStatusHandlerTests
    {
        private AppDbContext _context = null!;
        private GetQueueTokenStatusHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _doctorId;
        private DateTime _tokenDate;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetQueueTokenStatusHandler(_context);
            _hospitalId = Guid.NewGuid();
            _doctorId = Guid.NewGuid();
            _tokenDate = DateTime.UtcNow.Date;
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private AppointmentToken SeedToken(Guid apptId, int tokenNo, int queueSequence, string status)
        {
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
                IsManual = false,
                CreatedAt = DateTime.UtcNow,
            };
            _context.AppointmentTokens.Add(token);
            _context.SaveChanges();
            return token;
        }

        [Test]
        public async Task Handle_NoTokenForAppointment_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetQueueTokenStatusRequestModel { AppointmentId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_WaitingToken_ComputesPositionAndEstimatedWait()
        {
            var myApptId = Guid.NewGuid();
            SeedToken(Guid.NewGuid(), tokenNo: 1, queueSequence: 1, status: AppConstants.QueueTokenStatus_Called);
            SeedToken(Guid.NewGuid(), tokenNo: 2, queueSequence: 2, status: AppConstants.QueueTokenStatus_Waiting);
            SeedToken(myApptId, tokenNo: 3, queueSequence: 3, status: AppConstants.QueueTokenStatus_Waiting);
            _context.DoctorQueues.Add(new DoctorQueue { HospitalId = _hospitalId, DoctorId = _doctorId, TokenDate = _tokenDate, NextTokenNo = 4, TokenStrategy = AppConstants.TokenStrategy_Sequential, CurrentServingTokenNo = 1 });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetQueueTokenStatusRequestModel { AppointmentId = myApptId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.TokenNo, Is.EqualTo(3));
            Assert.That(response.CurrentServingTokenNo, Is.EqualTo(1));
            // Position counts WAITING/CALLED tokens with QueueSequence <= mine: tokens 1,2,3 => 3.
            Assert.That(response.PositionInQueue, Is.EqualTo(3));
            Assert.That(response.EstimatedWaitMinutes, Is.EqualTo(2 * AppConstants.QueueAverageConsultMinutes));
        }

        [Test]
        public async Task Handle_DoneToken_NoPositionOrEstimate()
        {
            var apptId = Guid.NewGuid();
            SeedToken(apptId, tokenNo: 1, queueSequence: 1, status: AppConstants.QueueTokenStatus_Done);

            var response = await _handler.Handle(new GetQueueTokenStatusRequestModel { AppointmentId = apptId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PositionInQueue, Is.Null);
            Assert.That(response.EstimatedWaitMinutes, Is.Null);
        }
    }
}
