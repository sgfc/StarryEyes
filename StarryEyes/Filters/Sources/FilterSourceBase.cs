﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using StarryEyes.Breezy.Authorize;
using StarryEyes.Breezy.DataModel;
using StarryEyes.Filters.Expressions.Operators;
using StarryEyes.Models;
using StarryEyes.Models.Backstages.NotificationEvents;
using StarryEyes.Models.Stores;

namespace StarryEyes.Filters.Sources
{
    /// <summary>
    /// Tweets source of status
    /// </summary>
    public abstract class FilterSourceBase
    {
        public abstract string FilterKey { get; }

        public abstract string FilterValue { get; }

        public abstract Func<TwitterStatus, bool> GetEvaluator();

        /// <summary>
        /// Activate dependency receiving method.
        /// </summary>
        public virtual void Activate() { }

        /// <summary>
        /// Deactivate dependency receiving method.
        /// </summary>
        public virtual void Deactivate() { }

        /// <summary>
        /// Receive older tweets. <para />
        /// Tweets are registered to StatusStore automatically.
        /// </summary>
        /// <param name="maxId">receiving threshold id</param>
        public IObservable<TwitterStatus> Receive(long? maxId)
        {
            return ReceiveSink(maxId)
                .SelectMany(StoreHelper.NotifyAndMergeStore)
                .Catch((Exception ex) =>
                {
                    BackstageModel.RegisterEvent(
                        new OperationFailedEvent("source: " + FilterKey + ": " + FilterValue + " => " + ex.Message));
                    return Observable.Empty<TwitterStatus>();
                });
        }

        protected virtual IObservable<TwitterStatus> ReceiveSink(long? maxId)
        {
            return Observable.Empty<TwitterStatus>();
        }

        /// <summary>
        /// Get accounts from screen name.
        /// </summary>
        /// <param name="screenName">partial screen name</param>
        /// <returns>accounts collection</returns>
        protected IEnumerable<AuthenticateInfo> GetAccountsFromString(string screenName)
        {
            if (String.IsNullOrEmpty(screenName))
                return AccountsStore.Accounts.Select(i => i.AuthenticateInfo);
            return AccountsStore.Accounts
                                .Select(i => i.AuthenticateInfo)
                                .Where(i => FilterOperatorEquals.StringMatch(
                                    i.UnreliableScreenName, screenName,
                                    FilterOperatorEquals.StringArgumentSide.Right));
        }
    }
}
