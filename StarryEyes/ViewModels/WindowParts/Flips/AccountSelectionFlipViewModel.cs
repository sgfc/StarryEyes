﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Livet;
using StarryEyes.Breezy.Api.Rest;
using StarryEyes.Breezy.Authorize;
using StarryEyes.Breezy.DataModel;
using StarryEyes.Models.Stores;

namespace StarryEyes.ViewModels.WindowParts.Flips
{
    /// <summary>
    /// アカウントを選択するコンポーネント用ビューモデル
    /// </summary>
    public class AccountSelectionFlipViewModel : PartialFlipViewModelBase
    {
        public AccountSelectionFlipViewModel()
        {
            this.CompositeDisposable.Add(_accounts = ViewModelHelperRx.CreateReadOnlyDispatcherCollectionRx(
                AccountsStore.Accounts,
                _ => new SelectableAccountViewModel(this, _.AuthenticateInfo, RaiseSelectedAccountsChanged),
                DispatcherHelper.UIDispatcher));
        }

        private string _selectionReason;
        /// <summary>
        /// Reason of selecting account
        /// </summary>
        public string SelectionReason
        {
            get { return _selectionReason; }
            set
            {
                _selectionReason = value;
                RaisePropertyChanged(() => SelectionReason);
            }
        }

        private readonly ReadOnlyDispatcherCollectionRx<SelectableAccountViewModel> _accounts;
        public ReadOnlyDispatcherCollectionRx<SelectableAccountViewModel> Accounts
        {
            get { return _accounts; }
        }

        public void SetSelectedAccountIds(IEnumerable<long> accountIds)
        {
            var acl = accountIds.Guard().ToArray();
            Accounts.ForEach(i => i.IsSelected = acl.Contains(i.Id));
        }

        public IEnumerable<AuthenticateInfo> SelectedAccounts
        {
            get
            {
                return Accounts
                    .Where(i => i.IsSelected)
                    .Select(_ => _.AuthenticateInfo);
            }
            set
            {
                SetSelectedAccountIds(value.Guard().Select(i => i.Id));
            }
        }

        public event Action SelectedAccountsChanged;
        private void RaiseSelectedAccountsChanged()
        {
            var handler = this.SelectedAccountsChanged;
            if (handler != null)
                handler();
        }
    }

    /// <summary>
    /// AccountSelectorViewModelで選択されるアカウント
    /// </summary>
    public class SelectableAccountViewModel : ViewModel
    {
        private readonly AuthenticateInfo _info;
        public AuthenticateInfo AuthenticateInfo
        {
            get { return _info; }
        }

        private readonly AccountSelectionFlipViewModel _parent;

        private readonly Action _onSelectionChanged;

        public SelectableAccountViewModel(AccountSelectionFlipViewModel parent, AuthenticateInfo info, Action onSelectionChanged)
        {
            this._parent = parent;
            this._info = info;
            this._onSelectionChanged = onSelectionChanged;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (this._isSelected == value) return;
                this._isSelected = value;
                this.RaisePropertyChanged(() => this.IsSelected);
                this._onSelectionChanged();
            }
        }

        public long Id
        {
            get { return _info.Id; }
        }

        public string ScreenName
        {
            get { return _info.UnreliableScreenName; }
        }

        public Uri ProfileImageUri
        {
            get
            {
                if (_info.UnreliableProfileImageUri == null)
                {
                    Task.Run(() => _info.ShowUser(_info.Id)
                                        .Catch(Observable.Empty<TwitterUser>())
                                        .Subscribe(user =>
                                        {
                                            _info.UnreliableProfileImageUriString = user.ProfileImageUri.OriginalString;
                                            RaisePropertyChanged(() => ProfileImageUri);
                                        }));
                }
                return _info.UnreliableProfileImageUri;
            }
        }

        public void ToggleSelection()
        {
            this.IsSelected = !IsSelected;
        }

        public void SelectExcepted()
        {
            _parent.SelectedAccounts = new[] { this._info };
        }
    }
}
