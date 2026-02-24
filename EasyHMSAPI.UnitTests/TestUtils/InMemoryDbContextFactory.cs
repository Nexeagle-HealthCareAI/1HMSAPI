using System;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EasyHMSAPI.UnitTests
{
 public static class InMemoryDbContextFactory
 {
 public static AppDbContext CreateContext()
 {
 var services = new ServiceCollection();
 services.AddEntityFrameworkInMemoryDatabase();
 var serviceProvider = services.BuildServiceProvider();

 var options = new DbContextOptionsBuilder<AppDbContext>()
 .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
 .UseInternalServiceProvider(serviceProvider)
 .ConfigureWarnings(w => 
 {
 w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
 })
 .EnableSensitiveDataLogging()
 .Options;

 var context = new AppDbContext(options);
 return context;
 }

 public static void Destroy(AppDbContext context)
 {
 if (context != null)
 {
 try
 {
 context.Database.EnsureDeleted();
 }
 catch
 {
 // Ignore errors
 }
 finally
 {
 context.Dispose();
 }
 }
 }
 }
}

