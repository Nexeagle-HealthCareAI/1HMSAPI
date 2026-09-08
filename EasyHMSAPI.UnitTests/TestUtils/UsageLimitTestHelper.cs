using System.Threading;
using EasyHMSAPI.Application.Services.Interfaces;
using Moq;

namespace EasyHMSAPI.UnitTests.TestUtils
{
    // Shared mock for the 5 handlers gated by IUsageLimitService -- AlwaysAllow() is what nearly
    // every existing test for those handlers wants (the free-tier quota is a separate concern
    // from what each of those tests actually verifies); AlwaysBlock() is for the handful of tests
    // that specifically assert the blocked-by-limit path.
    public static class UsageLimitTestHelper
    {
        public static IUsageLimitService AlwaysAllow()
        {
            var mock = new Mock<IUsageLimitService>();
            var result = new UsageLimitResult { Allowed = true, UsedCount = 1, Limit = 100 };
            mock.Setup(m => m.TryConsumeAsync(It.IsAny<System.Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
            mock.Setup(m => m.GetStatusAsync(It.IsAny<System.Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
            return mock.Object;
        }

        public static IUsageLimitService AlwaysBlock(string message = "Free monthly limit of 100 patient management actions reached. Upgrade your plan to continue, or wait until next month.")
        {
            var mock = new Mock<IUsageLimitService>();
            var result = new UsageLimitResult { Allowed = false, UsedCount = 100, Limit = 100, Message = message };
            mock.Setup(m => m.TryConsumeAsync(It.IsAny<System.Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
            mock.Setup(m => m.GetStatusAsync(It.IsAny<System.Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
            return mock.Object;
        }
    }
}
