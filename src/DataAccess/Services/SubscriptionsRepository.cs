using System;
using System.Collections.Generic;
using System.Linq;
using Marketplace.SaaS.Accelerator.DataAccess.Context;
using Marketplace.SaaS.Accelerator.DataAccess.Contracts;
using Marketplace.SaaS.Accelerator.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.SaaS.Accelerator.DataAccess.Services;

/// <summary>
/// Subscriptions Repository.
/// </summary>
/// <seealso cref="ISubscriptionsRepository" />
public class SubscriptionsRepository : ISubscriptionsRepository
{
    /// <summary>
    /// The context.
    /// </summary>
    private readonly SaasKitContext context;

    /// <summary>
    /// The disposed.
    /// </summary>
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsRepository" /> class.
    /// </summary>
    /// <param name="context">The this.context.</param>
    public SubscriptionsRepository(SaasKitContext context)
    {
        this.context = context;
    }

    /// <summary>
    /// Adds the specified subscription details.
    /// </summary>
    /// <param name="subscriptionDetails">The subscription details.</param>
    /// <returns> Subscription Detail Id.</returns>
    public int Save(Subscriptions subscriptionDetails)
    {
        var existingSubscriptions = this.context.Subscriptions.Where(s => s.AmpsubscriptionId == subscriptionDetails.AmpsubscriptionId).FirstOrDefault();
        if (existingSubscriptions != null)
        {
            existingSubscriptions.SubscriptionStatus = subscriptionDetails.SubscriptionStatus;
            existingSubscriptions.AmpplanId = subscriptionDetails.AmpplanId;
            existingSubscriptions.Ampquantity = subscriptionDetails.Ampquantity;
            existingSubscriptions.AmpOfferId = subscriptionDetails.AmpOfferId;
            existingSubscriptions.Term = subscriptionDetails.Term;
            existingSubscriptions.StartDate = subscriptionDetails.StartDate;
            existingSubscriptions.EndDate = subscriptionDetails.EndDate;

            this.context.Subscriptions.Update(existingSubscriptions);
            this.context.SaveChanges();
            return existingSubscriptions.Id;
        }

        this.context.Subscriptions.Add(subscriptionDetails);
        this.context.SaveChanges();
        return subscriptionDetails.Id;
    }

    /// <summary>
    /// Adds the specified subscription details.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="subscriptionStatus">The subscription status.</param>
    /// <param name="isActive">if set to <c>true</c> [is active].</param>
    public void UpdateStatusForSubscription(Guid subscriptionId, string subscriptionStatus, bool isActive)
    {
        var existingSubscription = this.context.Subscriptions.Where(s => s.AmpsubscriptionId == subscriptionId).FirstOrDefault();
        if (existingSubscription != null)
        {
            existingSubscription.IsActive = isActive;
            existingSubscription.SubscriptionStatus = subscriptionStatus;
            this.context.Subscriptions.Update(existingSubscription);
        }

        this.context.SaveChanges();
    }

    /// <summary>
    /// Updates the plan for subscription.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="planId">The plan identifier.</param>
    public void UpdatePlanForSubscription(Guid subscriptionId, string planId)
    {
        var existingSubscription = this.context.Subscriptions.Where(s => s.AmpsubscriptionId == subscriptionId).FirstOrDefault();
        if (existingSubscription != null)
        {
            existingSubscription.AmpplanId = planId;
            this.context.Subscriptions.Update(existingSubscription);
        }

        this.context.SaveChanges();
    }

    /// <summary>
    /// Updates the Quantity for subscription.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="quantity">The Quantity.</param>
    public void UpdateQuantityForSubscription(Guid subscriptionId, int quantity)
    {
        var existingSubscription = this.context.Subscriptions.Where(s => s.AmpsubscriptionId == subscriptionId).FirstOrDefault();
        if (existingSubscription != null)
        {
            existingSubscription.Ampquantity = quantity;
            this.context.Subscriptions.Update(existingSubscription);
        }

        this.context.SaveChanges();
    }

    /// <summary>
    /// Gets this instance.
    /// </summary>
    /// <returns> Subscriptions.</returns>
    public IEnumerable<Subscriptions> Get()
    {
        return this.context.Subscriptions.Include(s => s.User).OrderByDescending(s => s.CreateDate);
    }

    /// <summary>
    /// Gets the specified identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns> Subscriptions.</returns>
    public Subscriptions Get(int id)
    {
        return this.context.Subscriptions.Where(s => s.Id == id).FirstOrDefault();
    }

    /// <summary>
    /// Gets the subscriptions by email address.
    /// </summary>
    /// <param name="partnerEmailAddress">The partner email address.</param>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="isIncludeDeactvated">if set to <c>true</c> [is include deactvated].</param>
    /// <returns> List of Subscriptions.</returns>
    public IEnumerable<Subscriptions> GetSubscriptionsByEmailAddress(string partnerEmailAddress, Guid subscriptionId, bool isIncludeDeactvated = false)
    {
        if (subscriptionId != default)
        {
            return this.context.Subscriptions.Include(s => s.User).Where(s => s.User != null && s.User.EmailAddress == partnerEmailAddress && s.AmpsubscriptionId == subscriptionId);
        }
        else
        {
            return this.context.Subscriptions.Include(s => s.User).Where(s => s.User != null && s.User.EmailAddress == partnerEmailAddress);
        }
    }

    /// <summary>
    /// Gets the subscriptions by ScheduleId.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="isIncludeDeactvated">if set to <c>true</c> [is include deactvated].</param>
    /// <returns> Subscriptions.</returns>
    public Subscriptions GetById(Guid subscriptionId, bool isIncludeDeactvated = false)
    {
        if (subscriptionId != default)
        {
            return this.context.Subscriptions.Include(s => s.User).Where(s => s.AmpsubscriptionId == subscriptionId).FirstOrDefault();
        }

        return null;
    }

    /// <summary>
    /// Gets the subscriptions by ScheduleId.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="planId">The plan identifier.</param>
    /// <returns>
    /// List of  Subscription Parameters.
    /// </returns>
    public List<SubscriptionParametersOutput> GetSubscriptionsParametersById(Guid subscriptionId, Guid planId)
    {
        if (subscriptionId != default)
        {
            var subscriptionParameters = this.context.SubscriptionParametersOutput.FromSqlRaw("dbo.spGetSubscriptionParameters {0},{1}", subscriptionId, planId).ToList();
            return subscriptionParameters.ToList();
        }

        return new List<SubscriptionParametersOutput>();
    }

    /// <summary>
    /// Updates the plan for subscription.
    /// </summary>
    /// <param name="subscriptionParametersOutput">The subscription parameters output.</param>
    public void AddSubscriptionParameters(SubscriptionParametersOutput subscriptionParametersOutput)
    {
        var existingSubscriptionparameter = this.context.SubscriptionAttributeValues.Where(s => s.Id == subscriptionParametersOutput.Id).FirstOrDefault();
        if (existingSubscriptionparameter != null)
        {
            existingSubscriptionparameter.OfferId = subscriptionParametersOutput.OfferId;
            this.context.SubscriptionAttributeValues.Update(existingSubscriptionparameter);
            this.context.SaveChanges();
        }
        else
        {
            SubscriptionAttributeValues newAttributeValue = new SubscriptionAttributeValues();
            newAttributeValue.OfferId = subscriptionParametersOutput.OfferId;
            newAttributeValue.PlanAttributeId = subscriptionParametersOutput.PlanAttributeId;
            newAttributeValue.Value = subscriptionParametersOutput.Value;
            newAttributeValue.SubscriptionId = subscriptionParametersOutput.SubscriptionId;
            newAttributeValue.CreateDate = subscriptionParametersOutput.CreateDate;
            newAttributeValue.UserId = subscriptionParametersOutput.UserId;
            newAttributeValue.PlanId = subscriptionParametersOutput.PlanId;
            this.context.SubscriptionAttributeValues.Add(newAttributeValue);
            this.context.SaveChanges();
        }
    }

    /// <summary>
    /// Removes the specified entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    public void Remove(Subscriptions entity)
    {
        this.context.Subscriptions.Remove(entity);
        this.context.SaveChanges();
    }

    /// <inheritdoc />
    public int SaveDeferred(Subscriptions subscriptionDetails)
    {
        var existing = this.context.Subscriptions
            .FirstOrDefault(s => s.AmpsubscriptionId == subscriptionDetails.AmpsubscriptionId);
        if (existing != null)
        {
            existing.SubscriptionStatus = subscriptionDetails.SubscriptionStatus;
            existing.AmpplanId = subscriptionDetails.AmpplanId;
            existing.Ampquantity = subscriptionDetails.Ampquantity;
            existing.AmpOfferId = subscriptionDetails.AmpOfferId;
            existing.Term = subscriptionDetails.Term;
            existing.StartDate = subscriptionDetails.StartDate;
            existing.EndDate = subscriptionDetails.EndDate;
            this.context.Subscriptions.Update(existing);
            return existing.Id;
        }

        this.context.Subscriptions.Add(subscriptionDetails);
        return subscriptionDetails.Id;
    }

    /// <inheritdoc />
    public void UpdateStatusForSubscriptionDeferred(Guid subscriptionId, string subscriptionStatus, bool isActive)
    {
        var existing = this.context.Subscriptions
            .FirstOrDefault(s => s.AmpsubscriptionId == subscriptionId);
        if (existing is null) return;

        existing.IsActive = isActive;
        existing.SubscriptionStatus = subscriptionStatus;
        this.context.Subscriptions.Update(existing);
    }

    /// <inheritdoc />
    public void UpdatePlanForSubscriptionDeferred(Guid subscriptionId, string planId)
    {
        var existing = this.context.Subscriptions
            .FirstOrDefault(s => s.AmpsubscriptionId == subscriptionId);
        if (existing is null) return;

        existing.AmpplanId = planId;
        this.context.Subscriptions.Update(existing);
    }

    /// <inheritdoc />
    public void UpdateQuantityForSubscriptionDeferred(Guid subscriptionId, int quantity)
    {
        var existing = this.context.Subscriptions
            .FirstOrDefault(s => s.AmpsubscriptionId == subscriptionId);
        if (existing is null) return;

        existing.Ampquantity = quantity;
        this.context.Subscriptions.Update(existing);
    }

    /// <inheritdoc />
    public void AddSubscriptionParametersDeferred(SubscriptionParametersOutput subscriptionParametersOutput)
    {
        var existing = this.context.SubscriptionAttributeValues
            .FirstOrDefault(s => s.Id == subscriptionParametersOutput.Id);
        if (existing != null)
        {
            existing.OfferId = subscriptionParametersOutput.OfferId;
            this.context.SubscriptionAttributeValues.Update(existing);
            return;
        }

        this.context.SubscriptionAttributeValues.Add(new SubscriptionAttributeValues
        {
            OfferId = subscriptionParametersOutput.OfferId,
            PlanAttributeId = subscriptionParametersOutput.PlanAttributeId,
            Value = subscriptionParametersOutput.Value,
            SubscriptionId = subscriptionParametersOutput.SubscriptionId,
            CreateDate = subscriptionParametersOutput.CreateDate,
            UserId = subscriptionParametersOutput.UserId,
            PlanId = subscriptionParametersOutput.PlanId,
        });
    }

    /// <inheritdoc />
    public void RemoveDeferred(Subscriptions entity)
    {
        this.context.Subscriptions.Remove(entity);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.context.Dispose();
            }
        }

        this.disposed = true;
    }
}