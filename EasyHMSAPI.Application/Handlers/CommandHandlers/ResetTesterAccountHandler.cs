using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Wipes a tester number's account on a Development server so the same number can sign up again:
    /// the user, every hospital they registered, everything stored under those hospitals, and staff accounts
    /// that belonged only to those hospitals. See TesterAccounts and docs/TESTER-ACCOUNTS.md.
    ///
    /// How it finds "everything": tables with a HospitalId column are cleared for those hospitals, the tester's
    /// chains and user rows are cleared, and before any row is deleted the rows that point at it through a
    /// foreign key are deleted first (read from the database catalogue, so new tables need no change here).
    ///
    /// Safety: only on Development, only for a number in StaffAuth:TesterMobiles; refused when the tester also
    /// belongs to a hospital somebody else registered; one transaction (any failure undoes all of it); stops if a
    /// single table would lose more rows than StaffAuth:TesterResetMaxRows (default 20000); DryRun reports and changes nothing.
    /// </summary>
    public class ResetTesterAccountHandler : IRequestHandler<ResetTesterAccountRequestModel, ResetTesterAccountResponseModel>
    {
        public const int DefaultMaxRowsPerTable = 20000;
        private const int MaxDepth = 12;

        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ResetTesterAccountHandler> _logger;

        /// <summary>The database calls. Tests replace it; by default it runs on the app's own SQL Server connection.</summary>
        public ITesterResetSql Sql { get; set; }

        public ResetTesterAccountHandler(AppDbContext context, IConfiguration configuration, ILogger<ResetTesterAccountHandler> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            Sql = new TesterResetSql(context);
        }

        private sealed class TableRef
        {
            public string Schema = "dbo";
            public string Name = string.Empty;
            public string Key => (Schema + "." + Name).ToLowerInvariant();
            public string Quoted => "[" + Schema.Replace("]", "]]") + "].[" + Name.Replace("]", "]]") + "]";
        }

        private sealed class ForeignKey
        {
            public TableRef Child = new();
            public TableRef Parent = new();
            public List<(string ChildColumn, string ParentColumn)> Columns = new();
        }

        private sealed class ResetAbortedException : Exception
        {
            public ResetAbortedException(string message) : base(message) { }
        }

        private sealed class Run
        {
            public bool DryRun;
            public int MaxRows;
            public Dictionary<string, List<ForeignKey>> ChildrenOf = new();
            public Dictionary<string, int> Rows = new();
            public List<string> Order = new();
        }

        public async Task<ResetTesterAccountResponseModel> Handle(ResetTesterAccountRequestModel request, CancellationToken cancellationToken)
        {
            var response = new ResetTesterAccountResponseModel { DryRun = request.DryRun };

            if (!TesterAccounts.IsDevelopment(_configuration))
                return Fail(response, 404, "Not found.");

            var mobile = TesterAccounts.Normalize(request.MobileNumber);
            if (mobile.Length != 10)
                return Fail(response, 400, "A 10-digit mobile number is required.");
            if (!TesterAccounts.IsTester(_configuration, mobile))
                return Fail(response, 400, "This number is not a tester number (StaffAuth:TesterMobiles). Only tester numbers can be reset.");

            var user = await _context.Users.AsNoTracking()
                .Where(u => u.MobileNumber == mobile)
                .Select(u => new { u.UserID })
                .FirstOrDefaultAsync(cancellationToken);
            if (user == null)
            {
                response.Success = true;
                response.AccountFound = false;
                response.Message = "This number has no account. It is free to sign up.";
                return response;
            }
            response.AccountFound = true;

            // The hospitals this tester registered: they go, with everything under them.
            var hospitals = await _context.Hospitals.AsNoTracking()
                .Where(h => h.CreatedByUserID == user.UserID)
                .Select(h => new { h.HospitalID, h.Name })
                .ToListAsync(cancellationToken);
            var hospitalIds = hospitals.Select(h => h.HospitalID).ToList();
            response.Hospitals = hospitals.Select(h => h.Name).ToList();

            // A tester who was also added to somebody else's hospital is not reset: deleting the user would reach into that hospital.
            var foreignHospitalIds = await _context.HospitalUsers.AsNoTracking()
                .Where(hu => hu.UserID == user.UserID && !hospitalIds.Contains(hu.HospitalID))
                .Select(hu => hu.HospitalID)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (foreignHospitalIds.Count > 0)
            {
                var names = await _context.Hospitals.AsNoTracking()
                    .Where(h => foreignHospitalIds.Contains(h.HospitalID))
                    .Select(h => h.Name)
                    .ToListAsync(cancellationToken);
                return Fail(response, 409,
                    "This tester also belongs to a hospital it did not register (" + string.Join(", ", names) +
                    "). Remove it from that hospital first; nothing was changed.");
            }

            // Staff who belong only to those hospitals go too, so their numbers are free again as well.
            var userIds = new List<Guid> { user.UserID };
            if (hospitalIds.Count > 0)
            {
                var memberIds = await _context.HospitalUsers.AsNoTracking()
                    .Where(hu => hospitalIds.Contains(hu.HospitalID) && hu.UserID != user.UserID)
                    .Select(hu => hu.UserID)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                if (memberIds.Count > 0)
                {
                    var alsoElsewhere = await _context.HospitalUsers.AsNoTracking()
                        .Where(hu => memberIds.Contains(hu.UserID) && !hospitalIds.Contains(hu.HospitalID))
                        .Select(hu => hu.UserID)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                    var ownsAnother = await _context.Hospitals.AsNoTracking()
                        .Where(h => memberIds.Contains(h.CreatedByUserID) && !hospitalIds.Contains(h.HospitalID))
                        .Select(h => h.CreatedByUserID)
                        .Distinct()
                        .ToListAsync(cancellationToken);
                    userIds.AddRange(memberIds.Where(id => !alsoElsewhere.Contains(id) && !ownsAnother.Contains(id)));
                }
            }
            response.Users = userIds.Count;

            var run = new Run
            {
                DryRun = request.DryRun,
                MaxRows = int.TryParse(_configuration["StaffAuth:TesterResetMaxRows"], out var cap) && cap > 0 ? cap : DefaultMaxRowsPerTable,
            };

            try
            {
                var foreignKeyColumns = await Sql.ForeignKeysAsync(cancellationToken);
                var hospitalTables = hospitalIds.Count > 0
                    ? await Sql.HospitalTablesAsync(cancellationToken)
                    : new List<TesterResetTableColumn>();
                run.ChildrenOf = GroupForeignKeys(foreignKeyColumns);

                // Schema of a table we name ourselves, taken from the catalogue when it is known there.
                TableRef Known(string name)
                {
                    var hit = foreignKeyColumns
                        .Where(f => string.Equals(f.ParentTable, name, StringComparison.OrdinalIgnoreCase))
                        .Select(f => new TableRef { Schema = f.ParentSchema, Name = f.ParentTable })
                        .FirstOrDefault()
                        ?? foreignKeyColumns
                        .Where(f => string.Equals(f.ChildTable, name, StringComparison.OrdinalIgnoreCase))
                        .Select(f => new TableRef { Schema = f.ChildSchema, Name = f.ChildTable })
                        .FirstOrDefault();
                    return hit ?? new TableRef { Schema = "dbo", Name = name };
                }

                var hospitalList = GuidList(hospitalIds);
                var userList = GuidList(userIds);

                if (!run.DryRun) await Sql.BeginAsync(cancellationToken);

                // 1. Everything stored against the hospitals (the Hospitals table is one of these, by its own id column).
                foreach (var t in hospitalTables.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName))
                {
                    var table = new TableRef { Schema = t.SchemaName, Name = t.TableName };
                    var column = Bracket(t.ColumnName);
                    await VisitAsync(run, table, alias => alias + "." + column + " IN (" + hospitalList + ")", new HashSet<string>(), 0, cancellationToken);
                }

                // 2. Chains the accounts own.
                await VisitAsync(run, Known("HospitalChains"), alias => alias + ".[OwnerUserId] IN (" + userList + ")", new HashSet<string>(), 0, cancellationToken);

                // 3. The accounts themselves (login, profile, roles, memberships follow through their foreign keys).
                await VisitAsync(run, Known("Users"), alias => alias + ".[UserID] IN (" + userList + ")", new HashSet<string>(), 0, cancellationToken);

                if (!run.DryRun) await Sql.CommitAsync(cancellationToken);
            }
            catch (ResetAbortedException ex)
            {
                if (!run.DryRun) await SafeRollbackAsync(cancellationToken);
                return Fail(response, 409, ex.Message + " Nothing was changed.");
            }
            catch (Exception ex)
            {
                if (!run.DryRun) await SafeRollbackAsync(cancellationToken);
                _logger.LogError(ex, "Tester reset failed for a tester number; rolled back");
                return Fail(response, 500, "The reset failed and was rolled back, nothing was changed: " + ex.GetBaseException().Message);
            }

            response.Tables = run.Order
                .Select(key => new TesterResetTableCount { Table = key, Rows = run.Rows[key] })
                .Where(t => t.Rows > 0)
                .ToList();
            response.TotalRows = response.Tables.Sum(t => t.Rows);
            response.Success = true;
            response.Message = run.DryRun
                ? "Dry run: this is what a reset would delete. Nothing was changed."
                : "The tester account was reset. The number is free to sign up again.";
            if (!run.DryRun)
            {
                _context.ChangeTracker.Clear();
                _logger.LogWarning("Tester account reset: {Hospitals} hospital(s), {Users} user(s), {Rows} row(s) deleted",
                    hospitalIds.Count, userIds.Count, response.TotalRows);
            }
            return response;
        }

        /// <summary>
        /// Deletes the rows of <paramref name="table"/> matching <paramref name="predicate"/>, after deleting the rows that
        /// point at them through a foreign key (children first, to any depth). A table with nothing to delete costs one COUNT.
        /// </summary>
        private async Task VisitAsync(Run run, TableRef table, Func<string, string> predicate, HashSet<string> path, int depth, CancellationToken cancellationToken)
        {
            var alias = "t" + depth;
            var where = predicate(alias);
            var count = await Sql.CountAsync("SELECT COUNT(*) AS [Value] FROM " + table.Quoted + " AS " + alias + " WHERE " + where, cancellationToken);
            if (count == 0) return;

            if (count > run.MaxRows)
                throw new ResetAbortedException("Stopped: " + table.Key + " would lose " + count + " rows, more than the limit of " + run.MaxRows + " (StaffAuth:TesterResetMaxRows).");
            if (depth >= MaxDepth)
                throw new ResetAbortedException("Stopped: the chain of linked tables under " + table.Key + " is deeper than " + MaxDepth + ".");

            if (run.ChildrenOf.TryGetValue(table.Key, out var foreignKeys))
            {
                var nextPath = new HashSet<string>(path) { table.Key };
                foreach (var fk in foreignKeys)
                {
                    // A table already being cleared further up (or itself): its rows go with the statement that started it.
                    if (nextPath.Contains(fk.Child.Key)) continue;

                    var parentAlias = "p" + (depth + 1);
                    var join = string.Join(" AND ", fk.Columns.Select(c => parentAlias + "." + Bracket(c.ParentColumn) + " = {0}." + Bracket(c.ChildColumn)));
                    var parentWhere = predicate(parentAlias);
                    var parentTable = table.Quoted;
                    Func<string, string> childPredicate = childAlias =>
                        "EXISTS (SELECT 1 FROM " + parentTable + " AS " + parentAlias + " WHERE " +
                        join.Replace("{0}", childAlias) + " AND " + parentWhere + ")";

                    await VisitAsync(run, fk.Child, childPredicate, nextPath, depth + 1, cancellationToken);
                }
            }

            var rows = run.DryRun
                ? count
                : await Sql.ExecuteAsync("DELETE " + alias + " FROM " + table.Quoted + " AS " + alias + " WHERE " + where, cancellationToken);

            if (!run.Rows.ContainsKey(table.Key))
            {
                run.Rows[table.Key] = 0;
                run.Order.Add(table.Key);
            }
            // A dry run can meet the same rows by two routes; report the largest sighting, not the sum.
            run.Rows[table.Key] = run.DryRun ? Math.Max(run.Rows[table.Key], rows) : run.Rows[table.Key] + rows;
        }

        private static Dictionary<string, List<ForeignKey>> GroupForeignKeys(List<TesterResetForeignKeyColumn> columns)
        {
            var result = new Dictionary<string, List<ForeignKey>>();
            var groups = columns.GroupBy(c => (c.ChildSchema + "." + c.ChildTable + "." + c.FkName).ToLowerInvariant());
            foreach (var group in groups)
            {
                var first = group.First();
                var fk = new ForeignKey
                {
                    Child = new TableRef { Schema = first.ChildSchema, Name = first.ChildTable },
                    Parent = new TableRef { Schema = first.ParentSchema, Name = first.ParentTable },
                    Columns = group.OrderBy(c => c.Ordinal).Select(c => (c.ChildColumn, c.ParentColumn)).ToList(),
                };
                if (!result.TryGetValue(fk.Parent.Key, out var list))
                {
                    list = new List<ForeignKey>();
                    result[fk.Parent.Key] = list;
                }
                list.Add(fk);
            }
            foreach (var list in result.Values)
                list.Sort((a, b) => string.CompareOrdinal(a.Child.Key, b.Child.Key));
            return result;
        }

        private async Task SafeRollbackAsync(CancellationToken cancellationToken)
        {
            try { await Sql.RollbackAsync(cancellationToken); }
            catch (Exception ex) { _logger.LogError(ex, "Tester reset: rollback failed"); }
        }

        private static string Bracket(string name) => "[" + name.Replace("]", "]]") + "]";

        // Guid.ToString() is hex digits and dashes only, so the list is safe to place in the statement.
        private static string GuidList(IEnumerable<Guid> ids)
        {
            var list = string.Join(", ", ids.Select(id => "'" + id.ToString() + "'"));
            return list.Length == 0 ? "NULL" : list;
        }

        private static ResetTesterAccountResponseModel Fail(ResetTesterAccountResponseModel response, int statusCode, string message)
        {
            response.Success = false;
            response.StatusCode = statusCode;
            response.Message = message;
            return response;
        }
    }
}
