using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<YourDataModel> yourData;
        public ObservableCollection<YourDataModel> YourData
        {
            get => yourData;
            set
            {
                yourData = value;
                OnPropertyChanged(nameof(YourData));
            }
        }

        public MainViewModel()
        {
            YourData = new ObservableCollection<YourDataModel>
        {
            new YourDataModel { Id = 1, Name = "Item A", Category = "Category 1", Price = 10.5, CreatedDate = DateTime.Now },
            new YourDataModel { Id = 2, Name = "Item B", Category = "Category 2", Price = 23.0, CreatedDate = DateTime.Now.AddMonths(1) },
            new YourDataModel { Id = 3, Name = "Item C", Category = "Category 1", Price = 17.8, CreatedDate = DateTime.Now.AddMonths(-1) },
            new YourDataModel { Id = 4, Name = "Item D", Category = "Category 3", Price = 45.2, CreatedDate = DateTime.Now.AddDays(1) }
        };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class YourDataModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public double Price { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
