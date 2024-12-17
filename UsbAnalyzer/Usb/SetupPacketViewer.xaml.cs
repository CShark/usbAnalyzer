using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UsbAnalyzer.ViewData;

namespace UsbAnalyzer.Usb {
    /// <summary>
    /// Interaktionslogik für SetupPacketViewer.xaml
    /// </summary>
    public partial class SetupPacketViewer : UserControl {
        public static readonly DependencyProperty TreeViewDataProperty = DependencyProperty.Register(
            nameof(TreeViewData), typeof(ObservableCollection<TreeViewEntry>), typeof(SetupPacketViewer),
            new PropertyMetadata(default(ObservableCollection<TreeViewEntry>)));

        public ObservableCollection<TreeViewEntry> TreeViewData {
            get { return (ObservableCollection<TreeViewEntry>)GetValue(TreeViewDataProperty); }
            set { SetValue(TreeViewDataProperty, value); }
        }

        public SetupPacketViewer() {
            TreeViewData = new();
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            TreeViewData.Clear();
            if (DataContext is UsbLogPacketGroupSetup packet) {
                // parse setup data
                TreeViewData.Add(TreeViewEntry.BuildTree(packet.SetupData));

                foreach (var desc in packet.Descriptors) {
                    TreeViewData.Add(TreeViewEntry.BuildTree(desc));
                    //TreeViewData.Add(desc.BuildTree());
                }

                foreach (var item in TreeViewData) {
                    item.IsRoot = true;
                }
            }
        }
    }
}