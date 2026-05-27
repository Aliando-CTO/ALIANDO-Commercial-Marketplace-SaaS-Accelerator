using System;
using System.Collections.Generic;
using Marketplace.SaaS.Accelerator.DataAccess.Entities;

namespace Marketplace.SaaS.Accelerator.DataAccess.Contracts;

/// <summary>
/// Subscription Usage Logs Repository Interface.
/// </summary>
/// <seealso cref="System.IDisposable" />
/// <seealso cref="Microsoft.Marketplace.SaasKit.Client.DataAccess.Contracts.IBaseRepository{Microsoft.Marketplace.SaasKit.Client.DataAccess.Entities.MeteredAuditLogs}" />
public interface ISubscriptionUsageLogsRepository : IDisposable, IBaseRepository<MeteredAuditLogs>
{
    /// <summary>
    /// Gets the metered audit logs by subscription identifier.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="format">Specify to format the result properly.</param>
    /// <returns> Metered Audit Logs.</returns>
    List<MeteredAuditLogs> GetMeteredAuditLogsBySubscriptionId(int subscriptionId, bool format = false);

    /// <summary>
    /// Queries metered audit logs with optional filters and paging. Results are sorted newest-first.
    /// </summary>
    /// <param name="runByPrefix">Channel prefix match (e.g. "Manual", "Scheduler", "ExternalAPI"). Null/empty = any.</param>
    /// <param name="runByContains">Substring match within RunBy (e.g. the Source value). Null/empty = any.</param>
    /// <param name="statusCode">Exact status code match. Null/empty = any.</param>
    /// <param name="externalRequestIdContains">Substring search within RequestJson for the caller's trace id. Null/empty = any.</param>
    /// <param name="from">Inclusive lower bound on CreatedDate. Null = no lower bound.</param>
    /// <param name="to">Inclusive upper bound on CreatedDate. Null = no upper bound.</param>
    /// <param name="skip">Pagination offset.</param>
    /// <param name="take">Pagination page size.</param>
    /// <param name="totalCount">Outputs unpaged row count for the same filters.</param>
    /// <returns>One page of matching rows, newest first, with Subscription navigation included.</returns>
    List<MeteredAuditLogs> QueryMeteredAuditLogs(
        string runByPrefix,
        string runByContains,
        string statusCode,
        string externalRequestIdContains,
        DateTime? from,
        DateTime? to,
        int skip,
        int take,
        out int totalCount);
}