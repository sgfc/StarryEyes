﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using StarryEyes.Albireo.Data;
using StarryEyes.Models.Stores;

namespace StarryEyes.Filters.Expressions.Values.Locals
{
    public sealed class UserAny : UserExpressionBase
    {
        private CompositeDisposable _disposables = new CompositeDisposable();

        public override IReadOnlyCollection<long> Users
        {
            get
            {
                return AccountsStore.AccountIds;
            }
        }

        public override IReadOnlyCollection<long> Followers
        {
            get
            {
                var followers = new AVLTree<long>();
                AccountsStore.Accounts
                    .SelectMany(a => AccountRelationDataStore.Get(a.UserId).Following)
                    .ForEach(followers.Add);
                return followers;
            }
        }

        public override IReadOnlyCollection<long> Following
        {
            get
            {
                var following = new AVLTree<long>();
                AccountsStore.Accounts
                    .SelectMany(a => AccountRelationDataStore.Get(a.UserId).Following)
                    .ForEach(following.Add);
                return following;
            }
        }

        public override IReadOnlyCollection<long> Blockings
        {
            get
            {
                var blockings = new AVLTree<long>();
                AccountsStore.Accounts
                    .SelectMany(a => AccountRelationDataStore.Get(a.UserId).Blockings)
                    .ForEach(blockings.Add);
                return blockings;
            }
        }

        public override string ToQuery()
        {
            return "our";
        }

        public override long UserId
        {
            get { return -1; } // an representive user is not existed.
        }

        public override void BeginLifecycle()
        {
            _disposables.Add(
                AccountsStore.Accounts
                             .ListenCollectionChanged()
                             .Subscribe(_ => this.RaiseReapplyFilter(null)));
            _disposables.Add(
                Observable.FromEvent<RelationDataChangedInfo>(
                    h => AccountRelationDataStore.AccountDataUpdated += h,
                    h => AccountRelationDataStore.AccountDataUpdated -= h)
                          .Subscribe(this.RaiseReapplyFilter));
        }

        public override void EndLifecycle()
        {
            Interlocked.Exchange(ref _disposables, new CompositeDisposable()).Dispose();
        }
    }
}
