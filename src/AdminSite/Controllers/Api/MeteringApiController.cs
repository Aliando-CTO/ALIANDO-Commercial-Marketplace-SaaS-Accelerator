// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marketplace.SaaS.Accelerator.AdminSite.Filters;
using Marketplace.SaaS.Accelerator.Services.Contracts;
using Marketplace.SaaS.Accelerator.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.SaaS.Accelerator.AdminSite.Controllers.Api;

/// <summary>
/// External billing API. Lets a service outside the accelerator submit usage events that
/// get emitted to the Marketplace Metering Service. All requests are auth-gated by
/// <see cref="ExternalApiAuthAttribute"/> and audited to MeteredAuditLogs with
/// RunBy = "ExternalAPI-{Source}" so operators can trace which caller submitted what.
/// </summary>
[Route("api/metering")]
[ApiController]
[IgnoreAntiforgeryToken]
[ExternalApiAuth]
public class MeteringApiController : ControllerBase
{
    /// <summary>Prefix applied to the caller-supplied Source so the channel can't be spoofed.</summary>
    private const string ExternalSourcePrefix = "ExternalAPI-";

    private readonly IMeteringSubmissionService submissionService;

    public MeteringApiController(IMeteringSubmissionService submissionService)
    {
        this.submissionService = submissionService;
    }

    /// <summary>
    /// Submit a single usage event.
    /// </summary>
    [HttpPost("usage")]
    public async Task<ActionResult<MeteringSubmissionResult>> SubmitUsage([FromBody] MeteringSubmission submission)
    {
        if (submission is null)
        {
            return BadRequest(new MeteringSubmissionResult
            {
                Accepted = false,
                Status = nameof(MeteringSubmissionRejectReason.BadRequest),
                ErrorMessage = "Request body is required.",
            });
        }

        ApplyChannelPrefix(submission);
        var result = await this.submissionService.SubmitUsageAsync(submission).ConfigureAwait(false);
        return result.Accepted ? Ok(result) : MapRejection(result);
    }

    /// <summary>
    /// Submit a batch of usage events. Returns per-submission results in input order.
    /// HTTP 200 if any succeeded, 400 if all failed validation, 502 if Marketplace itself failed.
    /// </summary>
    [HttpPost("usage/batch")]
    public async Task<ActionResult<IReadOnlyList<MeteringSubmissionResult>>> SubmitBatch([FromBody] List<MeteringSubmission> submissions)
    {
        if (submissions is null || submissions.Count == 0)
        {
            return BadRequest(new[]
            {
                new MeteringSubmissionResult
                {
                    Accepted = false,
                    Status = nameof(MeteringSubmissionRejectReason.BadRequest),
                    ErrorMessage = "Request body must contain at least one submission.",
                },
            });
        }

        foreach (var s in submissions)
        {
            ApplyChannelPrefix(s);
        }

        var results = await this.submissionService.SubmitBatchAsync(submissions).ConfigureAwait(false);

        if (results.Any(r => r.Accepted))
        {
            return Ok(results);
        }

        // None accepted — distinguish between "all rejected by validation" and "Marketplace failed".
        var anyMarketplaceFailure = results.Any(r => r.Status == nameof(MeteringSubmissionRejectReason.MarketplaceError));
        return anyMarketplaceFailure
            ? StatusCode(502, results)
            : BadRequest(results);
    }

    /// <summary>
    /// Tag the caller-supplied Source with the immutable "ExternalAPI-" prefix so the audit
    /// log channel can't be spoofed (e.g. caller setting Source="Manual" or "Scheduler-X").
    /// Empty source is left empty; the service rejects with BadRequest.
    /// </summary>
    private static void ApplyChannelPrefix(MeteringSubmission submission)
    {
        if (submission is null || string.IsNullOrWhiteSpace(submission.Source))
        {
            return;
        }

        if (!submission.Source.StartsWith(ExternalSourcePrefix, System.StringComparison.Ordinal))
        {
            submission.Source = ExternalSourcePrefix + submission.Source;
        }
    }

    /// <summary>
    /// Map a single-submission rejection to the closest HTTP status. Validation errors are 4xx;
    /// Marketplace failures pass through as 502 since the caller's request was well-formed.
    /// </summary>
    private static ActionResult<MeteringSubmissionResult> MapRejection(MeteringSubmissionResult result)
    {
        return result.Status switch
        {
            nameof(MeteringSubmissionRejectReason.SubscriptionNotFound) => new NotFoundObjectResult(result),
            nameof(MeteringSubmissionRejectReason.SubscriptionNotActive)
                or nameof(MeteringSubmissionRejectReason.SubscriptionInactive)
                or nameof(MeteringSubmissionRejectReason.PlanNotFound)
                or nameof(MeteringSubmissionRejectReason.PlanNotMetered)
                or nameof(MeteringSubmissionRejectReason.DimensionNotFound) => new ConflictObjectResult(result),
            nameof(MeteringSubmissionRejectReason.MarketplaceError) => new ObjectResult(result) { StatusCode = 502 },
            _ => new BadRequestObjectResult(result),
        };
    }
}
