﻿using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Threading;
using Livet;
using Livet.Commands;
using StarryEyes.Breezy.Util;
using StarryEyes.Models;
using StarryEyes.Models.Stores;
using StarryEyes.Nightmare.Windows;
using StarryEyes.ViewModels.WindowParts.Timelines;
using StarryEyes.Views.Messaging;
using StarryEyes.Views.Utils;

namespace StarryEyes.ViewModels.WindowParts.Flips.SearchFlips
{
    public class UserInfoViewModel : ViewModel
    {
        private readonly SearchFlipViewModel _parent;
        private readonly string _screenName;
        private UserStatusesViewModel _statuses;
        private UserFavoritesViewModel _favorites;
        private UserFollowingViewModel _following;
        private UserFollowersViewModel _followers;
        private bool _communicating = true;
        private UserViewModel _user;

        private UserDisplayKind _displayKind = UserDisplayKind.Statuses;
        public UserDisplayKind DisplayKind
        {

            get { return _displayKind; }
            set
            {
                if (_displayKind == value) return;
                this._displayKind = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(() => IsVisibleStatuses);
                this.RaisePropertyChanged(() => IsVisibleFavorites);
                this.RaisePropertyChanged(() => IsVisibleFollowing);
                this.RaisePropertyChanged(() => IsVisibleFollowers);
            }
        }

        private ObservableCollection<RelationControlViewModel> _relationControls = new ObservableCollection<RelationControlViewModel>();
        public ObservableCollection<RelationControlViewModel> RelationControls
        {
            get { return this._relationControls; }
            set { this._relationControls = value; }
        }

        public SearchFlipViewModel Parent
        {
            get { return this._parent; }
        }

        public string ScreenName
        {
            get { return this._screenName; }
        }

        public bool Communicating
        {
            get { return this._communicating; }
            set
            {
                this._communicating = value;
                this.RaisePropertyChanged();
            }
        }

        public UserViewModel User
        {
            get { return this._user; }
            set
            {
                this._user = value;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(() => IsUserAvailable);
            }
        }

        public bool IsUserAvailable
        {
            get { return User != null; }
        }

        public UserStatusesViewModel Statuses
        {
            get { return this._statuses; }
        }

        public UserFavoritesViewModel Favorites
        {
            get { return this._favorites; }
        }

        public UserFollowingViewModel Following
        {
            get { return this._following; }
        }

        public UserFollowersViewModel Followers
        {
            get { return this._followers; }
        }

        public bool IsVisibleStatuses
        {
            get { return DisplayKind == UserDisplayKind.Statuses; }
        }

        public bool IsVisibleFavorites
        {
            get { return DisplayKind == UserDisplayKind.Favorites; }
        }

        public bool IsVisibleFollowing
        {
            get { return DisplayKind == UserDisplayKind.Following; }
        }

        public bool IsVisibleFollowers
        {
            get { return DisplayKind == UserDisplayKind.Followers; }
        }

        public UserInfoViewModel(SearchFlipViewModel parent, string screenName)
        {
            this._parent = parent;
            this._screenName = screenName;
            StoreHelper.GetUser(screenName)
                       .Finally(() => Communicating = false)
                       .Subscribe(
                           user =>
                           {
                               User = new UserViewModel(user);
                               var ps = this._statuses;
                               this._statuses = new UserStatusesViewModel(this);
                               this.RaisePropertyChanged(() => Statuses);
                               if (ps != null)
                               {
                                   ps.Dispose();
                               }
                               var pf = this._favorites;
                               this._favorites = new UserFavoritesViewModel(this);
                               this.RaisePropertyChanged(() => Favorites);
                               if (pf != null)
                               {
                                   pf.Dispose();
                               }
                               var pfw = this._following;
                               this._following = new UserFollowingViewModel(this);
                               this.RaisePropertyChanged(() => Following);
                               if (pfw != null)
                               {
                                   pfw.Dispose();
                               }
                               var pfr = this._followers;
                               this._followers = new UserFollowersViewModel(this);
                               this.RaisePropertyChanged(() => Followers);
                               if (pfr != null)
                               {
                                   pfr.Dispose();
                               }
                               Observable.Start(() => AccountsStore.Accounts)
                                         .SelectMany(a => a)
                                         .Where(a => a.UserId != user.Id)
                                         .Select(a => new RelationControlViewModel(this, a.AuthenticateInfo, user))
                                         .ObserveOn(DispatcherHolder.Dispatcher, DispatcherPriority.Render)
                                         .Subscribe(RelationControls.Add);
                           },
                           ex =>
                           {
                               parent.Messenger.Raise(new TaskDialogMessage(new TaskDialogOptions
                               {
                                   Title = "ユーザー表示エラー",
                                   MainIcon = VistaTaskDialogIcon.Error,
                                   MainInstruction = "ユーザーを表示できません。",
                                   Content = ex.Message,
                                   CommonButtons = TaskDialogCommonButtons.Close
                               }));
                               User = null;
                               parent.CloseResults();
                           });
        }

        public void Close()
        {
            Parent.RewindStack();
        }

        public void ShowStatuses()
        {
            DisplayKind = UserDisplayKind.Statuses;
        }

        public void ShowFavorites()
        {
            DisplayKind = UserDisplayKind.Favorites;
        }

        public void ShowFollowing()
        {
            DisplayKind = UserDisplayKind.Following;
        }

        public void ShowFollowers()
        {
            DisplayKind = UserDisplayKind.Followers;
        }

        #region Text selection control

        private string _selectedText;
        public string SelectedText
        {
            get { return this._selectedText ?? String.Empty; }
            set
            {
                this._selectedText = value;
                this.RaisePropertyChanged();
            }
        }

        public void CopyText()
        {
            try
            {
                Clipboard.SetText(SelectedText);
            }
            // ReSharper disable EmptyGeneralCatchClause
            catch
            // ReSharper restore EmptyGeneralCatchClause
            {
            }
        }

        public void SetTextToInputBox()
        {
            InputAreaModel.SetText(body: SelectedText);
        }

        public void FindOnKrile()
        {
            SearchFlipModel.RequestSearch(SelectedText, SearchMode.Local);
        }

        public void FindOnTwitter()
        {
            SearchFlipModel.RequestSearch(SelectedText, SearchMode.Web);
        }

        private const string GoogleUrl = @"http://www.google.com/search?q={0}";
        public void FindOnGoogle()
        {
            var encoded = HttpUtility.UrlEncode(SelectedText);
            var url = String.Format(GoogleUrl, encoded);
            BrowserHelper.Open(url);
        }

        #endregion

        #region OpenLinkCommand

        private ListenerCommand<string> _openLinkCommand;

        public ListenerCommand<string> OpenLinkCommand
        {
            get { return _openLinkCommand ?? (_openLinkCommand = new ListenerCommand<string>(OpenLink)); }
        }

        public void OpenLink(string parameter)
        {
            var param = TextBlockStylizer.ResolveInternalUrl(parameter);
            switch (param.Item1)
            {
                case LinkType.User:
                    SearchFlipModel.RequestSearch(param.Item2, SearchMode.UserScreenName);
                    break;
                case LinkType.Hash:
                    SearchFlipModel.RequestSearch("#" + param.Item2, SearchMode.Web);
                    break;
                case LinkType.Url:
                    BrowserHelper.Open(param.Item2);
                    break;
            }
        }

        #endregion
    }

    public enum UserDisplayKind
    {
        Statuses,
        Favorites,
        Following,
        Followers,
    }
}
