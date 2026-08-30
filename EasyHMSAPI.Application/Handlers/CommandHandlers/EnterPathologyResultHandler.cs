using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class EnterPathologyResultHandler : IRequestHandler<EnterPathologyResultCommand, bool>
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly AppDbContext _context;

        public EnterPathologyResultHandler(AppDbContext context)
        {
            _context = context;
        }

        private class ParameterSchemaWrapper
        {
            public List<ParameterSchemaItem> Params { get; set; } = new();
        }

        private class ParameterSchemaItem
        {
            public string Name { get; set; } = "";
            public decimal? MaleMin { get; set; }
            public decimal? MaleMax { get; set; }
            public decimal? FemaleMin { get; set; }
            public decimal? FemaleMax { get; set; }
            public decimal? ChildMin { get; set; }
            public decimal? ChildMax { get; set; }
            public decimal? CriticalLow { get; set; }
            public decimal? CriticalHigh { get; set; }
        }

        private class ResultValueEntry
        {
            [JsonPropertyName("value")]
            public string Value { get; set; } = "";
            [JsonPropertyName("flag")]
            public string Flag { get; set; } = nameof(PathologyResultFlag.NORMAL);
        }

        public async Task<bool> Handle(EnterPathologyResultCommand request, CancellationToken cancellationToken)
        {
            var line = await _context.PathologyOrderLine
                .Where(l => l.HospitalId == request.HospitalId && l.OrderId == request.OrderId && l.OrderLineId == request.OrderLineId)
                .FirstOrDefaultAsync(cancellationToken);

            if (line == null)
            {
                return false;
            }

            var (enrichedJson, hasCritical) = await ComputeFlaggedResultValuesAsync(
                request.HospitalId, request.OrderId, line.TestId, request.ResultValuesJson, cancellationToken);

            var result = await _context.PathologyResult
                .Where(r => r.OrderLineId == line.OrderLineId)
                .FirstOrDefaultAsync(cancellationToken);

            if (result == null)
            {
                result = new PathologyResult
                {
                    ResultId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    OrderLineId = line.OrderLineId,
                    ResultValuesJson = enrichedJson,
                    HasCriticalFlag = hasCritical,
                    Interpretation = request.Interpretation,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserId.ToString()
                };
                _context.PathologyResult.Add(result);
            }
            else
            {
                result.ResultValuesJson = enrichedJson;
                result.HasCriticalFlag = hasCritical;
                result.Interpretation = request.Interpretation;
                result.UpdatedAt = DateTime.UtcNow;
                result.UpdatedBy = request.LoggedInUserId.ToString();
                _context.PathologyResult.Update(result);
            }

            // Update line status
            if (line.Status == "PENDING" || line.Status == "SAMPLE_COLLECTED")
            {
                line.Status = "RESULT_ENTERED";
                line.UpdatedAt = DateTime.UtcNow;
                line.UpdatedBy = request.LoggedInUserId.ToString();
                _context.PathologyOrderLine.Update(line);
            }

            // Check if all lines are completed to update order status
            var allLines = await _context.PathologyOrderLine
                .Where(l => l.OrderId == request.OrderId)
                .ToListAsync(cancellationToken);
                
            var order = await _context.PathologyOrder
                .Where(o => o.OrderId == request.OrderId)
                .FirstOrDefaultAsync(cancellationToken);

            if (order != null)
            {
                bool allDone = allLines.All(l => l.Status == "RESULT_ENTERED" || l.Status == "REPORT_APPROVED" || (l.OrderLineId == line.OrderLineId && line.Status == "RESULT_ENTERED"));
                if (allDone && order.Status != "COMPLETED")
                {
                    order.Status = "COMPLETED"; // Or leave as IN_PROGRESS until report is approved depending on business rules
                    order.UpdatedAt = DateTime.UtcNow;
                    order.UpdatedBy = request.LoggedInUserId.ToString();
                    _context.PathologyOrder.Update(order);
                }
                else if (order.Status == "PLACED")
                {
                    order.Status = "IN_PROGRESS";
                    order.UpdatedAt = DateTime.UtcNow;
                    order.UpdatedBy = request.LoggedInUserId.ToString();
                    _context.PathologyOrder.Update(order);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>Re-derives {value, flag} for every entered parameter server-side -- the client
        /// may show its own live preview as the technician types, but the persisted flag always
        /// comes from this recomputation, never from whatever the client submitted.</summary>
        private async Task<(string Json, bool HasCritical)> ComputeFlaggedResultValuesAsync(
            Guid hospitalId, Guid orderId, Guid testId, string rawResultValuesJson, CancellationToken cancellationToken)
        {
            Dictionary<string, string>? rawValues;
            try
            {
                rawValues = JsonSerializer.Deserialize<Dictionary<string, string>>(rawResultValuesJson, JsonOptions);
            }
            catch (JsonException)
            {
                rawValues = null;
            }
            if (rawValues == null || rawValues.Count == 0)
            {
                return (rawResultValuesJson, false);
            }

            var schemaJson = await _context.PathologyTestMaster
                .Where(t => t.HospitalId == hospitalId && t.TestId == testId)
                .Select(t => t.ParameterSchemaJson)
                .FirstOrDefaultAsync(cancellationToken);

            var patientId = await _context.PathologyOrder
                .Where(o => o.HospitalId == hospitalId && o.OrderId == orderId)
                .Select(o => o.PatientId)
                .FirstOrDefaultAsync(cancellationToken);

            DateTime? dob = null;
            string? gender = null;
            if (patientId != null)
            {
                var patient = await _context.PatientRegistrations
                    .Where(p => p.PatientId == patientId)
                    .Select(p => new { p.DateOfBirth, p.Sex })
                    .FirstOrDefaultAsync(cancellationToken);
                dob = patient?.DateOfBirth;
                gender = patient?.Sex;
            }
            var patientAge = PathologyAgeCalculator.CalculateAgeYears(dob);

            List<ParameterSchemaItem> parameters;
            try
            {
                parameters = string.IsNullOrWhiteSpace(schemaJson)
                    ? new List<ParameterSchemaItem>()
                    : JsonSerializer.Deserialize<ParameterSchemaWrapper>(schemaJson, JsonOptions)?.Params ?? new List<ParameterSchemaItem>();
            }
            catch (JsonException)
            {
                parameters = new List<ParameterSchemaItem>();
            }
            var parametersByName = parameters
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var enriched = new Dictionary<string, ResultValueEntry>();
            var hasCritical = false;
            foreach (var (paramName, rawValue) in rawValues)
            {
                var flag = PathologyResultFlag.NORMAL;
                if (!string.IsNullOrWhiteSpace(rawValue) && parametersByName.TryGetValue(paramName, out var schema))
                {
                    var range = new PathologyParameterRange(
                        schema.Name, null, null,
                        schema.MaleMin, schema.MaleMax,
                        schema.FemaleMin, schema.FemaleMax,
                        schema.ChildMin, schema.ChildMax,
                        schema.CriticalLow, schema.CriticalHigh,
                        0);
                    flag = PathologyResultFlagCalculator.Evaluate(range, rawValue, patientAge, gender);
                }

                enriched[paramName] = new ResultValueEntry { Value = rawValue, Flag = flag.ToString() };
                if (flag is PathologyResultFlag.CRITICAL_HIGH or PathologyResultFlag.CRITICAL_LOW)
                {
                    hasCritical = true;
                }
            }

            return (JsonSerializer.Serialize(enriched), hasCritical);
        }
    }
}
