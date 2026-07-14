using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdateDoctorDischargeFieldConfigHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateDoctorDischargeFieldConfigHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdateDoctorDischargeFieldConfigHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private static DischargeFieldConfigItemModel Field(string key, string label) =>
            new() { Key = key, Label = label, Type = "builtin", BuiltIn = true, ShowInPad = true, ShowInPrint = true, Order = 0 };

        [Test]
        public async Task Handle_NoExistingRow_CreatesHospitalSpecificRow()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();

            var response = await _handler.Handle(new UpdateDoctorDischargeFieldConfigRequestModel
            {
                DoctorId = doctorId, HospitalId = hospitalId, Fields = new() { Field("chiefComplaint", "Presenting complaint") },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var row = _context.DoctorDischargeFieldConfigs.Single(c => c.DoctorId == doctorId);
            Assert.That(row.HospitalId, Is.EqualTo(hospitalId));
            Assert.That(row.ConfigJson, Does.Contain("Presenting complaint"));
        }

        [Test]
        public async Task Handle_ExistingLegacyNullHospitalRow_DoesNotTouchIt_CreatesNewHospitalRow()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            _context.DoctorDischargeFieldConfigs.Add(new DoctorDischargeFieldConfig
            {
                ConfigId = Guid.NewGuid(), DoctorId = doctorId, HospitalId = null,
                ConfigJson = "[{\"key\":\"chiefComplaint\",\"label\":\"Legacy\",\"type\":\"builtin\",\"builtIn\":true,\"showInPad\":true,\"showInPrint\":true,\"order\":0}]",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateDoctorDischargeFieldConfigRequestModel
            {
                DoctorId = doctorId, HospitalId = hospitalId, Fields = new() { Field("chiefComplaint", "New hospital label") },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var rows = _context.DoctorDischargeFieldConfigs.Where(c => c.DoctorId == doctorId).ToList();
            Assert.That(rows, Has.Count.EqualTo(2));
            var legacyRow = rows.Single(r => r.HospitalId == null);
            Assert.That(legacyRow.ConfigJson, Does.Contain("Legacy"));
            var hospitalRow = rows.Single(r => r.HospitalId == hospitalId);
            Assert.That(hospitalRow.ConfigJson, Does.Contain("New hospital label"));
        }

        [Test]
        public async Task Handle_ExistingHospitalSpecificRow_UpdatesInPlace_DoesNotDuplicate()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var existingId = Guid.NewGuid();
            _context.DoctorDischargeFieldConfigs.Add(new DoctorDischargeFieldConfig
            {
                ConfigId = existingId, DoctorId = doctorId, HospitalId = hospitalId,
                ConfigJson = "[{\"key\":\"chiefComplaint\",\"label\":\"Old label\",\"type\":\"builtin\",\"builtIn\":true,\"showInPad\":true,\"showInPrint\":true,\"order\":0}]",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            await _handler.Handle(new UpdateDoctorDischargeFieldConfigRequestModel
            {
                DoctorId = doctorId, HospitalId = hospitalId, Fields = new() { Field("chiefComplaint", "Updated label") },
            }, CancellationToken.None);

            var rows = _context.DoctorDischargeFieldConfigs.Where(c => c.DoctorId == doctorId).ToList();
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].ConfigId, Is.EqualTo(existingId));
            Assert.That(rows[0].ConfigJson, Does.Contain("Updated label"));
        }

        [Test]
        public async Task Handle_AnotherHospitalsRowForSameDoctor_UnaffectedBySave()
        {
            var doctorId = Guid.NewGuid();
            var hospitalA = Guid.NewGuid();
            var hospitalB = Guid.NewGuid();
            _context.DoctorDischargeFieldConfigs.Add(new DoctorDischargeFieldConfig
            {
                ConfigId = Guid.NewGuid(), DoctorId = doctorId, HospitalId = hospitalA,
                ConfigJson = "[{\"key\":\"chiefComplaint\",\"label\":\"Hospital A label\",\"type\":\"builtin\",\"builtIn\":true,\"showInPad\":true,\"showInPrint\":true,\"order\":0}]",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            await _handler.Handle(new UpdateDoctorDischargeFieldConfigRequestModel
            {
                DoctorId = doctorId, HospitalId = hospitalB, Fields = new() { Field("chiefComplaint", "Hospital B label") },
            }, CancellationToken.None);

            var hospitalARow = _context.DoctorDischargeFieldConfigs.Single(c => c.DoctorId == doctorId && c.HospitalId == hospitalA);
            Assert.That(hospitalARow.ConfigJson, Does.Contain("Hospital A label"));
        }

        [Test]
        public async Task Handle_MissingHospitalId_ReturnsFailure()
        {
            var response = await _handler.Handle(new UpdateDoctorDischargeFieldConfigRequestModel
            {
                DoctorId = Guid.NewGuid(), HospitalId = Guid.Empty, Fields = new() { Field("chiefComplaint", "x") },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Hospital"));
        }
    }
}
