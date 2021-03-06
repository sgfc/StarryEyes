﻿using System;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using Livet;
using StarryEyes.Models;
using StarryEyes.Models.Tab;

namespace StarryEyes.ViewModels.WindowParts.Timelines
{
    public class ColumnViewModel : ViewModel
    {
        private readonly MainAreaViewModel _parent;
        private readonly ColumnModel _model;
        private readonly ReadOnlyDispatcherCollectionRx<TabViewModel> _tabs;
        public ReadOnlyDispatcherCollectionRx<TabViewModel> Tabs
        {
            get { return _tabs; }
        }

        public MainAreaViewModel Parent
        {
            get { return _parent; }
        }

        public ColumnModel Model
        {
            get { return _model; }
        }

        public TabViewModel FocusedTab
        {
            get { return _tabs != null && _tabs.Count > 0 ? _tabs[_model.CurrentFocusTabIndex] : null; }
            set
            {
                _model.CurrentFocusTabIndex = _tabs.IndexOf(value);
                _tabs.ForEach(item => item.UpdateFocus());
                RaisePropertyChanged();
            }
        }

        #region DragDrop Control

        private Livet.Commands.ViewModelCommand _dragDropStartCommand;

        public Livet.Commands.ViewModelCommand DragDropStartCommand
        {
            get
            {
                return _dragDropStartCommand ??
                       (_dragDropStartCommand = new Livet.Commands.ViewModelCommand(DragDropStart));
            }
        }

        public void DragDropStart()
        {
            Parent.StartDragDrop();
        }

        private Livet.Commands.ViewModelCommand _dragDropFinishCommand;

        public Livet.Commands.ViewModelCommand DragDropFinishCommand
        {
            get
            {
                return _dragDropFinishCommand ??
                       (_dragDropFinishCommand = new Livet.Commands.ViewModelCommand(DragDropFinish));
            }
        }

        public void DragDropFinish()
        {
            Parent.FinishDragDrop();
        }

        private bool _isDragDropping;
        public bool IsDragDropping
        {
            get { return _isDragDropping; }
            set
            {
                _isDragDropping = value;
                RaisePropertyChanged();
            }
        }

        private DropAcceptDescription _leftAccept;

        private DropAcceptDescription _rightAccept;

        public DropAcceptDescription LeftAcceptDescription
        {
            get
            {
                if (_leftAccept == null)
                {
                    _leftAccept = new DropAcceptDescription();
                    _leftAccept.DragOver += this.AcceptTabViewModel;
                    _leftAccept.DragDrop += e => this.DropCreateNewColumn(e, false);
                }
                return _leftAccept;
            }
        }

        public DropAcceptDescription RightAcceptDescription
        {
            get
            {
                if (_rightAccept == null)
                {
                    _rightAccept = new DropAcceptDescription();
                    _rightAccept.DragOver += this.AcceptTabViewModel;
                    _rightAccept.DragDrop += e => this.DropCreateNewColumn(e, true);
                }
                return _rightAccept;
            }
        }

        private void AcceptTabViewModel(DragEventArgs e)
        {
            var data = e.Data.GetData(typeof(TabViewModel)) as TabViewModel;
            if (data != null)
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void DropCreateNewColumn(DragEventArgs e, bool createNext)
        {
            var data = e.Data.GetData(typeof(TabViewModel)) as TabViewModel;
            if (data == null) return;
            int fromColumnIndex, fromTabIndex;
            // get index
            var curindex = TabManager.FindColumnIndex(this.Model);
            if (curindex == -1) return;
            if (!TabManager.FindColumnTabIndex(data.Model, out fromColumnIndex, out fromTabIndex))
            {
                return;
            }
            var item = TabManager.Columns[fromColumnIndex].Tabs[fromTabIndex];
            TabManager.Columns[fromColumnIndex].RemoveTab(fromTabIndex);
            var index = createNext ? curindex + 1 : curindex;
            TabManager.CreateColumn(index, item);
            var focusTarget = TabManager.Columns[index];
            TabManager.GCColumn();
            var focusTargetIndex = TabManager.FindColumnIndex(focusTarget);
            if (focusTargetIndex >= 0)
            {
                this.Parent.Columns[focusTargetIndex].Focus();
            }
        }

        #endregion

        private DropAcceptDescription _description;
        public DropAcceptDescription DropAcceptDescription
        {
            get
            {
                if (_description == null)
                {
                    _description = new DropAcceptDescription();
                    _description.DragOver += e =>
                    {
                        var data = e.Data.GetData(typeof(TabViewModel)) as TabViewModel;
                        if (data != null)
                        {
                            e.Effects = DragDropEffects.Move;
                        }
                    };
                    _description.DragDrop += e =>
                    {
                        var data = e.Data.GetData(typeof(TabViewModel)) as TabViewModel;
                        if (data == null) return;
                        var dataPreviousParent = data.Parent;
                        var source = e.OriginalSource as FrameworkElement;
                        if (source == null) return;
                        int destColumnIndex, destTabIndex;
                        var tvm = source.DataContext as TabViewModel;
                        var cvm = source.DataContext as ColumnViewModel;
                        // find destination
                        if (tvm != null)
                        {
                            if (tvm == data) return;
                            destColumnIndex = TabManager.FindColumnIndex(this.Model);
                            destTabIndex = TabManager.FindTabIndex(tvm.Model, destColumnIndex);
                        }
                        else if (cvm != null)
                        {
                            destColumnIndex = TabManager.FindColumnIndex(this.Model);
                            destTabIndex = this.Model.Tabs.Count;
                        }
                        else
                        {
                            return;
                        }

                        int fromColumnIndex, fromTabIndex;
                        // get index
                        if (!TabManager.FindColumnTabIndex(data.Model, out fromColumnIndex, out fromTabIndex))
                        {
                            return;
                        }
                        // ensure move point
                        if (fromColumnIndex == destColumnIndex)
                        {
                            if (fromTabIndex < destTabIndex) destTabIndex--;
                            if (destTabIndex == -1) destTabIndex = 0;
                            if (destTabIndex == fromTabIndex) return;
                        }
                        // move tab
                        TabManager.MoveTo(fromColumnIndex, fromTabIndex, destColumnIndex, destTabIndex);
                        // update previous column's focus
                        if (this.Model != dataPreviousParent.Model && dataPreviousParent.Model.Tabs.Count > 0)
                        {
                            if (dataPreviousParent.Model.CurrentFocusTabIndex >= dataPreviousParent.Tabs.Count)
                            {
                                dataPreviousParent.Model.CurrentFocusTabIndex--;
                            }
                            else
                            {
                                // invoke update handler
                                dataPreviousParent.Model.CurrentFocusTabIndex =
                                    dataPreviousParent.Model.CurrentFocusTabIndex;
                            }
                        }
                        this.Model.CurrentFocusTabIndex = destTabIndex;
                        this.Focus();
                    };
                }
                return _description;
            }
        }

        private void UpdateFocusFromModel(int newFocus)
        {
            DispatcherHolder.Enqueue(() =>
            {
                _tabs.ForEach(item => item.UpdateFocus());
                RaisePropertyChanged(() => FocusedTab);
            });
        }

        public ColumnViewModel(MainAreaViewModel parent, ColumnModel model)
        {
            _parent = parent;
            _model = model;
            this.CompositeDisposable.Add(
                _tabs = ViewModelHelperRx.CreateReadOnlyDispatcherCollectionRx(
                    model.Tabs,
                    _ => new TabViewModel(this, _),
                    DispatcherHelper.UIDispatcher));
            this.CompositeDisposable.Add(
                Observable.FromEvent(
                h => _model.CurrentFocusTabChanged += h,
                h => _model.CurrentFocusTabChanged -= h)
                .Select(_ => _model.CurrentFocusTabIndex)
                .Subscribe(UpdateFocusFromModel));
            if (_tabs.Count > 0)
            {
                FocusedTab = _tabs[0];
            }
        }

        public bool IsFocused
        {
            get { return _parent.FocusedColumn == this; }
        }

        internal void UpdateFocus()
        {
            this.RaisePropertyChanged(() => IsFocused);
        }

        public void Focus()
        {
            _parent.FocusedColumn = this;
        }

        internal void CloseTab(TabViewModel tab)
        {
            _parent.CloseTab(this, tab);
        }

        #region CreateNewTabCommand
        private Livet.Commands.ViewModelCommand _createNewTabCommand;

        public Livet.Commands.ViewModelCommand CreateNewTabCommand
        {
            get
            {
                return _createNewTabCommand ??
                       (_createNewTabCommand = new Livet.Commands.ViewModelCommand(CreateNewTab));
            }
        }

        const string DefaultQueryString = "from local where ()";
        public void CreateNewTab()
        {
            var creating = new TabModel(string.Empty, DefaultQueryString);
            IDisposable subscribe = null;
            subscribe = Observable.FromEvent<bool>(
                h => creating.ConfigurationUpdated += h,
                h => creating.ConfigurationUpdated -= h)
                                  .Subscribe(_ =>
                                  {
                                      if (subscribe != null) subscribe.Dispose();
                                      // configuration completed.
                                      if (String.IsNullOrEmpty(creating.Name) &&
                                          creating.FilterQueryString == DefaultQueryString) return;
                                      this.Model.CreateTab(creating);
                                  });
            MainWindowModel.ShowTabConfigure(creating);
        }
        #endregion

        #region RestoreLastClosedTabCommand
        private Livet.Commands.ViewModelCommand _restoreLastClosedTabCommand;

        public Livet.Commands.ViewModelCommand RestoreLastClosedTabCommand
        {
            get
            {
                return _restoreLastClosedTabCommand ??
                       (_restoreLastClosedTabCommand = new Livet.Commands.ViewModelCommand(RestoreLastClosedTab));
            }
        }

        public void RestoreLastClosedTab()
        {
            Focus();
            TabManager.ReviveTab();
        }
        #endregion
    }
}
