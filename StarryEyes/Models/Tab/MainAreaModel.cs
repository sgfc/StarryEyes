﻿using System;
using System.Collections.Generic;
using System.Linq;
using Livet;
using StarryEyes.Nightmare.Windows;
using StarryEyes.Settings;

namespace StarryEyes.Models.Tab
{
    public static class MainAreaModel
    {
        private static readonly Stack<TabModel> _closedTabsStack = new Stack<TabModel>();

        private static readonly ObservableSynchronizedCollection<ColumnModel> _columns =
            new ObservableSynchronizedCollection<ColumnModel>();

        private static int _currentFocusColumnIndex;

        public static event Action OnCurrentFocusColumnChanged;

        public static ObservableSynchronizedCollection<ColumnModel> Columns
        {
            get { return _columns; }
        }

        /// <summary>
        /// Load from configuration.
        /// </summary>
        internal static void Load()
        {
            Setting.Columns
                .Select(c => new ColumnModel(c.Tabs.Select(d => d.ToTabModel()).ToArray()))
                .ForEach(_columns.Add);
            App.RaiseUserInterfaceReady();
        }

        /// <summary>
        /// Save tab info to configuration file.
        /// </summary>
        public static void Save()
        {
            Setting.Columns = Columns.Select(c => c.Tabs.Select(t => new TabDescription(t)))
                                     .Select(ts => new ColumnDescription { Tabs = ts.ToArray() }).ToArray();
        }

        /// <summary>
        /// Get current focusing tab model.
        /// </summary>
        public static TabModel CurrentFocusTab
        {
            get
            {
                if (_currentFocusColumnIndex < 0 || _currentFocusColumnIndex >= _columns.Count)
                    return null;
                var col = _columns[_currentFocusColumnIndex];
                var cti = col.CurrentFocusTabIndex;
                if (cti < 0 || cti >= col.Tabs.Count)
                    return null;
                return col.Tabs[cti];
            }
        }

        /// <summary>
        ///     Current focused column index
        /// </summary>
        public static int CurrentFocusColumnIndex
        {
            get { return _currentFocusColumnIndex; }
            set
            {
                _currentFocusColumnIndex = value;
                var col = _columns[_currentFocusColumnIndex];
                InputAreaModel.NotifyChangeFocusingTab(col.Tabs[col.CurrentFocusTabIndex]);
                Action handler = OnCurrentFocusColumnChanged;
                if (handler != null) handler();
            }
        }

        /// <summary>
        ///     Check revivable tab is existed in closed tabs stack.
        /// </summary>
        public static bool IsRevivableTabExsted
        {
            get { return _closedTabsStack.Count > 0; }
        }

        /// <summary>
        ///     Get column info datas for persistence.
        /// </summary>
        /// <returns></returns>
        internal static IEnumerable<ColumnInfo> GetColumnInfoData()
        {
            return _columns.Select(c => new ColumnInfo(c.Tabs));
        }

        /// <summary>
        ///     Find tab info where existed.
        /// </summary>
        /// <param name="info">tab info</param>
        /// <param name="colIndex">column index</param>
        /// <param name="tabIndex">tab index</param>
        public static void GetTabInfoIndexes(TabModel info, out int colIndex, out int tabIndex)
        {
            for (int ci = 0; ci < _columns.Count; ci++)
            {
                for (int ti = 0; ti < _columns[ci].Tabs.Count; ti++)
                {
                    if (_columns[ci].Tabs[ti] == info)
                    {
                        colIndex = ci;
                        tabIndex = ti;
                        return;
                    }
                }
            }
            throw new ArgumentException("specified TabInfo was not found.");
        }

        /// <summary>
        /// </summary>
        /// <param name="info"></param>
        /// <param name="columnIndex"></param>
        /// <param name="tabIndex"></param>
        public static void MoveTo(TabModel info, int columnIndex, int tabIndex)
        {
            int fci, fti;
            GetTabInfoIndexes(info, out fci, out fti);
            MoveTo(fci, fti, columnIndex, tabIndex);
        }

        /// <summary>
        ///     Move specified tab.
        /// </summary>
        public static void MoveTo(int fromColumnIndex, int fromTabIndex, int destColumnIndex, int destTabIndex)
        {
            if (fromColumnIndex == destColumnIndex)
            {
                // in-column moving
                _columns[fromColumnIndex].Tabs.Move(fromTabIndex, fromColumnIndex);
            }
            else
            {
                TabModel tab = _columns[fromColumnIndex].Tabs[fromTabIndex];
                _columns[fromColumnIndex].Tabs.RemoveAt(fromTabIndex);
                _columns[destTabIndex].Tabs.Insert(destTabIndex, tab);
            }
            Save();
        }

        /// <summary>
        ///     Create tab
        /// </summary>
        /// <param name="info">tab information</param>
        public static void CreateTab(TabModel info)
        {
            CreateTab(info, _currentFocusColumnIndex);
            Save();
        }

        /// <summary>
        ///     Create tab into specified column
        /// </summary>
        public static void CreateTab(TabModel info, int columnIndex)
        {
            // ReSharper disable LocalizableElement
            if (columnIndex > _columns.Count) // column index is only for existed or new column
                throw new ArgumentOutOfRangeException(
                    "columnIndex",
                    "currently " + _columns.Count +
                    " columns are existed. so, you can't set this parameter as " +
                    columnIndex + ".");
            // ReSharper restore LocalizableElement
            if (columnIndex == _columns.Count)
            {
                // create new
                CreateColumn(info);
            }
            else
            {
                _columns[columnIndex].CreateTab(info);
                Save();
            }
        }

        /// <summary>
        ///     Create column
        /// </summary>
        /// <param name="info">initial created tab</param>
        public static void CreateColumn(params TabModel[] info)
        {
            _columns.Add(new ColumnModel(info));
            Save();
        }

        /// <summary>
        ///     Close a tab.
        /// </summary>
        public static void CloseTab(int colIndex, int tabIndex)
        {
            TabModel ti = _columns[colIndex].Tabs[tabIndex];
            ti.Deactivate();
            _closedTabsStack.Push(ti);
            _columns[colIndex].Tabs.RemoveAt(tabIndex);
            Save();
        }

        /// <summary>
        ///     Revive tab from closed tabs stack.
        /// </summary>
        public static void ReviveTab()
        {
            TabModel ti = _closedTabsStack.Pop();
            CreateTab(ti);
        }

        /// <summary>
        ///     Clear closed tabs stack.
        /// </summary>
        public static void CrearClosedTabsStack()
        {
            _closedTabsStack.Clear();
        }
    }
}