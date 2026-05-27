using System;

namespace Marketplace.SaaS.Accelerator.DataAccess.Entities;

public partial class MeteredAuditLogs
{
    public int Id { get; set; }
    public int? SubscriptionId { get; set; }
    public string RequestJson { get; set; }
    public string ResponseJson { get; set; }
    public string StatusCode { get; set; }
    public string RunBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? SubscriptionUsageDate { get; set; }

    // Reporting columns: populated on insert by writers (MeteringSubmissionService, MeteredTriggerHelper)
    // so billing exports and caller-correlation queries can run against indexed columns instead of
    // scanning the JSON blobs.
    public string ExternalRequestId { get; set; }
    public string Dimension { get; set; }
    public double? Quantity { get; set; }
    public Guid? MarketplaceUsageEventId { get; set; }

    public virtual Subscriptions Subscription { get; set; }
}