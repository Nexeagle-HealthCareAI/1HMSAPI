using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetDoctorDischargeFieldConfigHandlerTests
    {
        private AppDbContext _context = null!;
        private GetDoctorDischargeFieldConfigHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetDoctorDischargeFieldConfigHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_HospitalSpecificRowExists_ReturnsIt()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            _context.DoctorDischargeFieldConfigs.Add(new DoctorDischargeFieldConfig
            {
                ConfigId = Guid.NewGuid(), DoctorId = doctorId, HospitalId = hospitalId,
                ConfigJson = "[{\"key\":\"chiefComplaint\",\"label\":\"Hospital A label\",\"type\":\"builtin\",\"builtIn\":true,\"showInPad\":true,\"showInPrint\":true,\"order\":0}]",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetDoctorDischargeFieldConfigRequestModel { DoctorId = doctorId, HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Fields, Has.Count.EqualTo(1));
            Assert.That(response.Fields[0].Label, Is.EqualTo("Hospital A label"));
        }

        [Test]
        public async Task Handle_NoHospitalSpecificRow_FallsBackToLegacyNullHospitalRow()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            _context.DoctorDischargeFieldConfigs.Add(new DoctorDischargeFieldConfig
            {
                ConfigId = Guid.NewGuid(), DoctorId = doctorId, HospitalId = null,
                ConfigJson = "[{\"key\":\"chiefComplaint\",\"label\":\"Legacy global label\",\"type\":\"builtin\",\"builtIn\":true,\"showInPad\":true,\"showInPrint\":true,\"order\":0}]",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetDoctorDischargeFieldConfigRequestModel { DoctorId = doctorId, HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Fields, Has.Count.EqualTo(1));
            Assert.That(response.Fields[0].Label, Is.EqualTo("Legacy global label"));
        }

        [Test]
        public async Task Handle_HospitalSpecificRowAndLegacyRowBothExist_PrefersHospitalSpecific()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            _context.DoctorDischargeFieldConfigs.AddRange(
                new DoctorDischargeFieldConfig
                {
                    ConfigId = Guid.NewGuid(), DoctorId = doctorId, HospitalId = null,
                    ConfigJson = "[{\"key\":\"chiefComplaint\",\"label\":\"Legacy\",\"type\":\"builtin\",\"builtIn\":true,\"showInPad\":true,\"showInPrint\":true,\"order\":0}]",
                    CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
                },
                new DoctorDischargeFieldConfig
                {
                    ConfigId = Guid.NewGuid(), DoctorId = doctorId, HospitalId = hospitalId,
                    ConfigJson = "[{\"key\":\"chiefComplaint\",\"label\":\"Hospital-specific\",\"type\":\"builtin\",\"builtIn\":true,\"showInPad\":true,\"showInPrint\":true,\"order\":0}]",
                    CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
                });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetDoctorDischargeFieldConfigRequestModel { DoctorId = doctorId, HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Fields[0].Label, Is.EqualTo("Hospital-specific"));
        }

        [Test]
        public async Task Handle_NoRowsAtAll_ReturnsEmptyList()
        {
            var response = await _handler.Handle(new GetDoctorDischargeFieldConfigRequestModel { DoctorId = Guid.NewGuid(), HospitalId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Fields, Is.Empty);
        }

        [Test]
        public async Task Handle_DifferentDoctorsHospitalRowsDoNotLeak()
        {
            var doctorA = Guid.NewGuid();
            var doctorB = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            _context.DoctorDischargeFieldConfigs.Add(new DoctorDischargeFieldConfig
            {
                ConfigId = Guid.NewGuid(), DoctorId = doctorA, HospitalId = hospitalId,
                ConfigJson = "[{\"key\":\"chiefComplaint\",\"label\":\"Doctor A's field\",\"type\":\"builtin\",\"builtIn\":true,\"showInPad\":true,\"showInPrint\":true,\"order\":0}]",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetDoctorDischargeFieldConfigRequestModel { DoctorId = doctorB, HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Fields, Is.Empty);
        }
    }
}
