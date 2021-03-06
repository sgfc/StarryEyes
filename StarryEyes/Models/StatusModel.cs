﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Livet;
using StarryEyes.Breezy.Authorize;
using StarryEyes.Breezy.DataModel;
using StarryEyes.Breezy.Imaging;
using StarryEyes.Models.Stores;
using StarryEyes.Settings;

namespace StarryEyes.Models
{
    public class StatusModel
    {
        #region Static members

        private static readonly object _staticCacheLock = new object();

        private static readonly SortedDictionary<long, WeakReference> _staticCache =
            new SortedDictionary<long, WeakReference>();

        private static readonly ConcurrentDictionary<long, object> _generateLock =
            new ConcurrentDictionary<long, object>();

        public static int CachedObjectsCount
        {
            get { return _staticCache.Count; }
        }

        public static StatusModel GetIfCacheIsAlive(long id)
        {
            StatusModel model = null;
            WeakReference wr;
            lock (_staticCacheLock)
            {
                _staticCache.TryGetValue(id, out wr);
            }
            if (wr != null)
                model = (StatusModel)wr.Target;
            return model;
        }

        private const int CleanupInterval = 5000;

        private static int _cleanupCount;

        public static async Task<StatusModel> Get(TwitterStatus status)
        {
            status = await StatusStore.Get(status.Id)
                                      .DefaultIfEmpty(status)
                                      .FirstAsync();
            var rto = status.RetweetedOriginal == null
                          ? null
                          : await Get(status.RetweetedOriginal);
            var lockerobj = _generateLock.GetOrAdd(status.Id, new object());
            try
            {
                lock (lockerobj)
                {
                    StatusModel model;
                    WeakReference wr;
                    lock (_staticCacheLock)
                    {
                        _staticCache.TryGetValue(status.Id, out wr);
                    }
                    if (wr != null)
                    {
                        model = (StatusModel)wr.Target;
                        if (model != null)
                        {
                            return model;
                        }
                    }

                    // cache is dead/not cached yet
                    model = new StatusModel(status, rto);
                    wr = new WeakReference(model);
                    lock (_staticCacheLock)
                    {
                        _staticCache[status.Id] = wr;
                    }
                    return model;
                }
            }
            finally
            {
                _generateLock.TryRemove(status.Id, out lockerobj);
                // ReSharper disable InvertIf
#pragma warning disable 4014
                if (Interlocked.Increment(ref _cleanupCount) == CleanupInterval)
                {
                    Interlocked.Exchange(ref _cleanupCount, 0);
                    Task.Run((Action)CollectGarbages);
                }
#pragma warning restore 4014
                // ReSharper restore InvertIf
            }
        }

        public static void CollectGarbages()
        {
            System.Diagnostics.Debug.WriteLine("*** COLLECT STATUS MODEL GARBAGES...");
            GC.Collect(2, GCCollectionMode.Optimized);
            long[] values;
            lock (_staticCacheLock)
            {
                values = _staticCache.Keys.ToArray();
            }
            foreach (var ids in values.Buffer(256))
            {
                lock (_staticCacheLock)
                {
                    foreach (var id in ids)
                    {
                        WeakReference wr;
                        _staticCache.TryGetValue(id, out wr);
                        if (wr != null && wr.Target == null)
                            _staticCache.Remove(id);
                    }
                }
                Thread.Sleep(0);
            }
        }

        #endregion

        private readonly ObservableSynchronizedCollection<TwitterUser> _favoritedUsers =
            new ObservableSynchronizedCollection<TwitterUser>();

        private readonly SortedDictionary<long, TwitterUser> _favoritedUsersDic =
            new SortedDictionary<long, TwitterUser>();

        private readonly object _favoritedsLock = new object();

        private readonly ObservableSynchronizedCollection<TwitterUser> _retweetedUsers =
            new ObservableSynchronizedCollection<TwitterUser>();

        private readonly SortedDictionary<long, TwitterUser> _retweetedUsersDic =
            new SortedDictionary<long, TwitterUser>();

        private readonly object _retweetedsLock = new object();

        private volatile bool _isFavoritedUsersLoaded;
        private volatile bool _isRetweetedUsersLoaded;

        private Subject<Unit> _imagesSubject = new Subject<Unit>();

        private StatusModel(TwitterStatus status)
        {
            Status = status;
            ImageResolver.Resolve(status.GetEntityAidedText(true))
                         .Aggregate(new List<Tuple<Uri, Uri>>(), (l, i) =>
                         {
                             l.Add(i);
                             return l;
                         })
                         .Finally(() =>
                         {
                             var subj = Interlocked.Exchange(ref _imagesSubject, null);
                             lock (subj)
                             {
                                 subj.OnCompleted();
                                 subj.Dispose();
                             }
                         })
                         .Subscribe(l => Images = l);
        }

        private StatusModel(TwitterStatus status, StatusModel retweetedOriginal)
            : this(status)
        {
            this.RetweetedOriginal = retweetedOriginal;
        }

        public TwitterStatus Status { get; private set; }

        public StatusModel RetweetedOriginal { get; private set; }

        public ObservableSynchronizedCollection<TwitterUser> FavoritedUsers
        {
            get
            {
                if (!_isFavoritedUsersLoaded)
                {
                    _isFavoritedUsersLoaded = true;
                    LoadFavoritedUsers();
                }
                return _favoritedUsers;
            }
        }

        public ObservableSynchronizedCollection<TwitterUser> RetweetedUsers
        {
            get
            {
                if (!_isRetweetedUsersLoaded)
                {
                    _isRetweetedUsersLoaded = true;
                    LoadRetweetedUsers();
                }
                return _retweetedUsers;
            }
        }

        public IEnumerable<Tuple<Uri, Uri>> Images { get; private set; }

        public IObservable<Unit> ImagesSubject
        {
            get { return _imagesSubject; }
        }

        private void LoadFavoritedUsers()
        {
            if (Status.FavoritedUsers != null && Status.FavoritedUsers.Length > 0)
            {
                Status.FavoritedUsers
                      .Distinct()
                      .Reverse()
                      .Where(_ =>
                      {
                          lock (_favoritedsLock)
                          {
                              if (_favoritedUsersDic.ContainsKey(_)) return false;
                              _favoritedUsersDic.Add(_, null);
                              return true;
                          }
                      })
                      .Select(u => Observable.Start(() => StoreHelper.GetUser(u)))
                      .Merge()
                      .SelectMany(_ => _)
                      .Do(_ =>
                      {
                          lock (_favoritedsLock)
                          {
                              _favoritedUsersDic[_.Id] = _;
                          }
                      })
                      .Subscribe(_ => _favoritedUsers.Insert(0, _));
            }
        }

        private void LoadRetweetedUsers()
        {
            if (Status.RetweetedUsers != null && Status.RetweetedUsers.Length > 0)
            {
                Status.RetweetedUsers
                      .Distinct()
                      .Reverse()
                      .Where(_ =>
                      {
                          lock (_retweetedsLock)
                          {
                              if (_retweetedUsersDic.ContainsKey(_)) return false;
                              _retweetedUsersDic.Add(_, null);
                              return true;
                          }
                      })
                      .Select(u => Observable.Start(() => StoreHelper.GetUser(u)))
                      .Merge()
                      .SelectMany(_ => _)
                      .Do(_ =>
                      {
                          lock (_retweetedsLock)
                          {
                              _retweetedUsersDic[_.Id] = _;
                          }
                      })
                      .Subscribe(_ => _retweetedUsers.Insert(0, _));
            }
        }

        public static void UpdateStatusInfo(TwitterStatus status,
                                            Action<StatusModel> ifCacheIsAlive,
                                            Action<TwitterStatus> ifCacheIsDead)
        {
            WeakReference wr;
            lock (_staticCacheLock)
            {
                _staticCache.TryGetValue(status.Id, out wr);
            }
            if (wr != null)
            {
                var target = (StatusModel)wr.Target;
                if (wr.IsAlive)
                {
                    ifCacheIsAlive(target);
                    StatusStore.Store(target.Status);
                    return;
                }
            }
            StatusStore.Get(status.Id)
                       .DefaultIfEmpty(status)
                       .Subscribe(s =>
                       {
                           ifCacheIsDead(s);
                           StatusStore.Store(s);
                       });
        }

        public void AddFavoritedUser(long userId)
        {
            StoreHelper.GetUser(userId)
                       .Subscribe(AddFavoritedUser);
        }

        public async void AddFavoritedUser(TwitterUser user)
        {
            if (this.Status.RetweetedOriginal != null)
            {
                var status = await Get(this.Status.RetweetedOriginal);
                status.AddFavoritedUser(user);
            }
            else
            {
                var added = false;
                lock (_favoritedsLock)
                {
                    if (!_favoritedUsersDic.ContainsKey(user.Id))
                    {
                        _favoritedUsersDic.Add(user.Id, user);
                        Status.FavoritedUsers = Status.FavoritedUsers
                                                      .Guard()
                                                      .Append(user.Id)
                                                      .Distinct()
                                                      .ToArray();
                        added = true;
                    }
                }
                if (added)
                {
                    _favoritedUsers.Add(user);
                    StatusStore.Store(Status);
                }
            }
        }

        public async void RemoveFavoritedUser(long id)
        {
            if (this.Status.RetweetedOriginal != null)
            {
                var status = await Get(this.Status.RetweetedOriginal);
                status.RemoveFavoritedUser(id);
            }
            else
            {
                TwitterUser remove;
                lock (_favoritedsLock)
                {
                    if (_favoritedUsersDic.TryGetValue(id, out remove))
                    {
                        _favoritedUsersDic.Remove(id);
                        Status.FavoritedUsers = Status.FavoritedUsers.Except(new[] { id }).ToArray();
                    }
                }
                if (remove != null)
                {
                    _favoritedUsers.Remove(remove);
                    StatusStore.Store(Status);
                }
            }
        }

        public void AddRetweetedUser(long userId)
        {
            StoreHelper.GetUser(userId).Subscribe(AddRetweetedUser);
        }

        public async void AddRetweetedUser(TwitterUser user)
        {
            if (this.Status.RetweetedOriginal != null)
            {
                var status = await Get(this.Status.RetweetedOriginal);
                status.AddRetweetedUser(user);
            }
            else
            {
                var added = false;
                lock (_retweetedsLock)
                {
                    if (!_retweetedUsersDic.ContainsKey(user.Id))
                    {
                        _retweetedUsersDic.Add(user.Id, user);
                        Status.RetweetedUsers = Status.RetweetedUsers.Guard()
                                                      .Append(user.Id)
                                                      .Distinct()
                                                      .ToArray();
                        added = true;
                    }
                }
                if (added)
                {
                    _retweetedUsers.Add(user);
                    // update persistent info
                    StatusStore.Store(Status);
                }
            }
        }

        public async void RemoveRetweetedUser(long id)
        {
            if (this.Status.RetweetedOriginal != null)
            {
                var status = await Get(this.Status.RetweetedOriginal);
                status.RemoveRetweetedUser(id);
            }
            else
            {
                TwitterUser remove;
                lock (_retweetedsLock)
                {
                    if (_retweetedUsersDic.TryGetValue(id, out remove))
                    {
                        _retweetedUsersDic.Remove(id);
                        Status.RetweetedUsers = Status.RetweetedUsers.Except(new[] { id }).ToArray();
                    }
                }
                if (remove != null)
                {
                    _retweetedUsers.Remove(remove);
                    // update persistent info
                    StatusStore.Store(Status);
                }
            }
        }

        public bool IsFavorited(params long[] ids)
        {
            if (ids.Length == 0) return false;
            if (this.Status.RetweetedOriginal != null)
            {
                throw new NotSupportedException("You must create another model indicating RetweetedOriginal status.");
            }
            lock (_favoritedsLock)
            {
                return ids.All(_favoritedUsersDic.ContainsKey);
            }
        }

        public bool IsRetweeted(params long[] ids)
        {
            if (ids.Length == 0) return false;
            if (this.Status.RetweetedOriginal != null)
            {
                throw new NotSupportedException("You must create another model indicating RetweetedOriginal status.");
            }
            lock (_retweetedsLock)
            {
                return ids.All(_retweetedUsersDic.ContainsKey);
            }
        }

        public IEnumerable<AuthenticateInfo> GetSuitableReplyAccount()
        {
            var uid = Status.InReplyToUserId.GetValueOrDefault();
            if (Status.StatusType == StatusType.DirectMessage)
                uid = Status.Recipient.Id;
            var info = AccountsStore.GetAccountSetting(uid);
            if (info != null)
            {
                return new[] { BacktrackFallback(info.AuthenticateInfo) };
            }
            return null;
        }

        public static AuthenticateInfo BacktrackFallback(AuthenticateInfo info)
        {
            if (!Setting.IsBacktrackFallback.Value)
                return info;
            var cinfo = info;
            while (true)
            {
                var backtrack = AccountsStore.Accounts
                                                        .FirstOrDefault(i => i.FallbackNext == cinfo.Id);
                if (backtrack == null)
                    return cinfo;
                if (backtrack.UserId == info.Id)
                    return info;
                cinfo = backtrack.AuthenticateInfo;
            }
        }

        public override bool Equals(object obj)
        {
            StatusModel another;
            if (obj == null || (another = obj as StatusModel) == null)
            {
                return false;
            }

            return this.Status.Id == another.Status.Id;
        }

        public override int GetHashCode()
        {
            return this.Status.GetHashCode();
        }
    }
}
