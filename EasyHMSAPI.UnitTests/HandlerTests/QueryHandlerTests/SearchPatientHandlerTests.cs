using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.AspNetCore.Http;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class SearchPatientHandlerTests
    {
         private AppDbContext _context = null!;
        private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
        private SearchPatientHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] 
            {
                new Claim("hospitalId", Guid.NewGuid().ToString())
            }));
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

            _handler = new SearchPatientHandler(_context, _httpContextAccessorMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_SearchByMobile_ReturnsPatient()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var patient = new PatientRegistration 
            { 
                PatientId = "PAT1", 
                HospitalId = hospitalId, 
                FullName = "John", 
                Mobile = "1234567890" 
            };
            _context.PatientRegistrations.Add(patient);
            await _context.SaveChangesAsync();

            var request = new SearchPatientRequestModel
            {
                HospitalId = hospitalId,
                By = "mobile",
                Q = "12345"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].FullName, Is.EqualTo("John"));
        }
    }
}
