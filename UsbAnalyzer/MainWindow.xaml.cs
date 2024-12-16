using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace UsbAnalyzer {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private ObservableCollection<UsbLogEntry> _originalLogEntries;
        private ICollectionView _rawLogEntries;

        public static readonly DependencyProperty SelectedEntryProperty = DependencyProperty.Register(
            nameof(SelectedEntry), typeof(UsbLogEntry), typeof(MainWindow), new PropertyMetadata(default(UsbLogEntry)));

        public UsbLogEntry SelectedEntry {
            get { return (UsbLogEntry)GetValue(SelectedEntryProperty); }
            set { SetValue(SelectedEntryProperty, value); }
        }

        public MainWindow() {
            InitializeComponent();
        }

        private void OpenLog_OnClick(object sender, RoutedEventArgs e) {
            var opf = new OpenFileDialog();
            opf.Title = "Open USB Log";
            opf.Filter = "Log Files|*.txt";

            if (opf.ShowDialog(this) == true) {
                var text = File.ReadAllText(opf.FileName);
                var usbLog = UsbLogDecoder.Decode(text);
                _originalLogEntries = new ObservableCollection<UsbLogEntry>(usbLog);
                _rawLogEntries = CollectionViewSource.GetDefaultView(_originalLogEntries);
                _rawLogEntries.Filter = FilterRawEntries;
                UsbLogDataGrid.ItemsSource = _rawLogEntries;
            }
        }

        private bool FilterRawEntries(object obj) {
            if (obj is not UsbLogEntry entry) return false;

            if (rawHideSOF.IsChecked == true && entry.Pid == PIDTypes.SOF) {
                return false;
            } else if (rawHideNAK.IsChecked == true && (entry.Packet?.Response == PacketResponses.NAK)) {
                return false;
            } else {
                return true;
            }
        }

        private void RawFilter_Changed(object sender, RoutedEventArgs e) {
            _rawLogEntries?.Refresh();
        }
    }
}