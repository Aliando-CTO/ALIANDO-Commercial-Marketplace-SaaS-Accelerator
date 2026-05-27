// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Marketplace.SaaS.Accelerator.DataAccess.Contracts;
using Marketplace.SaaS.Accelerator.Services.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Marketplace.SaaS.Accelerator.AdminSite.Filters;

/// <summary>
/// Auth filter for external API callers. Accepts either:
///   1) <c>X-API-Key</c> header matching any ApplicationConfig row with name prefix
///      <c>ExternalMeteringApiKey:</c> (constant-time compared); or
///   2) <c>Authorization: Bearer &lt;jwt&gt;</c> validated via <see cref="ValidateJwtToken"/>
///      against the configured tenant.
/// Rejects with 401 if neither succeeds. Apply with [ExternalApiAuth] on a controller or action.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExternalApiAuthAttribute : Attribute, IAsyncAuthorizationFilter
{
    /// <summary>App-config key prefix used to register one or more API keys.</summary>
    public const string ApiKeyConfigPrefix = "ExternalMeteringApiKey:";

    private const string ApiKeyHeader = "X-API-Key";
    private const string MatchedKeyItem = "ExternalApiAuth:MatchedKeyName";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configRepo = context.HttpContext.RequestServices.GetService(typeof(IApplicationConfigRepository)) as IApplicationConfigRepository;
        if (configRepo is null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // 1) API key path.
        if (context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var presentedKey) && !string.IsNullOrWhiteSpace(presentedKey))
        {
            var matchedName = FindMatchingKeyName(configRepo, presentedKey);
            if (matchedName != null)
            {
                context.HttpContext.Items[MatchedKeyItem] = matchedName;
                return;
            }
        }

        // 2) JWT bearer path.
        var auth = context.HttpContext.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(auth) && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var validator = context.HttpContext.RequestServices.GetService(typeof(ValidateJwtToken)) as ValidateJwtToken;
            if (validator != null)
            {
                try
                {
                    var token = auth.Substring("Bearer ".Length).Trim();
                    await validator.ValidateTokenAsync(token).ConfigureAwait(false);
                    return;
                }
                catch
                {
                    // fall through to 401
                }
            }
        }

        context.Result = new UnauthorizedResult();
    }

    private static string FindMatchingKeyName(IApplicationConfigRepository configRepo, string presentedKey)
    {
        var presentedBytes = System.Text.Encoding.UTF8.GetBytes(presentedKey);

        // Scan all config rows once. Caller's key is compared against each registered key
        // in constant time to avoid leaking key length or content via timing.
        var candidates = configRepo.GetAll()
            .Where(c => !string.IsNullOrWhiteSpace(c.Name)
                        && c.Name.StartsWith(ApiKeyConfigPrefix, StringComparison.Ordinal)
                        && !string.IsNullOrEmpty(c.Value));

        string firstMatchName = null;
        foreach (var candidate in candidates)
        {
            var candidateBytes = System.Text.Encoding.UTF8.GetBytes(candidate.Value);
            if (FixedTimeEquals(presentedBytes, candidateBytes) && firstMatchName == null)
            {
                firstMatchName = candidate.Name.Substring(ApiKeyConfigPrefix.Length);
                // Don't early-return — continue iterating so timing doesn't depend on match position.
            }
        }

        return firstMatchName;
    }

    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        // CryptographicOperations.FixedTimeEquals requires equal-length arrays. Pad with a
        // separate length-check that runs regardless of the byte-by-byte loop result.
        var lengthsMatch = a.Length == b.Length;
        var len = Math.Min(a.Length, b.Length);
        var diff = 0;
        for (var i = 0; i < len; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return lengthsMatch && diff == 0;
    }
}
