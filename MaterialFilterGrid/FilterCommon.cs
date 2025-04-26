#region (c) 2019 Gilles Macabies All right reserved

// Author     : Gilles Macabies
// Solution   : DataGridFilter
// Projet     : DataGridFilter
// File       : FilterCommon.cs
// Created    : 26/01/2021
//

#endregion (c) 2019 Gilles Macabies All right reserved

using System;
using System.Collections.Generic;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InvalidXmlDocComment
// ReSharper disable TooManyChainedReferences
// ReSharper disable ExcessiveIndentation
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace

// https://stackoverflow.com/questions/2251260/how-to-develop-treeview-with-checkboxes-in-wpf
// https://stackoverflow.com/questions/14876032/group-dates-by-year-month-and-date-in-wpf-treeview
// https://www.codeproject.com/Articles/28306/Working-with-Checkboxes-in-the-WPF-TreeView

namespace FilterDataGrid
{
    public sealed class FilterCommon : NotifyProperty
    {
        #region Private Fields

        private bool isFiltered;
        private bool showFilterTypes;
        private bool showValueInput;
        private string filterValue;
        private FilterCondition selectedFilter;
        private string valueInputWatermark;

        #endregion Private Fields

        #region Public Constructors

        public FilterCommon()
        {
            PreviouslyFilteredItems = new HashSet<object>(EqualityComparer<object>.Default);
            FilterValue = string.Empty;
            ValueInputWatermark = "Enter value...";
            ShowFilterTypes = true;
        }

        #endregion Public Constructors

        #region Public Properties

        public string FieldName { get; set; }
        public Type FieldType { get; set; }
        public FilterCondition SelectedFilter
        {
            get => selectedFilter;
            set
            {
                selectedFilter = value;
                OnPropertyChanged(nameof(SelectedFilter));
                ShowValueInput = value != FilterCondition.None;
            }
        }

        public bool ShowFilterTypes
        {
            get => showFilterTypes;
            set
            {
                showFilterTypes = value;
                OnPropertyChanged(nameof(ShowFilterTypes));
            }
        }

        public bool ShowValueInput
        {
            get => showValueInput;
            set
            {
                showValueInput = value;
                OnPropertyChanged(nameof(ShowValueInput));
            }
        }

        public string FilterValue
        {
            get => filterValue;
            set
            {
                filterValue = value;
                OnPropertyChanged(nameof(FilterValue));
            }
        }

        public string ValueInputWatermark
        {
            get => valueInputWatermark;
            set
            {
                valueInputWatermark = value;
                OnPropertyChanged(nameof(ValueInputWatermark));
            }
        }

        public bool IsFiltered
        {
            get => isFiltered;
            set
            {
                isFiltered = value;
                OnPropertyChanged("IsFiltered");
            }
        }

        public HashSet<object> PreviouslyFilteredItems { get; set; }

        public Loc Translate { get; set; }

        #endregion Public Properties

        #region Public Methods

        public void AddFilterCriteria(Dictionary<string, Predicate<object>> criteria, HashSet<object> checkedItems = null)
        {
            if (string.IsNullOrEmpty(FieldName)) return;

            // Remove existing criteria if any
            if (criteria.ContainsKey(FieldName))
            {
                criteria.Remove(FieldName);
            }

            criteria[FieldName] = obj =>
            {
                try
                {
                    if (obj == null)
                        return SelectedFilter == FilterCondition.NotEquals;

                    var propertyInfo = obj.GetType().GetProperty(FieldName);
                    if (propertyInfo == null) return true;

                    var value = propertyInfo.GetValue(obj);

                    // checkbox filtering
                    if (checkedItems != null && checkedItems.Count > 0 && !checkedItems.Contains(value))
                        return false;

                    // conditional filtering
                    if (SelectedFilter != FilterCondition.None && !string.IsNullOrEmpty(FilterValue))
                    {
                        return CompareValues(value, FilterValue, SelectedFilter);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            };

            IsFiltered = true;
        }

        #endregion Public Methods

        #region Helper Methods
        private bool CompareValues(object value, string filterValue, FilterCondition condition)
        {
            if (value == null)
                return condition == FilterCondition.NotEquals;

            if (string.IsNullOrEmpty(filterValue))
                return true;

            try
            {
                if (value is string strValue)
                    return CompareStrings(strValue, filterValue, condition);

                if (value is IConvertible)
                {
                    if (value is DateTime dateValue)
                    {
                        if (DateTime.TryParse(filterValue, out DateTime filterDate))
                        {
                            return CompareDates(dateValue, filterDate, condition);
                        }
                        return false;
                    }

                    if (double.TryParse(filterValue, out double filterNum))
                    {
                        return CompareNumbers(Convert.ToDouble(value), filterNum, condition);
                    }
                }

                return condition == FilterCondition.Equals
                    ? value.ToString().Equals(filterValue, StringComparison.OrdinalIgnoreCase)
                    : !value.ToString().Equals(filterValue, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool CompareStrings(string value, string filter, FilterCondition condition)
        {
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

        private static bool CompareDates(DateTime value, DateTime filter, FilterCondition condition)
        {
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
