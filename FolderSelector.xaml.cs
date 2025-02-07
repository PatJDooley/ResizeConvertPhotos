using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ResizeAndConvertImages {
    public partial class FolderSelector : UserControl {
        public FolderSelector() {
            InitializeComponent();
        }

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.Register("Enabled", typeof(bool), typeof(FolderSelector),
                new PropertyMetadata(true, OnEnabledPropertyChanged));

        public bool Enabled {
            get { return (bool)GetValue(EnabledProperty); }
            set { 
                SetValue(EnabledProperty, value);
                ProcessButton.IsEnabled = value;
            }
        }

        private static void OnEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is FolderSelector control) {
                control.UpdateControlState((bool)e.NewValue);
            }
        }

        private void UpdateControlState(bool isEnabled) {
            ProcessButton.IsEnabled = isEnabled;
        }

        public class SelectedFoldersEventArgs : EventArgs {
            public List<string> Folders { get; set; }

            public SelectedFoldersEventArgs(List<string> folders) {
                Folders = folders;
            }
        }

        public event EventHandler<SelectedFoldersEventArgs> SelectedFoldersChanged;

        private void ProcessButton_Click(object sender, RoutedEventArgs e) {
            var selectedFolders = GetSelectedFolders(FolderTreeView.Items.Cast<FolderItem>());
            SelectedFoldersChanged?.Invoke(this, new SelectedFoldersEventArgs(selectedFolders));
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                RootFolderTextBox.Text = dialog.SelectedPath;
                LoadFolders(dialog.SelectedPath);
            }
        }

        private void LoadFolders(string rootPath) {
            FolderTreeView.Items.Clear();
            var rootFolder = new FolderItem(rootPath);
            FolderTreeView.Items.Add(rootFolder);
        }

        private List<string> GetSelectedFolders(IEnumerable<FolderItem> items) {
            var selectedFolders = new List<string>();
            foreach (var item in items) {
                if (item.IsSelected == true) {
                    selectedFolders.Add(item.FullPath);
                }
                selectedFolders.AddRange(GetSelectedFolders(item.SubFolders));
            }
            return selectedFolders;
        }

        private void CheckAll_Click(object sender, RoutedEventArgs e) {
            foreach (var item in FolderTreeView.Items.Cast<FolderItem>()) {
                SetIsSelectedRecursive(item, true); // Recursively check all items
            }
        }

        private void ClearChecks_Click(object sender, RoutedEventArgs e) {
            foreach (var item in FolderTreeView.Items.Cast<FolderItem>()) {
                SetIsSelectedRecursive(item, false); // Recursively uncheck all items
            }
        }

        private void SetIsSelectedRecursive(FolderItem item, bool isSelected) {
            item.IsSelected = isSelected; // Set the current item's IsSelected property

            // Recursively set IsSelected for all subfolders
            foreach (var subFolder in item.SubFolders) {
                SetIsSelectedRecursive(subFolder, isSelected);
            }
        }
    }

    public class FolderItem : INotifyPropertyChanged {
        private bool _isSelected;

        public string Name { get; set; }
        public string FullPath { get; set; }
        public ObservableCollection<FolderItem> SubFolders { get; set; }

        public bool IsSelected {
            get { return _isSelected; }
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public FolderItem(string path) {
            FullPath = path;
            Name = System.IO.Path.GetFileName(path);
            SubFolders = new ObservableCollection<FolderItem>();

            try {
                foreach (var dir in Directory.GetDirectories(path)) {
                    SubFolders.Add(new FolderItem(dir));
                }
            }
            catch (UnauthorizedAccessException) {
                // Handle unauthorized access
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}