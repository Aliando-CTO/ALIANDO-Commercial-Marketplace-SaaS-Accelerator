// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Marketplace.SaaS.Accelerator.DataAccess.Contracts;
using Marketplace.SaaS.Accelerator.DataAccess.Entities;
using Marketplace.SaaS.Accelerator.Services.Contracts;
using Marketplace.SaaS.Accelerator.Services.Exceptions;
using Marketplace.SaaS.Accelerator.Services.Models;
using Marketplace.SaaS.Accelerator.Services.Utilities;

namespace Marketplace.SaaS.Accelerator.Services.Services;

/// <summary>
/// Orchestrates: validate submission → call Marketplace Metering → persist MeteredAuditLogs.
/// All entry points to metering (admin UI, external API, future schedulers) should go through here.
/// </summary>
public class MeteringSubmissionService : IMeteringSubmissionService
{
    private const string AcceptedStatus = "Accepted";

    private readonly ISubscriptionsRepository subscriptionsRepository;
    private readonly IPlansRepository plansRepository;
    private readonly IMeteredDimensionsRepository dimensionsRepository;
    private readonly IMeteredBillingApiService billingApiService;
    private readonly ISubscriptionUsageLogsRepository usageLogsRepository;
    private readonly SaaSClientLogger<MeteringSubmissionService> logger;

    public MeteringSubmissionService(
        ISubscriptionsRepository subscriptionsRepository,
        IPlansRepository plansRepository,
        IMeteredDimensionsRepository dimensionsRepository,
        IMeteredBillingApiService billingApiService,
        ISubscriptionUsageLogsRepository usageLogsRepository,
        SaaSClientLogger<MeteringSubmissionService> logger)
    {
        this.subscriptionsRepository = subscriptionsRepository;
        this.plansRepository = plansRepository;
        this.dimensionsRepository = dimensionsRepository;
        this.billingApiService = billingApiService;
        this.usageLogsRepository = usageLogsRepository;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<MeteringSubmissionResult> SubmitUsageAsync(MeteringSubmission submission)
    {
        var (validationResult, subscription) = this.Validate(submission);
        if (validationResult != null)
        {
            this.WriteAuditLog(submission, subscription, request: null, response: null, validationResult);
            return validationResult;
        }

        var request = BuildRequest(submission, subscription);
        var requestJson = JsonSerializer.Serialize(request);
        MeteringUsageResult marketplaceResult = null;
        string responseJson = null;
        MeteringSubmissionResult result;

        try
        {
            marketplaceResult = await this.billingApiService.EmitUsageEventAsync(request).ConfigureAwait(false);
            responseJson = JsonSerializer.Serialize(marketplaceResult);
            result = BuildResultFromMarketplace(submission, marketplaceResult);
        }
        catch (MarketplaceException mex)
        {
            responseJson = JsonSerializer.Serialize(mex.MeteredBillingErrorDetail);
            result = new MeteringSubmissionResult
            {
                Accepted = false,
                Status = string.IsNullOrEmpty(mex.ErrorCode) ? nameof(MeteringSubmissionRejectReason.MarketplaceError) : mex.ErrorCode,
                ExternalRequestId = submission.ExternalRequestId,
                ErrorMessage = mex.Message,
            };
        }

        this.WriteAuditLog(submission, subscription, requestJson, responseJson, result);
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MeteringSubmissionResult>> SubmitBatchAsync(IEnumerable<MeteringSubmission> submissions)
    {
        var items = submissions?.ToList() ?? new List<MeteringSubmission>();
        var results = new MeteringSubmissionResult[items.Count];
        var validRequests = new List<MeteringUsageRequest>();
        var validIndexes = new List<int>();
        var validSubscriptions = new List<Subscriptions>();

        // Validate each submission independently; log rejections immediately.
        for (var i = 0; i < items.Count; i++)
        {
            var (validationResult, subscription) = this.Validate(items[i]);
            if (validationResult != null)
            {
                this.WriteAuditLog(items[i], subscription, request: null, response: null, validationResult);
                results[i] = validationResult;
                continue;
            }

            validRequests.Add(BuildRequest(items[i], subscription));
            validIndexes.Add(i);
            validSubscriptions.Add(subscription);
        }

        if (validRequests.Count == 0)
        {
            return results;
        }

        // Single batched call to Marketplace for all valid submissions.
        MeteringBatchUsageResult batchResult = null;
        Exception batchFailure = null;
        try
        {
            batchResult = await this.billingApiService.EmitBatchUsageEventAsync(validRequests).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            batchFailure = ex;
            this.logger.LogError($"Batch metering submission failed: {ex.Message}");
        }

        for (var k = 0; k < validIndexes.Count; k++)
        {
            var inputIndex = validIndexes[k];
            var submission = items[inputIndex];
            var subscription = validSubscriptions[k];
            var requestJson = JsonSerializer.Serialize(validRequests[k]);
            MeteringSubmissionResult result;
            string responseJson;

            if (batchFailure != null)
            {
                responseJson = JsonSerializer.Serialize(new { error = batchFailure.Message });
                result = new MeteringSubmissionResult
                {
                    Accepted = false,
                    Status = nameof(MeteringSubmissionRejectReason.MarketplaceError),
                    ExternalRequestId = submission.ExternalRequestId,
                    ErrorMessage = batchFailure.Message,
                };
            }
            else
            {
                var item = MatchBatchResult(batchResult, validRequests[k], k);
                responseJson = item is null ? null : JsonSerializer.Serialize(item);
                result = item is null
                    ? new MeteringSubmissionResult
                    {
                        Accepted = false,
                        Status = nameof(MeteringSubmissionRejectReason.MarketplaceError),
                        ExternalRequestId = submission.ExternalRequestId,
                        ErrorMessage = "Marketplace batch response missing entry for this submission.",
                    }
                    : BuildResultFromMarketplace(submission, item);
            }

            this.WriteAuditLog(submission, subscription, requestJson, responseJson, result);
            results[inputIndex] = result;
        }

        return results;
    }

    private (MeteringSubmissionResult rejection, Subscriptions subscription) Validate(MeteringSubmission submission)
    {
        if (submission is null)
        {
            return (Reject(null, MeteringSubmissionRejectReason.BadRequest, "Submission body is required."), null);
        }

        if (string.IsNullOrWhiteSpace(submission.Source))
        {
            return (Reject(submission, MeteringSubmissionRejectReason.BadRequest, $"{nameof(submission.Source)} is required."), null);
        }

        if (string.IsNullOrWhiteSpace(submission.ExternalRequestId))
        {
            return (Reject(submission, MeteringSubmissionRejectReason.BadRequest, $"{nameof(submission.ExternalRequestId)} is required."), null);
        }

        if (submission.SubscriptionId == Guid.Empty)
        {
            return (Reject(submission, MeteringSubmissionRejectReason.BadRequest, $"{nameof(submission.SubscriptionId)} is required."), null);
        }

        if (string.IsNullOrWhiteSpace(submission.Dimension))
        {
            return (Reject(submission, MeteringSubmissionRejectReason.BadRequest, $"{nameof(submission.Dimension)} is required."), null);
        }

        if (submission.Quantity <= 0)
        {
            return (Reject(submission, MeteringSubmissionRejectReason.BadRequest, $"{nameof(submission.Quantity)} must be greater than zero."), null);
        }

        var subscription = this.subscriptionsRepository.GetById(submission.SubscriptionId, isIncludeDeactvated: true);
        if (subscription is null)
        {
            return (Reject(submission, MeteringSubmissionRejectReason.SubscriptionNotFound, $"Subscription {submission.SubscriptionId} not found."), null);
        }

        if (!string.Equals(subscription.SubscriptionStatus, SubscriptionStatusEnum.Subscribed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return (Reject(submission, MeteringSubmissionRejectReason.SubscriptionNotActive, $"Subscription status is {subscription.SubscriptionStatus}, expected Subscribed."), subscription);
        }

        if (subscription.IsActive != true)
        {
            return (Reject(submission, MeteringSubmissionRejectReason.SubscriptionInactive, "Subscription is marked inactive locally."), subscription);
        }

        var plan = this.plansRepository.GetById(subscription.AmpplanId);
        if (plan is null)
        {
            return (Reject(submission, MeteringSubmissionRejectReason.PlanNotFound, $"Plan {subscription.AmpplanId} not found locally."), subscription);
        }

        if (plan.IsmeteringSupported != true)
        {
            return (Reject(submission, MeteringSubmissionRejectReason.PlanNotMetered, $"Plan {subscription.AmpplanId} does not support metering."), subscription);
        }

        var dimensions = this.dimensionsRepository.GetDimensionsByPlanId(subscription.AmpplanId);
        if (dimensions is null || !dimensions.Any(d => string.Equals(d.Dimension, submission.Dimension, StringComparison.OrdinalIgnoreCase)))
        {
            return (Reject(submission, MeteringSubmissionRejectReason.DimensionNotFound, $"Dimension '{submission.Dimension}' is not configured on plan {subscription.AmpplanId}."), subscription);
        }

        return (null, subscription);
    }

    private static MeteringUsageRequest BuildRequest(MeteringSubmission submission, Subscriptions subscription)
    {
        return new MeteringUsageRequest
        {
            ResourceId = submission.SubscriptionId,
            Dimension = submission.Dimension,
            Quantity = submission.Quantity,
            EffectiveStartTime = (submission.EffectiveStartTime ?? DateTime.UtcNow).ToUniversalTime(),
            // Plan id resolved server-side from the validated subscription rather than trusting caller input.
            PlanId = subscription.AmpplanId,
        };
    }

    private static MeteringSubmissionResult BuildResultFromMarketplace(MeteringSubmission submission, MeteringUsageResult marketplaceResult)
    {
        var accepted = string.Equals(marketplaceResult?.Status, AcceptedStatus, StringComparison.OrdinalIgnoreCase);
        return new MeteringSubmissionResult
        {
            Accepted = accepted,
            Status = marketplaceResult?.Status ?? nameof(MeteringSubmissionRejectReason.MarketplaceError),
            MarketplaceUsageEventId = accepted ? marketplaceResult.UsageEventId : (Guid?)null,
            ExternalRequestId = submission.ExternalRequestId,
            ErrorMessage = accepted ? null : marketplaceResult?.Status,
        };
    }

    private static MeteringUsageResult MatchBatchResult(MeteringBatchUsageResult batchResult, MeteringUsageRequest request, int positionalIndex)
    {
        if (batchResult?.Result is null)
        {
            return null;
        }

        var results = batchResult.Result as IList<ResultBatchUsageResult> ?? batchResult.Result.ToList();

        // Marketplace echoes resourceId/dimension/effectiveStartTime; match on those when possible.
        var match = results.FirstOrDefault(r =>
            r.ResourceId == request.ResourceId
            && string.Equals(r.Dimension, request.Dimension, StringComparison.OrdinalIgnoreCase)
            && r.UsagePostedDate == request.EffectiveStartTime);

        if (match != null)
        {
            return match;
        }

        // Fall back to positional alignment if the echo doesn't disambiguate.
        return positionalIndex < results.Count ? results[positionalIndex] : null;
    }

    private static MeteringSubmissionResult Reject(MeteringSubmission submission, MeteringSubmissionRejectReason reason, string detail)
    {
        return new MeteringSubmissionResult
        {
            Accepted = false,
            Status = reason.ToString(),
            ExternalRequestId = submission?.ExternalRequestId,
            ErrorMessage = detail,
        };
    }

    private void WriteAuditLog(MeteringSubmission submission, Subscriptions subscription, object request, object response, MeteringSubmissionResult result)
    {
        try
        {
            var requestJson = request switch
            {
                string s => WrapWithCorrelation(s, submission),
                null => JsonSerializer.Serialize(new { submission, note = "validation-only" }),
                _ => WrapWithCorrelation(JsonSerializer.Serialize(request), submission),
            };

            var responseJson = response switch
            {
                string s => s,
                null => JsonSerializer.Serialize(result),
                _ => JsonSerializer.Serialize(response),
            };

            this.usageLogsRepository.Save(new MeteredAuditLogs
            {
                SubscriptionId = subscription?.Id,
                RequestJson = requestJson,
                ResponseJson = responseJson,
                StatusCode = result.Status,
                // Source carries the full RunBy tag. Callers — admin UI passes "Manual",
                // external API controller prefixes its callers with "ExternalAPI-" so they can't spoof channel.
                RunBy = string.IsNullOrWhiteSpace(submission?.Source) ? "Unknown" : submission.Source,
                SubscriptionUsageDate = (submission?.EffectiveStartTime ?? DateTime.UtcNow).ToUniversalTime(),
                CreatedBy = 0,
                CreatedDate = DateTime.Now,

                // Denormalised reporting columns. Populated even on rejection so failed attempts
                // remain searchable by dimension/quantity/etc. in the activity view and CSV export.
                ExternalRequestId = submission?.ExternalRequestId,
                Dimension = submission?.Dimension,
                Quantity = submission?.Quantity,
                MarketplaceUsageEventId = result.MarketplaceUsageEventId,
            });
        }
        catch (Exception ex)
        {
            // An audit-log failure must not mask the Marketplace outcome the caller already received.
            this.logger.LogError($"Failed to persist MeteredAuditLogs for ExternalRequestId={submission?.ExternalRequestId}: {ex.Message}");
        }
    }

    private static string WrapWithCorrelation(string innerJson, MeteringSubmission submission)
    {
        return JsonSerializer.Serialize(new
        {
            externalRequestId = submission?.ExternalRequestId,
            source = submission?.Source,
            request = JsonSerializer.Deserialize<JsonElement>(innerJson),
        });
    }
}
