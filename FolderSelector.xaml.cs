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
using Microsoft.WindowsAPICodePack.Dialogs;

namespace ConvertFoldersToWebP {
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
                BrowseButton.IsEnabled = value;
            }
        }

        public string RootFolder {
            get { return RootFolderTextBox.Text; }
            set { RootFolderTextBox.Text = value; }
        }

        private static void OnEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is FolderSelector control) {
                control.UpdateControlState((bool)e.NewValue);
            }
        }

        private void UpdateControlState(bool isEnabled) {
            BrowseButton.IsEnabled = isEnabled;
        }

        public class SelectedFoldersEventArgs : EventArgs {
            public List<string> Folders { get; set; }

            public SelectedFoldersEventArgs(List<string> folders) {
                Folders = folders;
            }
        }

        public event EventHandler<SelectedFoldersEventArgs> SelectedFoldersChanged;

        private void BrowseButton_Click(object sender, RoutedEventArgs e) {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;  // This line makes the dialog a folder picker
            dialog.Title = "Select a Folder";  // Optional: set a title for the dialog
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok) {
                RootFolderTextBox.Text = dialog.FileName;
                LoadFolders(dialog.FileName);
                CheckAll.IsEnabled = FolderTreeView.Items.Count > 0 ? true : false;
                OK.IsEnabled = CheckAll.IsEnabled;
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
            CheckAll.IsEnabled = false;
            ClearChecks.IsEnabled = FolderTreeView.Items.Count > 0 ? true : false;
            OK_Click(sender, e);
        }

        private void ClearChecks_Click(object sender, RoutedEventArgs e) {
            foreach (var item in FolderTreeView.Items.Cast<FolderItem>()) {
                SetIsSelectedRecursive(item, false); // Recursively uncheck all items
            }
            CheckAll.IsEnabled = FolderTreeView.Items.Count > 0 ? true : false;
            ClearChecks.IsEnabled = false;
            OK_Click(sender, e);

        }

        private void SetIsSelectedRecursive(FolderItem item, bool isSelected) {
            item.IsSelected = isSelected; // Set the current item's IsSelected property

            // Recursively set IsSelected for all subfolders
            foreach (var subFolder in item.SubFolders) {
                SetIsSelectedRecursive(subFolder, isSelected);
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e) {
            var selectedFolders = GetSelectedFolders(FolderTreeView.Items.Cast<FolderItem>());
            SelectedFoldersChanged?.Invoke(this, new SelectedFoldersEventArgs(selectedFolders));
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