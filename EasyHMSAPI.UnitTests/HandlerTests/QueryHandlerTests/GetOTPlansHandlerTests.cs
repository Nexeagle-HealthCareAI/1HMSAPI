using System;
using System.Linq;
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
    public class GetOTPlansHandlerTests
    {
        private AppDbContext _context = null!;
        private GetOTPlansHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetOTPlansHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsActivePlans_WithDepartmentName()
        {
            var hospitalId = Guid.NewGuid();
            var department = new Department { DepartmentID = Guid.NewGuid(), HospitalID = hospitalId, Name = "Urology", IsActive = true, CreatedAt = DateTime.UtcNow };
            _context.Departments.Add(department);

            _context.OTPlans.Add(new OTPlan
            {
                OtPlanId = Guid.NewGuid(), HospitalId = hospitalId, DepartmentId = department.DepartmentID,
                PlanName = "PCNL Plan", ProcedureName = "PCNL", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.OTPlans.Add(new OTPlan
            {
                OtPlanId = Guid.NewGuid(), HospitalId = hospitalId, DepartmentId = null,
                PlanName = "General Surgery Plan", ProcedureName = "General Surgery", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.OTPlans.Add(new OTPlan
            {
                OtPlanId = Guid.NewGuid(), HospitalId = hospitalId, DepartmentId = department.DepartmentID,
                PlanName = "Inactive Plan", ProcedureName = "N/A", IsActive = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetOTPlansRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Plans, Has.Count.EqualTo(2)); // inactive one excluded by default
            var pcnl = response.Plans.First(p => p.PlanName == "PCNL Plan");
            Assert.That(pcnl.DepartmentName, Is.EqualTo("Urology"));
            var general = response.Plans.First(p => p.PlanName == "General Surgery Plan");
            Assert.That(general.DepartmentName, Is.Null);
        }

        [Test]
        public async Task Handle_IncludeInactive_ReturnsAll()
        {
            var hospitalId = Guid.NewGuid();
            _context.OTPlans.Add(new OTPlan
            {
                OtPlanId = Guid.NewGuid(), HospitalId = hospitalId,
                PlanName = "Inactive Plan", ProcedureName = "N/A", IsActive = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetOTPlansRequestModel { HospitalId = hospitalId, IncludeInactive = true }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Plans, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Handle_FilterByDepartment_ReturnsOnlyThatDepartment()
        {
            var hospitalId = Guid.NewGuid();
            var urology = Guid.NewGuid();
            var gynae = Guid.NewGuid();
            _context.OTPlans.Add(new OTPlan { OtPlanId = Guid.NewGuid(), HospitalId = hospitalId, DepartmentId = urology, PlanName = "PCNL Plan", ProcedureName = "PCNL", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.OTPlans.Add(new OTPlan { OtPlanId = Guid.NewGuid(), HospitalId = hospitalId, DepartmentId = gynae, PlanName = "Hysterectomy Plan", ProcedureName = "Hysterectomy", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetOTPlansRequestModel { HospitalId = hospitalId, DepartmentId = urology }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Plans, Has.Count.EqualTo(1));
            Assert.That(response.Plans[0].PlanName, Is.EqualTo("PCNL Plan"));
        }
    }
}
