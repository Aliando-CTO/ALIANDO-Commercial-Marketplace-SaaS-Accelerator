// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Marketplace.SaaS.Accelerator.Services.Models;

namespace Marketplace.SaaS.Accelerator.Services.Contracts;

/// <summary>
/// Single entry point for submitting usage events to Marketplace Metering. Wraps validation,
/// the underlying <see cref="IMeteredBillingApiService"/> call, and audit-log persistence so
/// both the admin UI and external callers share one path.
/// </summary>
public interface IMeteringSubmissionService
{
    /// <summary>
    /// Validates and submits a single usage event. Always writes a MeteredAuditLogs row —
    /// including for validation rejections — so operators can trace every attempt.
    /// </summary>
    Task<MeteringSubmissionResult> SubmitUsageAsync(MeteringSubmission submission);

    /// <summary>
    /// Validates and submits a batch of usage events. Validation is per-submission; valid
    /// items are sent in one batched Marketplace call, invalid items are returned with a
    /// reject reason. Returned list order matches input order.
    /// </summary>
    Task<IReadOnlyList<MeteringSubmissionResult>> SubmitBatchAsync(IEnumerable<MeteringSubmission> submissions);
}
