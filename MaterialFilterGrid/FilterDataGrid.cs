#region (c) 2022 Gilles Macabies All right reserved

// Author     : Gilles Macabies
// Solution   : FilterDataGrid
// Projet     : FilterDataGrid.Net5.0
// File       : FilterDataGrid.cs
// Created    : 06/03/2022
// 

#endregion

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace FilterDataGrid
{
    /// <summary>
    ///     Implementation of Datagrid
    /// </summary>
    public sealed class FilterDataGrid : DataGrid, INotifyPropertyChanged
    {
        #region Constructors

        /// <summary>
        ///     FilterDataGrid constructor
        /// </summary>
        public FilterDataGrid()
        {
            DefaultStyleKey = typeof(FilterDataGrid);

            Debug.WriteLineIf(DebugMode, "Constructor");

            // load resources
            var resourcesDico = new ResourceDictionary
            {
                Source = new Uri("/MaterialFilterGrid;component/Themes/FilterDataGrid.xaml", UriKind.RelativeOrAbsolute)
            };
            SetValue(AvailableFilterTypesProperty, new List<FilterCondition>
            {
                FilterCondition.None,
                FilterCondition.Contains,
                FilterCondition.Equals,
                FilterCondition.NotEquals,
                FilterCondition.GreaterThan,
                FilterCondition.LessThan,
                FilterCondition.GreaterThanOrEqual,
                FilterCondition.LessThanOrEqual
            });
            Resources.MergedDictionaries.Add(resourcesDico);

            // initial popup size
            popUpSize = new Point
            {
                X = (double)TryFindResource("PopupWidth"),
                Y = (double)TryFindResource("PopupHeight")
            };

            CommandBindings.Add(new CommandBinding(ShowFilter, ShowFilterCommand, CanShowFilter));
            CommandBindings.Add(new CommandBinding(ApplyFilter, ApplyFilterCommand, CanApplyFilter)); // Ok
            CommandBindings.Add(new CommandBinding(CancelFilter, CancelFilterCommand));
            CommandBindings.Add(new CommandBinding(RemoveFilter, RemoveFilterCommand, CanRemoveFilter));
            CommandBindings.Add(new CommandBinding(IsChecked, CheckedAllCommand));
            CommandBindings.Add(new CommandBinding(ClearSearchBox, ClearSearchBoxClick));

            // Conditional filter
            CommandBindings.Add(new CommandBinding(ShowFilterValueInput, ShowFilterValueInputCommand, CanShowFilterValueInput));
            CommandBindings.Add(new CommandBinding(AcceptFilterValue, AcceptFilterValueCommand, CanAcceptFilterValue));
            CommandBindings.Add(new CommandBinding(CancelFilterValue, CancelFilterValueCommand));
        }


        #endregion Constructors

        #region Command

        // Public properties for commands
        public static readonly ICommand ShowFilterValueInput = new RoutedCommand();
        public static readonly ICommand AcceptFilterValue = new RoutedCommand();
        public static readonly ICommand CancelFilterValue = new RoutedCommand();
        // ------

        public static readonly ICommand ApplyFilter = new RoutedCommand();

        public static readonly ICommand CancelFilter = new RoutedCommand();

        public static readonly ICommand ClearSearchBox = new RoutedCommand();

        public static readonly ICommand IsChecked = new RoutedCommand();

        public static readonly ICommand RemoveFilter = new RoutedCommand();

        public static readonly ICommand ShowFilter = new RoutedCommand();


        #endregion Command

        #region Public DependencyProperty

        public static readonly DependencyProperty FilterDialogTitleProperty =
            DependencyProperty.Register(nameof(FilterDialogTitle), typeof(string), typeof(FilterDataGrid),
                new PropertyMetadata("Enter Filter Value"));

        public static readonly DependencyProperty ShowValueInputProperty =
            DependencyProperty.Register(nameof(ShowValueInput), typeof(bool), typeof(FilterDataGrid), new PropertyMetadata(true));

        public static readonly DependencyProperty FilterValueProperty =
            DependencyProperty.Register(
                nameof(FilterValue),
                typeof(string),
                typeof(FilterDataGrid),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnFilterValueChanged));


        public static readonly DependencyProperty SelectedFilterProperty =
            DependencyProperty.Register(
                nameof(SelectedFilter),
                typeof(FilterCondition),
                typeof(FilterDataGrid),
                new PropertyMetadata(FilterCondition.None, OnSelectFilterChanged));


        public static readonly DependencyProperty AvailableFilterTypesProperty =
            DependencyProperty.Register(nameof(AvailableFilterTypes), typeof(IEnumerable<FilterCondition>), typeof(FilterDataGrid), new PropertyMetadata(null));

        public static readonly DependencyProperty ShowFilterTypesProperty =
            DependencyProperty.Register(nameof(ShowFilterTypes), typeof(bool), typeof(FilterDataGrid), new PropertyMetadata(true));


        /// <summary>
        ///     Excluded Fields on AutoColumn
        /// </summary>
        public static readonly DependencyProperty ExcludeFieldsProperty =
            DependencyProperty.Register("ExcludeFields",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata(""));

        /// <summary>
        ///     date format displayed
        /// </summary>
        public static readonly DependencyProperty DateFormatStringProperty =
            DependencyProperty.Register("DateFormatString",
                typeof(string),
                typeof(FilterDataGrid),
                new PropertyMetadata("d"));

        /// <summary>
        ///     Language displayed
        /// </summary>
        public static readonly DependencyProperty FilterLanguageProperty =
            DependencyProperty.Register("FilterLanguage",
                typeof(Local),
                typeof(FilterDataGrid),
                new PropertyMetadata(Local.English));

        /// <summary>
        ///     Show elapsed time in status bar
        /// </summary>
        public static readonly DependencyProperty ShowElapsedTimeProperty =
            DependencyProperty.Register("ShowElapsedTime",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Show statusbar
        /// </summary>
        public static readonly DependencyProperty ShowStatusBarProperty =
            DependencyProperty.Register("ShowStatusBar",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        /// <summary>
        ///     Show Rows Count
        /// </summary>
        public static readonly DependencyProperty ShowRowsCountProperty =
            DependencyProperty.Register("ShowRowsCount",
                typeof(bool),
                typeof(FilterDataGrid),
                new PropertyMetadata(false));

        #endregion Public DependencyProperty

        #region Public Event

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler Sorted;

        #endregion Public Event

        #region Private Fields

        private static readonly ConcurrentDictionary<string, NumberCache> NumericCache = new ConcurrentDictionary<string, NumberCache>();

        private class NumberCache
        {
            public double NumericValue { get; set; }
            public bool IsValid { get; set; }
        }

        private Stopwatch stopWatchFilter = new Stopwatch();
        private bool pending;
        private bool search;
        private Button button;
        private const bool DebugMode = false;
        private Cursor cursor;
        private int searchLength;
        private double minHeight;
        private double minWidth;
        private double sizableContentHeight;
        private double sizableContentWidth;
        private Grid sizableContentGrid;

        private List<string> excludedFields;
        private List<FilterItemDate> treeview;
        private List<FilterItem> listBoxItems;

        private Point popUpSize;
        private Popup popup;

        private string fieldName;
        private string lastFilter;
        private string searchText;
        private TextBox searchTextBox;
        private Thumb thumb;

        private TimeSpan elased;

        private Type collectionType;
        private Type fieldType;

        private bool startsWith;
        private object currentColumn;

        private readonly Dictionary<string, Predicate<object>> criteria = new Dictionary<string, Predicate<object>>();
        private const StringComparison IgnoreCase = StringComparison.OrdinalIgnoreCase;
        private static readonly ConcurrentDictionary<string, object> ParsedFilterValueCache = new ConcurrentDictionary<string, object>();

        private Popup filterValuePopup;

        #endregion Private Fields

        #region Public Properties

        /// <summary>
        ///     Filter dialog title
        /// </summary>
        public string FilterDialogTitle
        {
            get => (string)GetValue(FilterDialogTitleProperty);
            set => SetValue(FilterDialogTitleProperty, value);
        }

        /// <summary>
        ///     Selected Condition
        /// </summary>
        public FilterCondition SelectedFilter
        {
            get => (FilterCondition)GetValue(SelectedFilterProperty);
            set => SetValue(SelectedFilterProperty, value);
        }

        /// <summary>
        ///     Available filter conditions based on type
        /// </summary>
        public IEnumerable<FilterCondition> AvailableFilterTypes
        {
            get => (IEnumerable<FilterCondition>)GetValue(AvailableFilterTypesProperty);
            set => SetValue(AvailableFilterTypesProperty, value);
        }

        /// <summary>
        /// Show filter types
        /// </summary>
        public bool ShowFilterTypes
        {
            get => (bool)GetValue(ShowFilterTypesProperty);
            set => SetValue(ShowFilterTypesProperty, value);
        }

        /// <summary>
        ///     FilterValue
        /// </summary>
        public string FilterValue
        {
            get => (string)GetValue(FilterValueProperty);
            set => SetValue(FilterValueProperty, value);
        }

        /// <summary>
        ///    Visibility
        /// </summary>
        public bool ShowValueInput
        {
            get => (bool)GetValue(ShowValueInputProperty);
            set => SetValue(ShowValueInputProperty, value);
        }

        /// <summary>
        ///     Excluded Fileds
        /// </summary>
        public string ExcludeFields
        {
            get => (string)GetValue(ExcludeFieldsProperty);
            set => SetValue(ExcludeFieldsProperty, value);
        }

        /// <summary>
        ///     String begins with the specified character. Used in popup searchBox
        /// </summary>
        public bool StartsWith
        {
            get => startsWith;
            set
            {
                startsWith = value;
                OnPropertyChanged();

                // refresh filter
                if (!string.IsNullOrEmpty(searchText)) ItemCollectionView.Refresh();
            }
        }

        /// <summary>
        ///     Date format displayed
        /// </summary>
        public string DateFormatString
        {
            get => (string)GetValue(DateFormatStringProperty);
            set => SetValue(DateFormatStringProperty, value);
        }

        /// <summary>
        ///     Elapsed time
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get => elased;
            set
            {
                elased = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        ///     Language
        /// </summary>
        public Local FilterLanguage
        {
            get => (Local)GetValue(FilterLanguageProperty);
            set => SetValue(FilterLanguageProperty, value);
        }

        /// <summary>
        ///     Display items count
        /// </summary>
        public int ItemsSourceCount { get; set; }

        /// <summary>
        ///     Show elapsed time in status bar
        /// </summary>
        public bool ShowElapsedTime
        {
            get => (bool)GetValue(ShowElapsedTimeProperty);
            set => SetValue(ShowElapsedTimeProperty, value);
        }

        /// <summary>
        ///     Show status bar
        /// </summary>
        public bool ShowStatusBar
        {
            get => (bool)GetValue(ShowStatusBarProperty);
            set => SetValue(ShowStatusBarProperty, value);
        }

        /// <summary>
        ///     Show rows count
        /// </summary>
        public bool ShowRowsCount
        {
            get => (bool)GetValue(ShowRowsCountProperty);
            set => SetValue(ShowRowsCountProperty, value);
        }

        /// <summary>
        ///     Instance of Loc
        /// </summary>
        public Loc Translate { get; private set; }

        /// <summary>
        ///     Row header size when ShowRowsCount is true
        /// </summary>
        public double RowHeaderSize { get; set; }

        /// <summary>
        /// Treeview ItemsSource
        /// </summary>
        public List<FilterItemDate> TreeviewItems
        {
            get => treeview ?? new List<FilterItemDate>();
            set
            {
                treeview = value;
                OnPropertyChanged(nameof(TreeviewItems));
            }
        }

        /// <summary>
        /// ListBox ItemsSource
        /// </summary>
        public List<FilterItem> ListBoxItems
        {
            get => listBoxItems ?? new List<FilterItem>();
            set
            {
                listBoxItems = value;
                OnPropertyChanged(nameof(ListBoxItems));
            }
        }

        #endregion Public Properties

        #region Private Properties

        private FilterCommon CurrentFilter { get; set; }
        private ICollectionView CollectionViewSource { get; set; }
        private ICollectionView ItemCollectionView { get; set; }
        private List<FilterCommon> GlobalFilterList { get; } = new List<FilterCommon>();


        /// <summary>
        ///     DatagridFilterStyleKey ComponentResourceKey
        /// </summary>
        private IEnumerable<FilterItem> PopupViewItems =>
            ItemCollectionView?.OfType<FilterItem>().Skip(1) ?? new List<FilterItem>();

        /// <summary>
        ///     DataGridStyle, only internal usage
        /// </summary>
        private IEnumerable<FilterItem> SourcePopupViewItems =>
            ItemCollectionView?.SourceCollection.OfType<FilterItem>().Skip(1) ?? new List<FilterItem>();

        #endregion Private Properties

        #region Protected Methods

        // CALL ORDER :
        // Constructor
        // OnInitialized
        // OnItemsSourceChanged

        /// <summary>
        ///     Initialize datagrid
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInitialized(EventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "OnInitialized");

            base.OnInitialized(e);

            try
            {
                // FilterLanguage : default : 0 (english)
                Translate = new Loc { Language = FilterLanguage };

                // Show row count
                RowHeaderWidth = ShowRowsCount ? RowHeaderWidth > 0 ? RowHeaderWidth : double.NaN : 0;

                // fill excluded Fields list with values
                if (AutoGenerateColumns)
                    excludedFields = ExcludeFields.Split(',').Select(p => p.Trim()).ToList();

                // sorting event
                Sorted += OnSorted;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnInitialized : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Auto generated column, set templateHeader
        /// </summary>
        /// <param name="e"></param>
        protected override void OnAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "OnAutoGeneratingColumn");

            base.OnAutoGeneratingColumn(e);

            try
            {
                if (e.Column.GetType() != typeof(System.Windows.Controls.DataGridTextColumn)) return;

                var column = new DataGridTextColumn
                {
                    Binding = new Binding(e.PropertyName) { ConverterCulture = Translate.Culture /* StringFormat */ },
                    FieldName = e.PropertyName,
                    Header = e.Column.Header.ToString(),
                    IsColumnFiltered = false
                };

                // get type
                fieldType = Nullable.GetUnderlyingType(e.PropertyType) ?? e.PropertyType;

                // apply the format string provided
                if (fieldType == typeof(DateTime) && !string.IsNullOrEmpty(DateFormatString))
                    column.Binding.StringFormat = DateFormatString;

                // add DataGridHeaderTemplate template if not excluded
                if (excludedFields?.FindIndex(c =>
                        string.Equals(c, e.PropertyName, StringComparison.CurrentCultureIgnoreCase)) == -1)
                {
                    column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");
                    column.IsColumnFiltered = true;
                }

                e.Column = column;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnAutoGeneratingColumn : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     The source of the Datagrid items has been changed (refresh or on loading)
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            Debug.WriteLineIf(DebugMode, "OnItemsSourceChanged");

            base.OnItemsSourceChanged(oldValue, newValue);

            try
            {
                if (newValue == null) return;

                if (oldValue != null)
                {
                    // reset current filter, !important
                    CurrentFilter = null;

                    // reset GlobalFilterList list
                    GlobalFilterList.Clear();

                    // reset criteria List
                    criteria.Clear();

                    // free previous resource
                    CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());

                    // scroll to top on reload collection
                    var scrollViewer = GetTemplateChild("DG_ScrollViewer") as ScrollViewer;
                    scrollViewer?.ScrollToTop();
                }

                CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(ItemsSource);

                // set Filter, contribution : STEFAN HEIMEL
                if (CollectionViewSource.CanFilter) CollectionViewSource.Filter = Filter;

                ItemsSourceCount = Items.Count;
                ElapsedTime = new TimeSpan(0, 0, 0);
                OnPropertyChanged(nameof(ItemsSourceCount));

                // Calculate row header width
                if (ShowRowsCount)
                {
                    var txt = new TextBlock
                    {
                        Text = ItemsSourceCount.ToString(),
                        FontSize = FontSize,
                        FontFamily = FontFamily,
                        Margin = new Thickness(2.0)
                    };
                    txt.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    RowHeaderSize = Math.Ceiling(txt.DesiredSize.Width);
                    OnPropertyChanged(nameof(RowHeaderSize));
                }

                // get collection type
                if (ItemsSourceCount > 0)
                    // contribution : APFLKUACHA
                    collectionType = ItemsSource is ICollectionView collectionView
                        ? collectionView.SourceCollection?.GetType().GenericTypeArguments.FirstOrDefault()
                        : ItemsSource?.GetType().GenericTypeArguments.FirstOrDefault();

                // generating custom columns
                if (!AutoGenerateColumns && collectionType != null) GeneratingCustomsColumn();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnItemsSourceChanged : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Set the cursor to "Cursors.Wait" during a long sorting operation
        ///     https://stackoverflow.com/questions/8416961/how-can-i-be-notified-if-a-datagrid-column-is-sorted-and-not-sorting
        /// </summary>
        /// <param name="eventArgs"></param>
        protected override void OnSorting(DataGridSortingEventArgs eventArgs)
        {
            // Check if we're filtering or if popup is open
            if (pending || (popup?.IsOpen ?? false))
            {
                eventArgs.Handled = true;
                return;
            }

            try
            {
                // Ensure we disable sorting for the filtered column while the filter is active
                var column = eventArgs.Column;
                if (column != null &&
                    ((column is DataGridTextColumn textColumn && textColumn.FieldName == CurrentFilter?.FieldName) ||
                     (column is DataGridTemplateColumn templateColumn && templateColumn.FieldName == CurrentFilter?.FieldName)))
                {
                    if (CurrentFilter?.IsFiltered == true && !string.IsNullOrEmpty(CurrentFilter.FilterValue))
                    {
                        eventArgs.Handled = true;
                        return;
                    }
                }

                Mouse.OverrideCursor = Cursors.Wait;
                base.OnSorting(eventArgs);
                Sorted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.OnSorting error: {ex.Message}");
                eventArgs.Handled = true;
            }
            finally
            {
                ResetCursor();
            }
        }

        /// <summary>
        ///     Adding Rows count
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoadingRow(DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        #endregion Protected Methods

        #region Private Methods
        /// <summary>
        /// Get type of column (For conditional filtering)
        /// </summary>
        /// <param name="fieldType"></param>
        /// <returns></returns>
        private IEnumerable<FilterCondition> GetFilterTypesForField(Type fieldType)
        {
            var conditions = new List<FilterCondition> { FilterCondition.None };

            if (fieldType == typeof(string))
            {
                conditions.AddRange(new[]
                {
                    FilterCondition.Contains,
                    FilterCondition.Equals,
                    FilterCondition.NotEquals,
                    FilterCondition.StartsWith,
                    FilterCondition.EndsWith
                });
            }
            else if (fieldType == typeof(int) || fieldType == typeof(double) || fieldType == typeof(decimal))
            {
                conditions.AddRange(new[]
                {
                    FilterCondition.Equals,
                    FilterCondition.NotEquals,
                    FilterCondition.GreaterThan,
                    FilterCondition.LessThan,
                    FilterCondition.GreaterThanOrEqual,
                    FilterCondition.LessThanOrEqual
                });
            }
            else if (fieldType == typeof(DateTime))
            {
                conditions.AddRange(new[]
                {
                    FilterCondition.Equals,
                    FilterCondition.NotEquals,
                    FilterCondition.GreaterThan,
                    FilterCondition.LessThan,
                    FilterCondition.GreaterThanOrEqual,
                    FilterCondition.LessThanOrEqual
                });
            }

            return conditions;
        }

        /// <summary>
        /// Can hit ok of conditional filter dialog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanAcceptFilterValue(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !string.IsNullOrEmpty(FilterValue) && CurrentFilter != null;
        }

        /// <summary>
        /// Can show input field when click on one of the conditions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanShowFilterValueInput(object sender, CanExecuteRoutedEventArgs e)
        {
            // Can execute if there's a current filter and popup isn't already open
            e.CanExecute = CurrentFilter != null && (filterValuePopup?.IsOpen != true);
        }

        /// <summary>
        /// Display input field
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ShowFilterValueInputCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLine($"ShowFilterValueInputCommand Parameter Type: {e.Parameter?.GetType()}, Value: {e.Parameter}");

            try
            {
                if (!(e.Parameter is FilterCondition condition))
                    return;

                if (condition == FilterCondition.None)
                {
                    FilterValue = string.Empty;
                    if (CurrentFilter != null)
                    {
                        CurrentFilter.SelectedFilter = FilterCondition.None;
                        CurrentFilter.FilterValue = string.Empty;
                    }
                    HideFilterValueInput();
                    return;
                }

                var overlay = GetOverlayFromTemplate();
                if (overlay == null) return;

                FilterDialogTitle = $"{condition}";

                if (CurrentFilter != null)
                {
                    await Task.Run(() =>
                    {
                        CurrentFilter.SelectedFilter = condition;
                    });
                }

                overlay.Visibility = Visibility.Visible;

                // Focus the input box
                var inputField = GetValueInputFromTemplate();
                if (inputField != null)
                {
                    inputField.Focus();
                    Keyboard.Focus(inputField);
                    inputField.SelectAll();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.ShowFilterValueInputCommand error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Apply Conditional filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AcceptFilterValueCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var overlay = GetOverlayFromTemplate();
            if (overlay == null) return;

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                if (CurrentFilter != null)
                {
                    var currentColumnObj = currentColumn;
                    var filterValue = FilterValue;

                    await Task.Run(() =>
                    {
                        CurrentFilter.FilterValue = filterValue;
                        CurrentFilter.IsFiltered = true;

                        // Use the FilterCommon's method to add the criteria
                        CurrentFilter.AddFilterCriteria(criteria);
                    });

                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        FilterState.SetIsFiltered(button, true);

                        // Disable sorting for filtered column
                        if (currentColumnObj != null)
                        {
                            if (currentColumnObj is DataGridTextColumn textCol)
                                textCol.CanUserSort = false;
                            else if (currentColumnObj is DataGridTemplateColumn templateCol)
                                templateCol.CanUserSort = false;
                        }

                        CollectionViewSource?.Refresh();
                        overlay.Visibility = Visibility.Collapsed;

                        // If you want to keep the ApplyFilter execution
                        ApplyFilter.Execute(this);
                    }));
                }
                else
                {
                    overlay.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AcceptFilterValueCommand error: {ex.Message}");
                throw;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
        /// <summary>
        /// Hit cancel button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelFilterValueCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var overlay = GetOverlayFromTemplate();
            if (overlay != null)
            {
                FilterValue = string.Empty;
                if (CurrentFilter != null)
                {
                    CurrentFilter.SelectedFilter = FilterCondition.None;
                    CurrentFilter.FilterValue = string.Empty;
                }

                overlay.Visibility = Visibility.Collapsed;
            }
        }

        private void HideFilterValueInput()
        {
            var overlay = GetOverlayFromTemplate();
            if (overlay != null)
            {
                overlay.Visibility = Visibility.Collapsed;
            }
        }

        private Grid GetOverlayFromTemplate()
        {
            if (popup?.Child is FrameworkElement popupContent)
            {
                return VisualTreeHelpers.FindChild<Grid>(popupContent, "FilterValueInputOverlay");
            }
            return null;
        }

        private TextBox GetValueInputFromTemplate()
        {
            if (popup?.Child is FrameworkElement popupContent)
            {
                return VisualTreeHelpers.FindChild<TextBox>(popupContent, "PART_ValueInput");
            }
            return null;
        }

        private static void OnSelectFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterDataGrid grid && e.NewValue is FilterCondition newCondition)
            {
                // Always execute the command when condition changes
                ShowFilterValueInput.Execute(newCondition);
            }
        }

        /// <summary>
        ///     OnFilterValueChanged
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnFilterValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterDataGrid grid && grid.CurrentFilter != null)
            {
                grid.CurrentFilter.FilterValue = (string)e.NewValue;
            }
        }

        /// <summary>
        ///     Build the item tree
        /// </summary>
        /// <param name="dates"></param>
        /// <returns></returns>
        private List<FilterItemDate> BuildTree(IEnumerable<FilterItem> dates)
        {
            try
            {
                var tree = new List<FilterItemDate>
                {
                    new FilterItemDate
                    {
                        Label = Translate.All, Content = 0, Level = 0, Initialize = true, FieldType = fieldType
                    }
                };

                if (dates == null) return tree;

                // iterate over all items that are not null
                // INFO:
                // Initialize   : does not call the SetIsChecked method
                // IsChecked    : call the SetIsChecked method
                // (see the FilterItem class for more informations)

                var dateTimes = dates.ToList();

                foreach (var y in dateTimes.Where(c => c.Level == 1)
                             .Select(d => new
                             {
                                 ((DateTime)d.Content).Date,
                                 d.IsChecked,
                                 Item = d
                             })
                             .OrderBy(o => o.Date.Year)
                             .GroupBy(g => g.Date.Year)
                             .Select(year => new FilterItemDate
                             {
                                 Level = 1,
                                 Content = year.Key,
                                 Label = year.FirstOrDefault()?.Date.ToString("yyyy", Translate.Culture),
                                 Initialize = true, // default state
                                 FieldType = fieldType,

                                 Children = year.GroupBy(date => date.Date.Month)
                                     .Select(month => new FilterItemDate
                                     {
                                         Level = 2,
                                         Content = month.Key,
                                         Label = month.FirstOrDefault()?.Date.ToString("MMMM", Translate.Culture),
                                         Initialize = true, // default state
                                         FieldType = fieldType,

                                         Children = month.GroupBy(date => date.Date.Day)
                                             .Select(day => new FilterItemDate
                                             {
                                                 Level = 3,
                                                 Content = day.Key,
                                                 Label = day.FirstOrDefault()?.Date.ToString("dd", Translate.Culture),
                                                 Initialize = true, // default state
                                                 FieldType = fieldType,

                                                 // filter Item linked to the day,
                                                 // it propagates the status changes
                                                 Item = day.FirstOrDefault()?.Item,

                                                 Children = new List<FilterItemDate>()
                                             }).ToList()
                                     }).ToList()
                             }))
                {
                    // set parent and IsChecked property if uncheck Previous items
                    y.Children.ForEach(m =>
                    {
                        m.Parent = y;

                        m.Children.ForEach(d =>
                        {
                            d.Parent = m;

                            // set the state of the ischecked property based on the items already filtered (unchecked)
                            if (d.Item.IsChecked) return;

                            // call the SetIsChecked method of the FilterItemDate class
                            d.IsChecked = false;

                            // reset with new state (isChanged == false)
                            d.Initialize = d.IsChecked;
                        });
                        // reset with new state
                        m.Initialize = m.IsChecked;
                    });
                    // reset with new state
                    y.Initialize = y.IsChecked;
                    tree.Add(y);
                }
                // last empty item if exist in collection
                if (dateTimes.Any(d => d.Level == -1))
                {
                    var empty = dateTimes.FirstOrDefault(x => x.Level == -1);
                    if (empty != null)
                        tree.Add(
                            new FilterItemDate
                            {
                                Label = Translate.Empty, // translation
                                Content = null,
                                Level = -1,
                                FieldType = fieldType,
                                Initialize = empty.IsChecked,
                                Item = empty,
                                Children = new List<FilterItemDate>()
                            }
                        );
                }
                tree.First().Tree = tree;
                return tree;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterCommon.BuildTree : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Handle Mousedown, contribution : WORDIBOI
        /// </summary>
        private readonly MouseButtonEventHandler onMousedown = (o, eArgs) => { eArgs.Handled = true; };

        /// <summary>
        ///     Generate custom columns that can be filtered
        /// </summary>
        private void GeneratingCustomsColumn()
        {
            Debug.WriteLineIf(DebugMode, "GeneratingCustomColumn");

            try
            {
                // get the columns that can be filtered
                var columns = Columns
                    .Where(c => (c is DataGridTextColumn dtx && dtx.IsColumnFiltered) ||
                                (c is DataGridTemplateColumn dtp && dtp.IsColumnFiltered))
                    .Select(c => c)
                    .ToList();

                // set header template
                foreach (var col in columns)
                {
                    var columnType = col.GetType();

                    if (col.HeaderTemplate != null)
                    {
                        // reset filter Button
                        var buttonFilter = VisualTreeHelpers.GetHeader(col, this)
                            ?.FindVisualChild<Button>("FilterButton");
                        if (buttonFilter != null) FilterState.SetIsFiltered(buttonFilter, false);
                    }
                    else
                    {
                        if (columnType == typeof(DataGridTextColumn))
                        {
                            var column = (DataGridTextColumn)col;

                            // template
                            column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");

                            fieldType = null;
                            var fieldProperty = collectionType.GetProperty(((Binding)column.Binding).Path.Path);

                            // get type or underlying type if nullable
                            if (fieldProperty != null)
                                fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ??
                                            fieldProperty.PropertyType;

                            // apply DateFormatString when StringFormat for column is not provided or empty
                            if (fieldType == typeof(DateTime) && !string.IsNullOrEmpty(DateFormatString))
                                if (string.IsNullOrEmpty(column.Binding.StringFormat))
                                    column.Binding.StringFormat = DateFormatString;

                            // culture
                            if (((Binding)column.Binding).ConverterCulture == null)
                                ((Binding)column.Binding).ConverterCulture = Translate.Culture;

                            column.FieldName = ((Binding)column.Binding).Path.Path;
                        }
                        else if (columnType == typeof(DataGridTemplateColumn))
                        {
                            // DataGridTemplateColumn has no culture property
                            var column = (DataGridTemplateColumn)col;

                            // template
                            column.HeaderTemplate = (DataTemplate)TryFindResource("DataGridHeaderTemplate");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.GeneratingCustomColumn : {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Reset the cursor at the end of the sort
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSorted(object sender, EventArgs e)
        {
            ResetCursor();
        }

        /// <summary>
        ///     Reactivate sorting
        /// </summary>
        private void ReactivateSorting()
        {
            switch (currentColumn)
            {
                case null:
                    return;

                case DataGridTextColumn column:
                    column.CanUserSort = true;
                    break;

                case DataGridTemplateColumn templateColumn:
                    templateColumn.CanUserSort = true;
                    break;
            }
        }

        /// <summary>
        ///     Reset cursor
        /// </summary>
        private async void ResetCursor()
        {
            // reset cursor
            await Dispatcher.BeginInvoke((Action)(() => { Mouse.OverrideCursor = null; }),
                DispatcherPriority.ContextIdle);
        }

        /// <summary>
        ///     Can Apply filter (popup Ok button)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanApplyFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            // CanExecute only when the popup is open
            if ((popup?.IsOpen ?? false) == false)
            {
                e.CanExecute = false;
            }
            else
            {
                if (search)
                    e.CanExecute = PopupViewItems.Any(f => f?.IsChecked == true);
                else
                    e.CanExecute = PopupViewItems.Any(f => f.IsChanged) &&
                                   PopupViewItems.Any(f => f?.IsChecked == true);

                // TODO: remove tempo condition
                e.CanExecute = true;
            }
        }

        /// <summary>
        ///     Cancel button, close popup
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (popup != null)
            {
                popup.IsOpen = false; // raise EventArgs PopupClosed
            }

            if (filterValuePopup != null)
            {
                filterValuePopup.IsOpen = false;
            }
        }

        /// <summary>
        ///     Can remove filter when current column (CurrentFilter) filtered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanRemoveFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CurrentFilter?.IsFiltered ?? false;
        }

        /// <summary>
        ///     Can show filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CanShowFilter(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CollectionViewSource?.CanFilter == true && (!popup?.IsOpen ?? true) && !pending;
        }

        /// <summary>
        ///     Check/uncheck all item when the action is (select all)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckedAllCommand(object sender, ExecutedRoutedEventArgs e)
        {
            var item = (FilterItem)e.Parameter;

            // only when the item[0] (select all) is checked or unchecked
            if (item?.Level != 0 || ItemCollectionView == null) return;

            foreach (var obj in PopupViewItems.ToList()
                         .Where(f => f.IsChecked != item.IsChecked))
                obj.IsChecked = item.IsChecked;
        }

        /// <summary>
        ///     Clear Search Box text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="routedEventArgs"></param>
        private void ClearSearchBoxClick(object sender, RoutedEventArgs routedEventArgs)
        {
            search = false;
            searchTextBox.Text = string.Empty; // raises TextChangedEventArgs
        }

        /// <summary>
        ///     Aggregate list of predicate as filter
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        private bool Filter(object o)
        {
            if (o == null) return false;

            // Fast path for single predicate (most common case)
            if (criteria.Count == 1)
            {
                return criteria.Values.First()(o);
            }

            // Multiple predicates - use foreach instead of LINQ
            foreach (var predicate in criteria.Values)
            {
                if (!predicate(o))
                    return false;
            }
            return true;
        }

        /// <summary>
        ///     OnPropertyChange
        /// </summary>
        /// <param name="propertyName"></param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        ///     On Resize Thumb Drag Completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
        {
            Cursor = cursor;
        }

        /// <summary>
        ///     Get delta on drag thumb
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            // initialize the first Actual size Width/Height
            if (sizableContentHeight <= 0)
            {
                sizableContentHeight = sizableContentGrid.ActualHeight;
                sizableContentWidth = sizableContentGrid.ActualWidth;
            }

            var yAdjust = sizableContentGrid.Height + e.VerticalChange;
            var xAdjust = sizableContentGrid.Width + e.HorizontalChange;

            //make sure not to resize to negative width or heigth
            xAdjust = sizableContentGrid.ActualWidth + xAdjust > minWidth ? xAdjust : minWidth;
            yAdjust = sizableContentGrid.ActualHeight + yAdjust > minHeight ? yAdjust : minHeight;

            xAdjust = xAdjust < minWidth ? minWidth : xAdjust;
            yAdjust = yAdjust < minHeight ? minHeight : yAdjust;

            // set size of grid
            sizableContentGrid.Width = xAdjust;
            sizableContentGrid.Height = yAdjust;
        }

        /// <summary>
        ///     On Resize Thumb DragStarted
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnResizeThumbDragStarted(object sender, DragStartedEventArgs e)
        {
            cursor = Cursor;
            Cursor = Cursors.SizeNWSE;
        }

        /// <summary>
        ///     Reset the size of popup to original size
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PopupClosed(object sender, EventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "PopupClosed");

            var pop = (Popup)sender;

            // free the resources if the popup is closed without filtering
            if (!pending)
            {
                // clear resources
                ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());
                CurrentFilter = null;
                ReactivateSorting();
                ResetCursor();
            }

            // unsubscribe from event and re-enable datagrid
            pop.Closed -= PopupClosed;
            pop.MouseDown -= onMousedown;
            searchTextBox.TextChanged -= SearchTextBoxOnTextChanged;
            thumb.DragCompleted -= OnResizeThumbDragCompleted;
            thumb.DragDelta -= OnResizeThumbDragDelta;
            thumb.DragStarted -= OnResizeThumbDragStarted;

            sizableContentGrid.Width = sizableContentWidth;
            sizableContentGrid.Height = sizableContentHeight;
            Cursor = cursor;

            ListBoxItems = new List<FilterItem>();
            TreeviewItems = new List<FilterItemDate>();

            searchText = string.Empty;
            search = false;

            // re-enable datagrid
            IsEnabled = true;
        }

        /// <summary>
        ///     Remove current filter
        /// </summary>
        private void RemoveCurrentFilter()
        {
            Debug.WriteLineIf(DebugMode, "RemoveCurrentFilter");

            if (CurrentFilter == null) return;

            popup.IsOpen = false;

            // button icon reset
            FilterState.SetIsFiltered(button, false);

            var start = DateTime.Now;
            ElapsedTime = new TimeSpan(0, 0, 0);

            Mouse.OverrideCursor = Cursors.Wait;

            if (CurrentFilter.IsFiltered && criteria.Remove(CurrentFilter.FieldName))
                CollectionViewSource.Refresh();

            if (GlobalFilterList.Contains(CurrentFilter))
                _ = GlobalFilterList.Remove(CurrentFilter);

            // set the last filter applied
            lastFilter = GlobalFilterList.LastOrDefault()?.FieldName;

            ElapsedTime = DateTime.Now - start;

            CurrentFilter.IsFiltered = false;

            ResetCursor();
        }

        /// <summary>
        ///     remove current filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            RemoveCurrentFilter();
        }

        /// <summary>
        ///     Filter current list in popup
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private bool SearchFilter(object obj)
        {
            var item = (FilterItem)obj;
            if (string.IsNullOrEmpty(searchText) || item == null || item.Level == 0) return true;

            // Contains
            if (!StartsWith)
                return item.FieldType == typeof(DateTime)
                    ? ((DateTime?)item.Content)?.ToString(DateFormatString, Translate.Culture)
                    .IndexOf(searchText, IgnoreCase) >= 0
                    : item.Content?.ToString().IndexOf(searchText, IgnoreCase) >= 0;

            // StartsWith preserve RangeOverflow
            if (searchLength > item.ContentLength) return false;

            return item.FieldType == typeof(DateTime)
                ? ((DateTime?)item.Content)?.ToString(DateFormatString, Translate.Culture)
                .IndexOf(searchText, 0, searchLength, IgnoreCase) >= 0
                : item.Content?.ToString().IndexOf(searchText, 0, searchLength, IgnoreCase) >=
                  0;
        }

        /// <summary>
        ///     Search TextBox Text Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;

            // fix TextChanged event fires twice I did not find another solution
            if (textBox == null || textBox.Text == searchText || ItemCollectionView == null) return;

            searchText = textBox.Text;

            searchLength = searchText.Length;

            search = !string.IsNullOrEmpty(searchText);

            // apply filter
            ItemCollectionView.Refresh();

            if (CurrentFilter.FieldType != typeof(DateTime) || treeview == null) return;

            // rebuild treeview rebuild treeview
            if (string.IsNullOrEmpty(searchText))
            {
                // fill the tree with the elements of the list of the original items
                TreeviewItems = BuildTree(SourcePopupViewItems);
            }
            else
            {
                // fill the tree only with the items found by the search
                var items = PopupViewItems.Where(i => i.IsChecked).ToList();

                // if at least one item is not null, fill in the tree structure otherwise the tree structure contains only the item (select all).
                TreeviewItems = BuildTree(items.Any() ? items : null);
            }
        }

        /// <summary>
        ///     Open a pop-up window, Click on the header button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ShowFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "\r\nShowFilterCommand");
            SelectedFilter = FilterCondition.None;
            FilterValue = string.Empty;
            // reset previous elapsed time
            stopWatchFilter = Stopwatch.StartNew();
            //var start = DateTime.Now;

            // clear search text (!important)
            searchText = string.Empty;
            search = false;

            try
            {
                // filter button
                button = (Button)e.OriginalSource;
                if (button == null) return;
                //if (Items.Count == 0 || button == null) return;

                // contribution : OTTOSSON
                // for the moment this functionality is not tested, I do not know if it can cause unexpected effects
                _ = CommitEdit(DataGridEditingUnit.Row, true);

                // navigate up to the current header and get column type
                var header = VisualTreeHelpers.FindAncestor<DataGridColumnHeader>(button);
                var columnType = header.Column.GetType();

                // then down to the current popup
                popup = VisualTreeHelpers.FindChild<Popup>(header, "FilterPopup");

                if (popup == null) return;

                // popup handle event
                popup.Closed += PopupClosed;

                // disable popup background clickthrough, contribution : WORDIBOI
                popup.MouseDown += onMousedown;

                // disable datagrid while popup is open
                IsEnabled = false;

                // resizable grid
                sizableContentGrid = VisualTreeHelpers.FindChild<Grid>(popup.Child, "SizableContentGrid");

                // search textbox
                searchTextBox = VisualTreeHelpers.FindChild<TextBox>(popup.Child, "SearchBox");
                searchTextBox.Text = string.Empty;
                searchTextBox.TextChanged += SearchTextBoxOnTextChanged;
                searchTextBox.Focusable = true;

                // thumb resize grip
                thumb = VisualTreeHelpers.FindChild<Thumb>(sizableContentGrid, "PopupThumb");

                // minimum size of Grid
                sizableContentHeight = 0;
                sizableContentWidth = 0;

                sizableContentGrid.Height = popUpSize.Y;
                sizableContentGrid.MinHeight = popUpSize.Y;

                minHeight = sizableContentGrid.MinHeight;
                minWidth = sizableContentGrid.MinWidth;

                // thumb handle event
                thumb.DragCompleted += OnResizeThumbDragCompleted;
                thumb.DragDelta += OnResizeThumbDragDelta;
                thumb.DragStarted += OnResizeThumbDragStarted;

                // get field name from binding Path
                if (columnType == typeof(DataGridTextColumn))
                {
                    var column = (DataGridTextColumn)header.Column;
                    fieldName = column.FieldName;
                    column.CanUserSort = false;
                    currentColumn = column;
                }

                if (columnType == typeof(DataGridTemplateColumn))
                {
                    var column = (DataGridTemplateColumn)header.Column;
                    fieldName = column.FieldName;
                    column.CanUserSort = false;
                    currentColumn = column;
                }

                // invalid fieldName
                if (string.IsNullOrEmpty(fieldName)) return;

                // get type of field
                fieldType = null;
                var fieldProperty = collectionType.GetProperty(fieldName);

                // get type or underlying type if nullable
                if (fieldProperty != null)
                    fieldType = Nullable.GetUnderlyingType(fieldProperty.PropertyType) ?? fieldProperty.PropertyType;
                SetValue(AvailableFilterTypesProperty, GetFilterTypesForField(fieldType));

                // If no filter, add filter to GlobalFilterList list
                CurrentFilter = GlobalFilterList.FirstOrDefault(f => f.FieldName == fieldName) ??
                                new FilterCommon
                                {
                                    FieldName = fieldName,
                                    FieldType = fieldType,
                                    Translate = Translate
                                };

                // list of all item values, filtered and unfiltered (previous filtered items)
                var sourceObjectList = new List<object>();

                // set cursor
                Mouse.OverrideCursor = Cursors.Wait;

                var filterItemList = new List<FilterItem>();

                // get the list of values distinct from the list of raw values of the current column
                await Task.Run(() =>
                {
                    // empty item flag
                    var emptyItem = false;

                    // contribution : STEFAN HEIMEL
                    Dispatcher.Invoke(() =>
                    {
                        if (fieldType == typeof(DateTime))
                        {
                            sourceObjectList = Items.Cast<object>()
                                .Select(x =>
                                {
                                    var dt = fieldProperty?.GetValue(x, null);
                                    return dt != null ? (object)((DateTime)dt) : null;
                                })
                                .Where(x => x != null)
                                .Distinct()
                                .OrderBy(x => x)
                                .ToList();
                        }
                        else
                            sourceObjectList = Items.Cast<object>()
                                .Select(x => fieldProperty?.GetValue(x, null))
                                .Distinct()
                                .ToList();
                    });

                    // adds the previous filtered items to the list of new items (CurrentFilter.PreviouslyFilteredItems) displays new (checked) and
                    if (lastFilter == CurrentFilter.FieldName)
                        sourceObjectList.AddRange(CurrentFilter?.PreviouslyFilteredItems ?? new HashSet<object>());

                    // if they exist, remove from the list all null objects or empty strings
                    if (sourceObjectList.Any(l => l == null || l.Equals(string.Empty) || l.Equals(null)))
                    {
                        // element = null && "" are two different things but labeled as (Blank)
                        // in the list of items to be filtered
                        emptyItem = true;
                        sourceObjectList.RemoveAll(v => v == null || v.Equals(null) || v.Equals(string.Empty));
                    }

                    // sorting is a slow operation, using ParallelQuery
                    // TODO : AggregateException when user can add row
                    sourceObjectList = sourceObjectList.AsParallel().OrderBy(x => x).ToList();

                    // add the first element (select all) at the top of list
                    filterItemList = new List<FilterItem>(sourceObjectList.Count + 2)
                    {
                        new FilterItem { Label = Translate.All, IsChecked = true, Level = 0 }
                    };

                    // add all items (not null) to the filterItemList, the dates list is computed by BuildTree
                    filterItemList.AddRange(sourceObjectList.Select(item => new FilterItem
                    {
                        Content = item,
                        FieldType = fieldType,
                        Label = item.ToString(),
                        ContentLength = item.ToString().Length,
                        Level = 1,
                        Initialize = CurrentFilter.PreviouslyFilteredItems?.Contains(item) == false
                    }));

                    // add a empty item(if exist) at the bottom of the list
                    if (emptyItem)
                    {
                        sourceObjectList.Insert(sourceObjectList.Count, null);

                        filterItemList.Add(new FilterItem
                        {
                            FieldType = fieldType,
                            Content = null,
                            Label = Translate.Empty,
                            Level = -1,
                            Initialize = CurrentFilter?.PreviouslyFilteredItems?.Contains(null) == false
                        });
                    }
                });

                if (fieldType == typeof(DateTime))
                    TreeviewItems = BuildTree(filterItemList);
                else
                    ListBoxItems = filterItemList;

                // Set ICollectionView for filtering in the pop-up window
                ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(filterItemList);

                // set filter in popup
                if (ItemCollectionView.CanFilter) ItemCollectionView.Filter = SearchFilter;

                // set the placement and offset of the PopUp in relation to the header and the main window of the application
                // i.e (placement : bottom left or bottom right)
                PopupPlacement(sizableContentGrid, header);

                popup.UpdateLayout();

                // open popup
                popup.IsOpen = true;

                // set focus on searchTextBox
                searchTextBox.Focus();
                Keyboard.Focus(searchTextBox);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.ShowFilterCommand error : {ex.Message}");
                throw;
            }
            finally
            {
                stopWatchFilter.Stop();

                // show open popup elapsed time in UI
                ElapsedTime = stopWatchFilter.Elapsed;

                // reset cursor
                ResetCursor();
            }
        }

        /// <summary>
        ///     Click OK Button when Popup is Open, apply filter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ApplyFilterCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Debug.WriteLineIf(DebugMode, "\r\nApplyFilterCommand");

            stopWatchFilter.Start();
            pending = true;
            popup.IsOpen = false;

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var checkedItemsSet = new HashSet<object>(PopupViewItems.Where(f => f.IsChecked).Select(f => f.Content));

                await Task.Run(() =>
                {
                    if (CurrentFilter != null)
                    {
                        CurrentFilter.AddFilterCriteria(criteria, checkedItemsSet);

                        if (GlobalFilterList.All(f => f.FieldName != CurrentFilter.FieldName))
                        {
                            GlobalFilterList.Add(CurrentFilter);
                        }
                        lastFilter = CurrentFilter.FieldName;
                    }
                });

                if (CollectionViewSource?.CanFilter == true)
                {
                    CollectionViewSource.Refresh();
                }

                if (button != null)
                {
                    FilterState.SetIsFiltered(button, true);
                }

                if (CurrentFilter?.SelectedFilter == FilterCondition.None)
                {
                    CurrentFilter.FilterValue = string.Empty;
                    FilterValue = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyFilterCommand error: {ex.Message}");
                throw;
            }
            finally
            {
                ReactivateSorting();
                ResetCursor();
                ItemCollectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(new object());
                pending = false;
                CurrentFilter = null;
                stopWatchFilter.Stop();
                ElapsedTime = stopWatchFilter.Elapsed;
            }
        }

        /// <summary>
        ///     PopUp placement and offset
        /// </summary>
        /// <param name="grid"></param>
        /// <param name="header"></param>
        private void PopupPlacement(FrameworkElement grid, FrameworkElement header)
        {
            try
            {
                popup.PlacementTarget = header;
                popup.HorizontalOffset = 0d;
                popup.VerticalOffset = -1d;
                popup.Placement = PlacementMode.Bottom;

                // get the host window of the datagrid, contribution : STEFAN HEIMEL
                var hostingWindow = Window.GetWindow(this);

                if (hostingWindow != null)
                {
                    // greater than or equal to 0.0
                    double MaxSize(double size) => (size >= 0.0d) ? size : 0.0d;

                    const double border = 1d;

                    // get the ContentPresenter from the hostingWindow
                    var contentPresenter = VisualTreeHelpers.FindChild<ContentPresenter>(hostingWindow);

                    var hostSize = new Point
                    {
                        X = contentPresenter.ActualWidth,
                        Y = contentPresenter.ActualHeight
                    };

                    // get the X, Y position of the header
                    var headerContentOrigin = header.TransformToVisual(contentPresenter).Transform(new Point(0, 0));
                    var headerDataGridOrigin = header.TransformToVisual(this).Transform(new Point(0, 0));

                    var headerSize = new Point { X = header.ActualWidth, Y = header.ActualHeight };
                    var offset = popUpSize.X - headerSize.X + border;

                    // the popup must stay in the DataGrid, move it to the left of the header, because it overflows on the right.
                    if (headerDataGridOrigin.X + headerSize.X > popUpSize.X) popup.HorizontalOffset -= offset;

                    // delta for max size popup
                    var delta = new Point
                    {
                        X = hostSize.X - (headerContentOrigin.X + headerSize.X),
                        Y = hostSize.Y - (headerContentOrigin.Y + headerSize.Y + popUpSize.Y)
                    };

                    // max size
                    grid.MaxWidth = MaxSize(popUpSize.X + delta.X - border);
                    grid.MaxHeight = MaxSize(popUpSize.Y + delta.Y - border);

                    // remove offset
                    if (popup.HorizontalOffset == 0)
                        grid.MaxWidth = MaxSize(grid.MaxWidth -= offset);

                    // the height of popup is too large, reduce it, because it overflows down.
                    if (delta.Y <= 0d)
                    {
                        grid.MaxHeight = MaxSize(popUpSize.Y - Math.Abs(delta.Y) - border);
                        grid.Height = grid.MaxHeight;
                        grid.MinHeight = grid.MaxHeight;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FilterDataGrid.PopupPlacement error : {ex.Message}");
                throw;
            }
        }

        #endregion Private Methods

        #region Conditional filtering helpers

        /// <summary>
        ///     Compare strings
        /// </summary>
        /// <param name="value"></param>
        /// <param name="filter"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        private static bool CompareStrings(string value, string filter, FilterCondition condition)
        {
            if (value == null)
                return condition == FilterCondition.NotEquals;

            if (filter == null)
                return false;

            switch (condition)
            {
                case FilterCondition.Contains:
                    return value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                case FilterCondition.StartsWith:
                    return value.StartsWith(filter, StringComparison.OrdinalIgnoreCase);
                case FilterCondition.EndsWith:
                    return value.EndsWith(filter, StringComparison.OrdinalIgnoreCase);
                case FilterCondition.Equals:
                    return string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
                case FilterCondition.NotEquals:
                    return !string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        /// <summary>
        ///  Compare numbers
        /// </summary>
        /// <param name="value"></param>
        /// <param name="filter"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        private static bool CompareNumbers(double value, double filter, FilterCondition condition)
        {
            const double epsilon = 0.000001;
            switch (condition)
            {
                case FilterCondition.Equals: return Math.Abs(value - filter) < epsilon;
                case FilterCondition.NotEquals: return Math.Abs(value - filter) >= epsilon;
                case FilterCondition.GreaterThan: return value > filter;
                case FilterCondition.LessThan: return value < filter;
                case FilterCondition.GreaterThanOrEqual: return value >= filter;
                case FilterCondition.LessThanOrEqual: return value <= filter;
                default: return false;
            }
        }

        /// <summary>
        /// Compare dates
        /// </summary>
        /// <param name="value"></param>
        /// <param name="filter"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        private static bool CompareDates(DateTime value, DateTime filter, FilterCondition condition)
        {
            // Remove time component for comparison
            var dateValue = value.Date;
            var dateFilter = filter.Date;

            switch (condition)
            {
                case FilterCondition.Equals:
                    return dateValue == dateFilter;
                case FilterCondition.NotEquals:
                    return dateValue != dateFilter;
                case FilterCondition.GreaterThan:
                    return dateValue > dateFilter;
                case FilterCondition.LessThan:
                    return dateValue < dateFilter;
                case FilterCondition.GreaterThanOrEqual:
                    return dateValue >= dateFilter;
                case FilterCondition.LessThanOrEqual:
                    return dateValue <= dateFilter;
                default:
                    return false;
            }
        }
        #endregion
    }
}

