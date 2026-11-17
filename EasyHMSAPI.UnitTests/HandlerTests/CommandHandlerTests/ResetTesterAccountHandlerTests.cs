using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Resetting a tester number: the safety rules and the order of the deletes. The database calls are faked
    /// (ITesterResetSql): each COUNT answers the number scripted for its table, each DELETE is recorded.
    /// </summary>
    [TestFixture]
    public class ResetTesterAccountHandlerTests
    {
        private const string Tester = "9000000001";
        private AppDbContext _context = null!;
        private FakeSql _sql = null!;

        private sealed class FakeSql : ITesterResetSql
        {
            public List<TesterResetForeignKeyColumn> ForeignKeys = new();
            public List<TesterResetTableColumn> HospitalTables = new();
            public Dictionary<string, int> RowsIn = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Deleted = new();
            public List<string> Statements = new();
            public int Begun, Committed, RolledBack;
            public string? FailOnDeleteOf;

            private static string TableOf(string sql) => Regex.Match(sql, @"FROM \[[^\]]+\]\.\[([^\]]+)\] AS t\d+ WHERE").Groups[1].Value;

            public Task<List<TesterResetForeignKeyColumn>> ForeignKeysAsync(CancellationToken cancellationToken) => Task.FromResult(ForeignKeys);
            public Task<List<TesterResetTableColumn>> HospitalTablesAsync(CancellationToken cancellationToken) => Task.FromResult(HospitalTables);

            public Task<int> CountAsync(string sql, CancellationToken cancellationToken)
            {
                Statements.Add(sql);
                return Task.FromResult(RowsIn.TryGetValue(TableOf(sql), out var n) ? n : 0);
            }

            public Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken)
            {
                Statements.Add(sql);
                var table = TableOf(sql);
                if (string.Equals(table, FailOnDeleteOf, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("conflict with a REFERENCE constraint");
                Deleted.Add(table);
                var n = RowsIn.TryGetValue(table, out var rows) ? rows : 0;
                RowsIn[table] = 0;   // gone: a second route to the same table finds nothing
                return Task.FromResult(n);
            }

            public Task BeginAsync(CancellationToken cancellationToken) { Begun++; return Task.CompletedTask; }
            public Task CommitAsync(CancellationToken cancellationToken) { Committed++; return Task.CompletedTask; }
            public Task RollbackAsync(CancellationToken cancellationToken) { RolledBack++; return Task.CompletedTask; }
        }

        private static TesterResetForeignKeyColumn Fk(string child, string childColumn, string parent, string parentColumn) => new()
        {
            FkName = "FK_" + child + "_" + parent + "_" + childColumn,
            ChildSchema = "dbo", ChildTable = child, ChildColumn = childColumn,
            ParentSchema = "dbo", ParentTable = parent, ParentColumn = parentColumn, Ordinal = 1,
        };

        private static TesterResetTableColumn HospitalTable(string table, string column = "HospitalId") =>
            new() { SchemaName = "dbo", TableName = table, ColumnName = column };

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _sql = new FakeSql
            {
                // Hospitals <- Invoices <- InvoiceItems (no hospital column of its own); Users <- UserAuth, HospitalUsers
                ForeignKeys =
                {
                    Fk("Invoices", "HospitalId", "Hospitals", "HospitalID"),
                    Fk("InvoiceItems", "InvoiceId", "Invoices", "InvoiceId"),
                    Fk("HospitalUsers", "HospitalID", "Hospitals", "HospitalID"),
                    Fk("HospitalUsers", "UserID", "Users", "UserID"),
                    Fk("UserAuth", "UserID", "Users", "UserID"),
                    Fk("Hospitals", "CreatedByUserID", "Users", "UserID"),
                },
                HospitalTables = { HospitalTable("Hospitals", "HospitalID"), HospitalTable("Invoices"), HospitalTable("HospitalUsers", "HospitalID"), HospitalTable("Notes") },
                RowsIn = { ["Hospitals"] = 1, ["Invoices"] = 3, ["InvoiceItems"] = 7, ["HospitalUsers"] = 1, ["Notes"] = 2, ["Users"] = 1, ["UserAuth"] = 1 },
            };
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private static IConfiguration Config(string environment = "Development") =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = environment,
                ["StaffAuth:TesterMobiles"] = Tester,
            }).Build();

        private Task<ResetTesterAccountResponseModel> Reset(string mobile = Tester, bool dryRun = false, string environment = "Development", string? maxRows = null)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = environment,
                ["StaffAuth:TesterMobiles"] = Tester,
                ["StaffAuth:TesterResetMaxRows"] = maxRows,
            }).Build();
            var handler = new ResetTesterAccountHandler(_context, config, NullLogger<ResetTesterAccountHandler>.Instance) { Sql = _sql };
            return handler.Handle(new ResetTesterAccountRequestModel { MobileNumber = mobile, DryRun = dryRun }, CancellationToken.None);
        }

        private (User user, Hospital hospital) SeedTesterWithHospital()
        {
            var user = TestDataFactory.SeedUser(_context, phone: Tester, email: "tester@example.com");
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospital.HospitalID, UserID = user.UserID });
            _context.SaveChanges();
            return (user, hospital);
        }

        [Test]
        public async Task SignedInTester_ResetsOwnAccount_FromTheirUserRow()
        {
            var (user, _) = SeedTesterWithHospital();
            var handler = new ResetTesterAccountHandler(_context, Config(), NullLogger<ResetTesterAccountHandler>.Instance) { Sql = _sql };

            // A number in the body is ignored: the account comes from the sign-in.
            var r = await handler.Handle(new ResetTesterAccountRequestModel { UserId = user.UserID, MobileNumber = "9876543210" }, CancellationToken.None);

            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.AccountFound, Is.True);
            Assert.That(_sql.Deleted, Does.Contain("Users"));
        }

        [Test]
        public async Task SignedInUserWhoIsNotATester_IsRefused()
        {
            var real = TestDataFactory.SeedUser(_context, phone: "9876543210", email: "real@example.com");
            var handler = new ResetTesterAccountHandler(_context, Config(), NullLogger<ResetTesterAccountHandler>.Instance) { Sql = _sql };

            var r = await handler.Handle(new ResetTesterAccountRequestModel { UserId = real.UserID, MobileNumber = Tester }, CancellationToken.None);

            Assert.That(r.Success, Is.False);
            Assert.That(r.StatusCode, Is.EqualTo(400));
            Assert.That(_sql.Statements, Is.Empty);
        }

        [Test]
        public async Task Status_SaysTester_OnlyOnDevelopment()
        {
            var (user, _) = SeedTesterWithHospital();
            var real = TestDataFactory.SeedUser(_context, phone: "9876543210", email: "real@example.com");

            var dev = new TesterAccountStatusHandler(_context, Config());
            var mine = await dev.Handle(new TesterAccountStatusRequestModel { UserId = user.UserID }, CancellationToken.None);
            var other = await dev.Handle(new TesterAccountStatusRequestModel { UserId = real.UserID }, CancellationToken.None);
            var prod = await new TesterAccountStatusHandler(_context, Config("Production")).Handle(new TesterAccountStatusRequestModel { UserId = user.UserID }, CancellationToken.None);

            Assert.That(mine.Available && mine.IsTester, Is.True);
            Assert.That(mine.MaskedMobile, Is.EqualTo("90******01"));
            Assert.That(other.IsTester, Is.False);
            Assert.That(other.MaskedMobile, Is.Null);
            Assert.That(prod.Available, Is.False);
            Assert.That(prod.IsTester, Is.False);
        }

        [Test]
        public async Task NotDevelopment_AnswersNotFound_AndTouchesNothing()
        {
            SeedTesterWithHospital();

            var r = await Reset(environment: "Production");

            Assert.That(r.Success, Is.False);
            Assert.That(r.StatusCode, Is.EqualTo(404));
            Assert.That(_sql.Statements, Is.Empty);
        }

        [Test]
        public async Task NumberThatIsNotATester_IsRefused()
        {
            TestDataFactory.SeedUser(_context, phone: "9876543210", email: "real@example.com");

            var r = await Reset(mobile: "9876543210");

            Assert.That(r.Success, Is.False);
            Assert.That(r.StatusCode, Is.EqualTo(400));
            Assert.That(_sql.Statements, Is.Empty);
        }

        [Test]
        public async Task TesterWithNoAccount_IsAlreadyFree()
        {
            var r = await Reset();

            Assert.That(r.Success, Is.True);
            Assert.That(r.AccountFound, Is.False);
            Assert.That(_sql.Statements, Is.Empty);
        }

        [Test]
        public async Task Reset_DeletesChildrenBeforeParents_InOneTransaction()
        {
            SeedTesterWithHospital();

            var r = await Reset();

            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.Hospitals, Is.EqualTo(new[] { "Test Hospital" }));
            Assert.That(_sql.Begun, Is.EqualTo(1));
            Assert.That(_sql.Committed, Is.EqualTo(1));
            Assert.That(_sql.RolledBack, Is.EqualTo(0));

            int At(string table) => _sql.Deleted.IndexOf(table);
            Assert.That(At("InvoiceItems"), Is.GreaterThanOrEqualTo(0), "a table with no hospital column is reached through its foreign key");
            Assert.That(At("InvoiceItems"), Is.LessThan(At("Invoices")));
            Assert.That(At("Invoices"), Is.LessThan(At("Hospitals")));
            Assert.That(At("HospitalUsers"), Is.LessThan(At("Hospitals")));
            Assert.That(At("Hospitals"), Is.LessThan(At("Users")));
            Assert.That(At("UserAuth"), Is.LessThan(At("Users")));
            Assert.That(At("Notes"), Is.GreaterThanOrEqualTo(0), "a table with a hospital column but no foreign key is cleared too");
            Assert.That(r.TotalRows, Is.EqualTo(1 + 3 + 7 + 1 + 2 + 1 + 1));
        }

        [Test]
        public async Task Reset_ReachesGrandchildrenThroughNestedExists_ScopedToTheTestersHospital()
        {
            var (_, hospital) = SeedTesterWithHospital();

            await Reset();

            var deleteItems = _sql.Statements.First(s => s.StartsWith("DELETE") && s.Contains("[InvoiceItems]"));
            Assert.That(deleteItems, Does.Contain("EXISTS (SELECT 1 FROM [dbo].[Invoices] AS p2 WHERE p2.[InvoiceId] = t2.[InvoiceId] AND EXISTS (SELECT 1 FROM [dbo].[Hospitals] AS p1 WHERE p1.[HospitalID] = p2.[HospitalId]"));
            Assert.That(deleteItems, Does.Contain("'" + hospital.HospitalID + "'"));
            Assert.That(_sql.Statements.Any(s => s.Contains("{") || s.Contains("}")), Is.False, "no braces: the statements go through ExecuteSqlRaw");
        }

        [Test]
        public async Task DryRun_ReportsCounts_AndDeletesNothing()
        {
            SeedTesterWithHospital();

            var r = await Reset(dryRun: true);

            Assert.That(r.Success, Is.True);
            Assert.That(r.DryRun, Is.True);
            Assert.That(_sql.Deleted, Is.Empty);
            Assert.That(_sql.Begun, Is.EqualTo(0));
            Assert.That(r.Tables.Single(t => t.Table == "dbo.invoiceitems").Rows, Is.EqualTo(7));
        }

        [Test]
        public async Task TesterWhoAlsoBelongsToSomeoneElsesHospital_IsRefused()
        {
            var (user, _) = SeedTesterWithHospital();
            var owner = TestDataFactory.SeedUser(_context, phone: "9876500000", email: "owner@example.com");
            var theirs = TestDataFactory.SeedHospital(_context, owner.UserID);
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = theirs.HospitalID, UserID = user.UserID });
            _context.SaveChanges();

            var r = await Reset();

            Assert.That(r.Success, Is.False);
            Assert.That(r.StatusCode, Is.EqualTo(409));
            Assert.That(_sql.Statements, Is.Empty);
        }

        [Test]
        public async Task StaffWhoBelongOnlyToTheTestersHospital_GoToo_ButSharedStaffStay()
        {
            var (_, hospital) = SeedTesterWithHospital();
            var onlyHere = TestDataFactory.SeedUser(_context, phone: "9000011111", email: "a@example.com");
            var alsoElsewhere = TestDataFactory.SeedUser(_context, phone: "9000022222", email: "b@example.com");
            var otherOwner = TestDataFactory.SeedUser(_context, phone: "9876500000", email: "owner@example.com");
            var otherHospital = TestDataFactory.SeedHospital(_context, otherOwner.UserID);
            _context.HospitalUsers.AddRange(
                new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospital.HospitalID, UserID = onlyHere.UserID },
                new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospital.HospitalID, UserID = alsoElsewhere.UserID },
                new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = otherHospital.HospitalID, UserID = alsoElsewhere.UserID });
            _context.SaveChanges();

            var r = await Reset();

            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.Users, Is.EqualTo(2));
            var deleteUsers = _sql.Statements.Last(s => s.StartsWith("DELETE") && s.Contains("[Users]"));
            Assert.That(deleteUsers, Does.Contain(onlyHere.UserID.ToString()));
            Assert.That(deleteUsers, Does.Not.Contain(alsoElsewhere.UserID.ToString()));
        }

        [Test]
        public async Task MoreRowsThanTheLimit_StopsAndRollsBack()
        {
            SeedTesterWithHospital();
            _sql.RowsIn["Invoices"] = 500;

            var r = await Reset(maxRows: "100");

            Assert.That(r.Success, Is.False);
            Assert.That(r.StatusCode, Is.EqualTo(409));
            Assert.That(r.Message, Does.Contain("dbo.invoices"));
            Assert.That(_sql.Committed, Is.EqualTo(0));
            Assert.That(_sql.RolledBack, Is.EqualTo(1));
        }

        [Test]
        public async Task DatabaseErrorPartWay_RollsBack_AndSaysNothingChanged()
        {
            SeedTesterWithHospital();
            _sql.FailOnDeleteOf = "Hospitals";

            var r = await Reset();

            Assert.That(r.Success, Is.False);
            Assert.That(r.StatusCode, Is.EqualTo(500));
            Assert.That(_sql.Committed, Is.EqualTo(0));
            Assert.That(_sql.RolledBack, Is.EqualTo(1));
        }
    }
}
