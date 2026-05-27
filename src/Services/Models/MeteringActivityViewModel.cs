// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using Marketplace.SaaS.Accelerator.DataAccess.Entities;

namespace Marketplace.SaaS.Accelerator.Services.Models;

/// <summary>
/// View model for the operator-facing "Metering Activity" page.
/// Carries both the filter state and the resulting page of rows.
/// </summary>
public class MeteringActivityViewModel
{
    /// <summary>Channel filter: empty = all, or "Manual" / "Scheduler" / "ExternalAPI".</summary>
    public string Channel { get; set; }

    /// <summary>Substring match within RunBy — typically the Source suffix (e.g. "BillingService-Prod").</summary>
    public string SourceContains { get; set; }

    /// <summary>Exact match on StatusCode (e.g. "Accepted", "Expired", "BadRequest").</summary>
    public string StatusCode { get; set; }

    /// <summary>Substring search of the caller's trace id within RequestJson.</summary>
    public string ExternalRequestId { get; set; }

    /// <summary>Inclusive lower bound on CreatedDate. Null = no lower bound.</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive upper bound on CreatedDate. Null = no upper bound.</summary>
    public DateTime? To { get; set; }

    /// <summary>1-based page index. Defaults to 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Page size; clamped server-side. Defaults to 50.</summary>
    public int PageSize { get; set; } = 50;

    /// <summary>Total matching rows for the current filters (unpaged).</summary>
    public int TotalCount { get; set; }

    /// <summary>Current page of results, newest first.</summary>
    public List<MeteredAuditLogs> Results { get; set; } = new();

    /// <summary>Total number of pages given <see cref="TotalCount"/> and <see cref="PageSize"/>.</summary>
    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Known channel filter values, including a no-filter option.</summary>
    public static IReadOnlyList<string> KnownChannels { get; } = new[] { "", "Manual", "Scheduler", "ExternalAPI" };

    /// <summary>Common status codes for the dropdown. Free-text search is also allowed via the text input.</summary>
    public static IReadOnlyList<string> CommonStatusCodes { get; } = new[]
    {
        "",
        "Accepted",
        "Expired",
        "Duplicate",
        "ResourceNotFound",
        "ResourceNotAuthorized",
        "InvalidToken",
        "BadRequest",
        "SubscriptionNotFound",
        "SubscriptionNotActive",
        "SubscriptionInactive",
        "PlanNotFound",
        "PlanNotMetered",
        "DimensionNotFound",
        "MarketplaceError",
    };
}
