// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace Marketplace.SaaS.Accelerator.Services.Models;

/// <summary>
/// External-caller payload for submitting a single usage event. The accelerator validates the
/// submission against local subscription/plan/dimension state, emits to the Marketplace Metering
/// Service, and writes a MeteredAuditLogs row tagged with <see cref="Source"/>.
/// </summary>
public sealed class MeteringSubmission
{
    /// <summary>The SaaS subscription identifier (Marketplace Guid, stored as AmpsubscriptionId locally).</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>The metered dimension name as published in the plan.</summary>
    public string Dimension { get; set; }

    /// <summary>The usage quantity for this event. Must be positive.</summary>
    public double Quantity { get; set; }

    /// <summary>UTC time the usage occurred. Defaults to now when null.</summary>
    public DateTime? EffectiveStartTime { get; set; }

    /// <summary>Caller-supplied trace id, persisted in the audit log RequestJson for correlation. Required.</summary>
    public string ExternalRequestId { get; set; }

    /// <summary>Identifier of the calling service, used as the RunBy tag on the audit log. Required.</summary>
    public string Source { get; set; }
}

/// <summary>
/// Outcome of a single submission. Returned to callers and persisted in the audit log.
/// </summary>
public sealed class MeteringSubmissionResult
{
    /// <summary>True only if the Marketplace Metering Service accepted the event.</summary>
    public bool Accepted { get; set; }

    /// <summary>
    /// Outcome tag. "Accepted" on success; otherwise either a reject reason from
    /// <see cref="MeteringSubmissionRejectReason"/> or a Marketplace status/error code.
    /// </summary>
    public string Status { get; set; }

    /// <summary>Marketplace-assigned event id. Null when not accepted.</summary>
    public Guid? MarketplaceUsageEventId { get; set; }

    /// <summary>Echoes the caller's trace id so async callers can correlate.</summary>
    public string ExternalRequestId { get; set; }

    /// <summary>Human-readable error detail when not accepted.</summary>
    public string ErrorMessage { get; set; }
}

/// <summary>
/// Typed reasons a submission was rejected before reaching Marketplace.
/// Stringified into <see cref="MeteringSubmissionResult.Status"/>.
/// </summary>
public enum MeteringSubmissionRejectReason
{
    BadRequest,
    SubscriptionNotFound,
    SubscriptionNotActive,
    SubscriptionInactive,
    PlanNotFound,
    PlanNotMetered,
    DimensionNotFound,
    MarketplaceError,
}
