using System;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.UnitTests
{
 public static class InMemoryDbContextFactory
 {
 public static AppDbContext CreateContext()
 {
 var options = new DbContextOptionsBuilder<AppDbContext>()
 .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
 .Options;
 return new AppDbContext(options);
 }

 public static void Destroy(AppDbContext context)
 {
 context.Database.EnsureDeleted();
 context.Dispose();
 }
 }
}
