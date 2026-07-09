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
        public async Task Handle_ResolvesMultiplePackageTypes_PerPlan()
        {
            var hospitalId = Guid.NewGuid();
            var plan = new OTPlan
            {
                OtPlanId = Guid.NewGuid(), HospitalId = hospitalId,
                PlanName = "PCNL Plan", ProcedureName = "PCNL", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            var fullPackage = new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Full Package", Price = 50000m, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            var nonPackage = new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Non Package", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.OTPlans.Add(plan);
            _context.PackageTypes.AddRange(fullPackage, nonPackage);
            _context.OTPlanPackageTypes.AddRange(
                new OTPlanPackageType { OtPlanId = plan.OtPlanId, PackageTypeId = fullPackage.PackageTypeId, CreatedAt = DateTime.UtcNow },
                new OTPlanPackageType { OtPlanId = plan.OtPlanId, PackageTypeId = nonPackage.PackageTypeId, CreatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetOTPlansRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var pcnl = response.Plans.First(p => p.PlanName == "PCNL Plan");
            Assert.That(pcnl.PackageTypes, Has.Count.EqualTo(2));
            Assert.That(pcnl.PackageTypes.Select(pt => pt.Name), Is.EquivalentTo(new[] { "Full Package", "Non Package" }));
            Assert.That(pcnl.PackageTypes.First(pt => pt.Name == "Full Package").Price, Is.EqualTo(50000m));
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
