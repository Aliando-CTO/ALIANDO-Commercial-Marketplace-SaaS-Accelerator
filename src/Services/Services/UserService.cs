using System;
using Marketplace.SaaS.Accelerator.DataAccess.Contracts;
using Marketplace.SaaS.Accelerator.DataAccess.Entities;
using Marketplace.SaaS.Accelerator.Services.Models;

namespace Marketplace.SaaS.Accelerator.Services.Services;

/// <summary>
/// Users Service.
/// </summary>
public class UserService
{
    /// <summary>
    /// The user repository.
    /// </summary>
    private IUsersRepository userRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService" /> class.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    public UserService(IUsersRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    /// <summary>
    /// Adds the partner detail.
    /// </summary>
    /// <param name="partnerDetailViewModel">The partner detail view model.</param>
    /// <returns> User id.</returns>
    public int AddUser(PartnerDetailViewModel partnerDetailViewModel)
    {
        if (!string.IsNullOrEmpty(partnerDetailViewModel.EmailAddress))
        {
            return this.userRepository.Save(BuildUser(partnerDetailViewModel));
        }

        return 0;
    }

    /// <summary>
    /// Stages an add/update for a user without committing. Caller must commit via
    /// <see cref="ISaasKitUnitOfWork"/>. Returns the tracked entity so dependent rows
    /// can set it as a navigation property and let EF resolve the FK at commit time.
    /// </summary>
    public Users AddUserDeferred(PartnerDetailViewModel partnerDetailViewModel)
    {
        if (string.IsNullOrEmpty(partnerDetailViewModel.EmailAddress))
        {
            return null;
        }

        return this.userRepository.SaveDeferred(BuildUser(partnerDetailViewModel));
    }

    private static Users BuildUser(PartnerDetailViewModel partnerDetailViewModel)
    {
        return new Users()
        {
            UserId = partnerDetailViewModel.UserId,
            EmailAddress = partnerDetailViewModel.EmailAddress,
            FullName = partnerDetailViewModel.FullName,
            CreatedDate = DateTime.Now,
        };
    }

    /// <summary>
    /// Gets the user identifier from email address.
    /// </summary>
    /// <param name="partnerEmail">The partner email.</param>
    /// <returns>returns user id.</returns>
    public int GetUserIdFromEmailAddress(string partnerEmail)
    {
        if (!string.IsNullOrEmpty(partnerEmail))
        {
            return this.userRepository.GetPartnerDetailFromEmail(partnerEmail).UserId;
        }

        return 0;
    }
}