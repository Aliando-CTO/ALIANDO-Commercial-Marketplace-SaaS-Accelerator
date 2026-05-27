// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Marketplace.SaaS.Accelerator.DataAccess.Contracts;
using Marketplace.SaaS.Accelerator.Services.Models;
using Marketplace.SaaS.Accelerator.Services.Services;
using Marketplace.SaaS.Accelerator.Services.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.SaaS.Accelerator.AdminSite.Controllers;

/// <summary>
/// Operator view onto MeteredAuditLogs. Provides filtering by channel, status, date,
/// caller source, and ExternalRequestId so that submissions from the Manual UI, the
/// Scheduler, and the External API can all be inspected from one place.
/// </summary>
[ServiceFilter(typeof(KnownUserAttribute))]
public class MeteringActivityController : BaseController
{
    private const int MinPageSize = 10;
    private const int MaxPageSize = 200;

    // Export hard ceiling. Anyone needing more should narrow the date range or call the DB directly.
    private const int ExportMaxRows = 100_000;

    private readonly ISubscriptionUsageLogsRepository usageLogsRepository;
    private readonly SaaSClientLogger<MeteringActivityController> logger;

    public MeteringActivityController(
        ISubscriptionUsageLogsRepository usageLogsRepository,
        IApplicationConfigRepository applicationConfigRepository,
        IAppVersionService appVersionService,
        SaaSClientLogger<MeteringActivityController> logger)
        : base(applicationConfigRepository, appVersionService)
    {
        this.usageLogsRepository = usageLogsRepository;
        this.logger = logger;
    }

    /// <summary>
    /// Renders the activity page. Filters are bound from the query string so the URL is shareable.
    /// </summary>
    public IActionResult Index(MeteringActivityViewModel filter)
    {
        try
        {
            filter ??= new MeteringActivityViewModel();

            // Clamp paging to safe bounds; never trust the query string blindly.
            if (filter.Page < 1) filter.Page = 1;
            if (filter.PageSize < MinPageSize) filter.PageSize = MinPageSize;
            if (filter.PageSize > MaxPageSize) filter.PageSize = MaxPageSize;

            var skip = (filter.Page - 1) * filter.PageSize;

            // Channel maps to a RunBy prefix in the audit log table.
            // - "Manual"     matches "Manual"
            // - "Scheduler"  matches "Scheduler - <name>"
            // - "ExternalAPI" matches "ExternalAPI-<source>"
            var runByPrefix = string.IsNullOrWhiteSpace(filter.Channel) ? null : filter.Channel;

            var rows = this.usageLogsRepository.QueryMeteredAuditLogs(
                runByPrefix: runByPrefix,
                runByContains: filter.SourceContains,
                statusCode: filter.StatusCode,
                externalRequestIdContains: filter.ExternalRequestId,
                from: filter.From,
                to: filter.To,
                skip: skip,
                take: filter.PageSize,
                out var totalCount);

            filter.Results = rows;
            filter.TotalCount = totalCount;
            return this.View(filter);
        }
        catch (Exception ex)
        {
            this.logger.LogError($"MeteringActivity / Index error: {ex.Message} :: {ex.InnerException}");
            return this.View("Error", ex);
        }
    }

    /// <summary>
    /// CSV export of metering submissions. Reuses the same filters as Index. Defaults to
    /// Accepted-only rows (true billing data); pass <paramref name="includeRejected"/>=true
    /// to include validation rejections and Marketplace failures for investigation.
    /// </summary>
    /// <remarks>
    /// Capped at <see cref="ExportMaxRows"/> rows per call. For larger exports, narrow the
    /// date range or run a direct DB query against the new indexed columns.
    /// </remarks>
    public IActionResult ExportCsv(MeteringActivityViewModel filter, bool includeRejected = false)
    {
        try
        {
            filter ??= new MeteringActivityViewModel();

            // For exports, force StatusCode=Accepted unless the operator opted in to rejections.
            // If the operator typed a specific StatusCode value, honor it (they're investigating).
            if (!includeRejected && string.IsNullOrWhiteSpace(filter.StatusCode))
            {
                filter.StatusCode = "Accepted";
            }

            var runByPrefix = string.IsNullOrWhiteSpace(filter.Channel) ? null : filter.Channel;

            var rows = this.usageLogsRepository.QueryMeteredAuditLogs(
                runByPrefix: runByPrefix,
                runByContains: filter.SourceContains,
                statusCode: filter.StatusCode,
                externalRequestIdContains: filter.ExternalRequestId,
                from: filter.From,
                to: filter.To,
                skip: 0,
                take: ExportMaxRows,
                out var totalCount);

            var csv = BuildCsv(rows, totalCount);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv; charset=utf-8", BuildFilename(filter, includeRejected));
        }
        catch (Exception ex)
        {
            this.logger.LogError($"MeteringActivity / ExportCsv error: {ex.Message} :: {ex.InnerException}");
            return this.View("Error", ex);
        }
    }

    private static string BuildCsv(System.Collections.Generic.List<DataAccess.Entities.MeteredAuditLogs> rows, int totalCount)
    {
        var sb = new StringBuilder(capacity: rows.Count * 200);

        // Header row. Order chosen for human-readable billing reports.
        sb.AppendLine("CreatedDate,SubscriptionUsageDate,SubscriptionName,AmpSubscriptionId,AmpPlanId,Dimension,Quantity,StatusCode,RunBy,ExternalRequestId,MarketplaceUsageEventId");

        foreach (var r in rows)
        {
            sb.Append(CsvField(r.CreatedDate?.ToString("u", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(CsvField(r.SubscriptionUsageDate?.ToString("u", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(CsvField(r.Subscription?.Name)).Append(',');
            sb.Append(CsvField(r.Subscription?.AmpsubscriptionId.ToString())).Append(',');
            sb.Append(CsvField(r.Subscription?.AmpplanId)).Append(',');
            sb.Append(CsvField(r.Dimension)).Append(',');
            sb.Append(CsvField(r.Quantity?.ToString(CultureInfo.InvariantCulture))).Append(',');
            sb.Append(CsvField(r.StatusCode)).Append(',');
            sb.Append(CsvField(r.RunBy)).Append(',');
            sb.Append(CsvField(r.ExternalRequestId)).Append(',');
            sb.Append(CsvField(r.MarketplaceUsageEventId?.ToString()));
            sb.AppendLine();
        }

        // Trailing comment line is technically off-spec for strict RFC 4180, but Excel ignores
        // it and operators get a quick visual check that the export wasn't truncated silently.
        if (rows.Count >= ExportMaxRows && totalCount > rows.Count)
        {
            sb.AppendLine();
            sb.AppendLine($"# truncated: {rows.Count} of {totalCount} rows exported (cap is {ExportMaxRows})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes a single CSV field per RFC 4180: wrap in quotes if the value contains comma,
    /// quote, CR, or LF; double up any embedded quotes. Null/empty becomes an empty field.
    /// </summary>
    private static string CsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool needsQuoting =
            value.IndexOf(',') >= 0 ||
            value.IndexOf('"') >= 0 ||
            value.IndexOf('\n') >= 0 ||
            value.IndexOf('\r') >= 0;

        if (!needsQuoting)
        {
            return value;
        }

        return string.Concat("\"", value.Replace("\"", "\"\""), "\"");
    }

    private static string BuildFilename(MeteringActivityViewModel filter, bool includeRejected)
    {
        var fromTag = filter.From?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "all";
        var toTag = filter.To?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "now";
        var scopeTag = includeRejected ? "all" : "accepted";
        return $"metering-{scopeTag}-{fromTag}-to-{toTag}.csv";
    }
}
