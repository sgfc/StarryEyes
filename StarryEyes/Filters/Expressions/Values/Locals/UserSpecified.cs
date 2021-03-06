﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StarryEyes.Albireo.Data;
using StarryEyes.Models.Stores;

namespace StarryEyes.Filters.Expressions.Values.Locals
{
    public sealed class UserSpecified : UserExpressionBase
    {
        readonly string _originalScreenName;
        readonly long _userId;
        readonly AccountRelationData _adata;

        public UserSpecified(string screenName)
        {
            _originalScreenName = screenName;
            _userId = AccountsStore.Accounts
                .Where(u => u.AuthenticateInfo.UnreliableScreenName == screenName)
                .Select(u => u.UserId)
                .FirstOrDefault();
            if (_userId == 0)
            {
                _userId = UserStore.GetId(screenName);
            }
            else
            {
                _adata = AccountRelationDataStore.Get(_userId);
            }
        }

        public UserSpecified(long id)
        {
            _userId = id;
            _adata = AccountRelationDataStore.Get(_userId);
        }

        public override void BeginLifecycle()
        {
            AccountRelationDataStore.AccountDataUpdated += this.AccountRelationDataAccountDataUpdated;
        }

        public override void EndLifecycle()
        {
            AccountRelationDataStore.AccountDataUpdated -= this.AccountRelationDataAccountDataUpdated;
        }

        void AccountRelationDataAccountDataUpdated(RelationDataChangedInfo obj)
        {
            if (obj.AccountUserId == _userId)
            {
                this.RaiseReapplyFilter(obj);
            }
        }

        public override IReadOnlyCollection<long> Users
        {
            get
            {
                return new List<long>(new[] { _userId });
            }
        }

        public override IReadOnlyCollection<long> Following
        {
            get
            {
                if (_adata == null)
                    return new List<long>(); // returns empty list
                return new AVLTree<long>(_adata.Following);
            }
        }

        public override IReadOnlyCollection<long> Followers
        {
            get
            {
                if (_adata == null)
                    return new List<long>();
                return new AVLTree<long>(_adata.Followers);
            }
        }

        public override IReadOnlyCollection<long> Blockings
        {
            get
            {
                if (_adata == null)
                    return new List<long>();
                return new AVLTree<long>(_adata.Blockings);
            }
        }

        public override long UserId
        {
            get { return _userId; }
        }

        public override string ToQuery()
        {
            if (String.IsNullOrEmpty(_originalScreenName))
                return "#" + _userId.ToString(CultureInfo.InvariantCulture);
            return "@" + _originalScreenName;
        }
    }
}
