using System;
using Marketplace.SaaS.Accelerator.DataAccess.Entities;

namespace Marketplace.SaaS.Accelerator.DataAccess.Contracts;

/// <summary>
/// Repository to access users.
/// </summary>
/// <seealso cref="System.IDisposable" />
/// <seealso cref="Microsoft.Marketplace.SaasKit.Client.DataAccess.Contracts.IBaseRepository{Microsoft.Marketplace.SaasKit.Client.DataAccess.Entities.Users}" />
public interface IUsersRepository : IDisposable, IBaseRepository<Users>
{
    /// <summary>
    /// Gets the partner detail from email.
    /// </summary>
    /// <param name="emailAddress">The email address.</param>
    /// <returns> Users.</returns>
    Users GetPartnerDetailFromEmail(string emailAddress);

    /// <summary>
    /// Stages an add/update for a user without committing. Caller must commit via ISaasKitUnitOfWork.
    /// </summary>
    /// <param name="userDetail">The user detail.</param>
    /// <returns>The tracked Users entity. For new users, UserId is 0 until SaveChanges runs —
    /// set this entity as a navigation property on dependent rows so EF resolves the FK at commit time.</returns>
    Users SaveDeferred(Users userDetail);
}