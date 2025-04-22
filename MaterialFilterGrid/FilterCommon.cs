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
using System.Linq;

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
            AvailableFilterTypes = new List<FilterCondition> { FilterCondition.None, FilterCondition.Equals, FilterCondition.NotEquals };
            SelectedFilter = FilterCondition.None;
            FilterValue = string.Empty;
            ValueInputWatermark = "Enter value...";
            ShowFilterTypes = true;
        }

        #endregion Public Constructors

        #region Public Properties

        public string FieldName { get; set; }
        public Type FieldType { get; set; }

        public FilterCondition Condition { get; set; }
        public List<FilterCondition> AvailableFilterTypes { get; private set; }

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
        private bool CompareValues(object value, object parsedValue, FilterCondition condition)
        {
            // Special handling for null values
            if (value == null)
                return condition == FilterCondition.NotEquals;

            if (parsedValue == null)
                return false;

            // String comparisons
            if (value is string stringValue)
            {
                string stringParsedValue = parsedValue.ToString();

                switch (condition)
                {
                    case FilterCondition.Contains:
                        return stringValue.IndexOf(stringParsedValue, StringComparison.OrdinalIgnoreCase) >= 0;
                    case FilterCondition.StartsWith:
                        return stringValue.StartsWith(stringParsedValue, StringComparison.OrdinalIgnoreCase);
                    case FilterCondition.EndsWith:
                        return stringValue.EndsWith(stringParsedValue, StringComparison.OrdinalIgnoreCase);
                    case FilterCondition.Equals:
                        return string.Equals(stringValue, stringParsedValue, StringComparison.OrdinalIgnoreCase);
                    case FilterCondition.NotEquals:
                        return !string.Equals(stringValue, stringParsedValue, StringComparison.OrdinalIgnoreCase);
                    default:
                        return false;
                }
            }

            // Numeric and date comparisons
            if (value is IComparable comparable)
            {
                try
                {
                    // Important: Convert the parsedValue to the same type as value
                    object convertedValue = Convert.ChangeType(parsedValue, value.GetType());
                    int compareResult = comparable.CompareTo(convertedValue);

                    switch (condition)
                    {
                        case FilterCondition.Equals:
                            return compareResult == 0;
                        case FilterCondition.NotEquals:
                            return compareResult != 0;
                        case FilterCondition.GreaterThan:
                            return compareResult > 0;
                        case FilterCondition.LessThan:
                            return compareResult < 0;
                        case FilterCondition.GreaterThanOrEqual:
                            return compareResult >= 0;
                        case FilterCondition.LessThanOrEqual:
                            return compareResult <= 0;
                        default:
                            return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

            // Default comparison for non-comparable types
            switch (condition)
            {
                case FilterCondition.Equals:
                    return value.Equals(parsedValue);
                case FilterCondition.NotEquals:
                    return !value.Equals(parsedValue);
                default:
                    return false;
            }
        }
        public void AddFilter(Dictionary<string, Predicate<object>> criteria)
        {
            if (IsFiltered)
                return;

            bool Predicate(object o)
            {
                if (o == null)
                    return false;

                var property = o.GetType().GetProperty(FieldName);
                if (property == null)
                    return true;

                try
                {
                    // Get the value, handling DateTime specially
                    object value;
                    if (FieldType == typeof(DateTime))
                    {
                        var dateValue = property.GetValue(o, null) as DateTime?;
                        value = dateValue?.Date;
                    }
                    else
                    {
                        value = property.GetValue(o, null);
                    }

                    // First check the checkbox filter
                    bool passesCheckboxFilter = !PreviouslyFilteredItems.Contains(value);

                    // Then check the conditional filter if one is active
                    bool passesConditionalFilter = true;
                    if (SelectedFilter != FilterCondition.None && !string.IsNullOrEmpty(FilterValue))
                    {
                        var parsedValue = ParseFilterValue();
                        passesConditionalFilter = CompareValues(value, parsedValue, SelectedFilter);
                    }

                    // Both conditions must be met
                    return passesCheckboxFilter && passesConditionalFilter;
                }
                catch
                {
                    return false;
                }
            }

            criteria.Add(FieldName, Predicate);
            IsFiltered = true;
        }

        private object ParseFilterValue()
        {
            if (string.IsNullOrEmpty(FilterValue))
                return null;

            try
            {
                // Handle different types
                if (FieldType == typeof(string))
                    return FilterValue;

                if (FieldType == typeof(int) || FieldType == typeof(int?))
                    return int.Parse(FilterValue);

                if (FieldType == typeof(double) || FieldType == typeof(double?))
                    return double.Parse(FilterValue);

                if (FieldType == typeof(decimal) || FieldType == typeof(decimal?))
                    return decimal.Parse(FilterValue);

                if (FieldType == typeof(DateTime) || FieldType == typeof(DateTime?))
                {
                    var date = DateTime.Parse(FilterValue);
                    return FieldType == typeof(DateTime?) ? (DateTime?)date : date;
                }

                if (FieldType == typeof(bool) || FieldType == typeof(bool?))
                    return bool.Parse(FilterValue);

                return FilterValue;
            }
            catch
            {
                return FilterValue;
            }
        }
        #endregion Public Methods
    }
}