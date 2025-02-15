
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.Win32;
using ImageMagick;
using Microsoft.WindowsAPICodePack.Dialogs;
using static ConvertFoldersToWebP.FolderSelector;
using static ConvertFoldersToWebP.Settings1;
using ImageMagick.Drawing;
using System.Collections.ObjectModel;
using System.Globalization;
using CsvHelper;
using System.Reflection;

namespace ConvertFoldersToWebP {
    public partial class MainWindow : Window {
        private WindowHandler windowHandler;
        public ObservableCollection<FolderStats> StatsCollection { get; set; }
        string _RootFolder = string.Empty;
        List<string> _SourceFolders = new List<string>();
        List<string> _SelectedFolders = new List<string>();
        StreamWriter _ErrorLogFile;
        public List<string> _Errors = new List<string>();

        // Values that are stored via Settings1
        string _OutputFolder = string.Empty;
        uint _ImageQuality = 80;
        string _InputType = "jpg";
        uint? _ResizeAmount = null;
        bool _Ready = false;
        MagickFormat _OutputType = MagickFormat.Jpg;

        private enum resizeRB {
            NotSet,
            None,
            Percentage,
            Width,
            Height
        }
        resizeRB _ResizeRB = resizeRB.None;

        private enum sourceRB {
            Keep,
            Delete,
            DeleteOnly
        }
        sourceRB _SourceRB = sourceRB.Delete;

        public MainWindow() {
            InitializeComponent();
            windowHandler = new WindowHandler(this);
            FolderSelector.SelectedFoldersChanged += FolderSelector_SelectedFoldersChanged;
            StatsCollection = new ObservableCollection<FolderStats>();
            StatsGrid.ItemsSource = StatsCollection;
        }

        private void winMain1_Loaded(object sender, RoutedEventArgs e) {
            LoadFileTypes(cboInputFileType);
            LoadFileTypes(cboOutputFileType);
            FolderSelector.Enabled = true;
            LoadSettings();
            _ErrorLogFile = new StreamWriter(Path.Combine(Path.GetTempPath(), "Error_Log_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt"));
            _Errors.Clear();
            _Ready = true;
        }

        private string GetExePath() {
            string exePath = Assembly.GetExecutingAssembly().Location;

            // Extract the directory from the path
            return Path.GetDirectoryName(exePath);
        }

        private void LoadSettings() {
            _ResizeAmount = (uint)Settings1.Default.Amount;
            txtResizeAmount.Text = _ResizeAmount.ToString();

            switch (Settings1.Default.ResizeType) {
                case "None":
                    rbNone.IsChecked = true;
                    _ResizeRB = resizeRB.None;
                    break;
                case "Percentage":
                    rbPercentage.IsChecked = true;
                    _ResizeRB = resizeRB.Percentage;
                    break;
                case "Width":
                    rbWidth.IsChecked = true;
                    _ResizeRB = resizeRB.Width;
                    break;
                case "Height":
                    rbHeight.IsChecked = true;
                    _ResizeRB = resizeRB.Height;
                    break;
            }

            _InputType = Settings1.Default.InputFileType;
            cboInputFileType.SelectedValue = _InputType;

            if (Settings1.Default.KeepInput) {
                rbKeep.IsChecked = true;
                _SourceRB = sourceRB.Keep;
            }
            else {
                rbDelete.IsChecked = true;
                _SourceRB = sourceRB.Delete;
            }

            string outputType = Settings1.Default.OutputFileType;
            cboOutputFileType.SelectedValue = outputType;
            _OutputType = OutputType(outputType);

            _ImageQuality = (uint)Settings1.Default.Quality;
            txtQuality.Text = _ImageQuality.ToString();

            txtCopyRight.Text = Settings1.Default.CopyrightNotice.ToString();

            chkSaveFolder.IsChecked = Settings1.Default.SaveElseWhere;

            _OutputFolder = Settings1.Default.OutputFolder;
            txtOutputFolder.Text = _OutputFolder;

        }
        private void btnSelectFolders_Click(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.OpenFileDialog {
                // Allow selecting folders instead of files
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true) {
                _SourceFolders = dialog.FileNames.Select(Path.GetDirectoryName).Distinct().ToList();
            }
        }
        //private void FolderSelector_SelectedFoldersChanged(object sender, RoutedEventArgs e) {

        //}
        private void FolderSelector_SelectedFoldersChanged(object sender, SelectedFoldersEventArgs e) {
            _SourceFolders = e.Folders;
            if (_SourceFolders.Count == 0) {
                System.Windows.MessageBox.Show("No folders selected", "ERROR", MessageBoxButton.OK);
                return;
            }

            if (_SourceRB == sourceRB.DeleteOnly) {
                if (System.Windows.MessageBox.Show("You have chosen to delete every " + _InputType + " file in the checked folders. This is your final chance to keep the input files. Click \"Yes\" Keep them after all. Click \"No\" to say goodbye to them.",
                    "Check",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes) {
                    return;
                }
            }

            if (ValidationSuccessful()) {
                _RootFolder = FolderSelector.RootFolder;
                ConvertResizeDelete(_SourceFolders);
            }
        }

        private async void ConvertResizeDelete(List<string> folders) {
            windowHandler.StartProcessing();

            uint newWidth = _ResizeAmount == null ? 0 : _ResizeAmount.Value;
            uint newHeight = newWidth;

            var searchPatterns = GetSearchPatterns();
            string outputType = _OutputType.ToString().ToLower();
            string copyRightNotice = txtCopyRight.Text;
            bool addFileName = chkFileName.IsChecked.Value;

            // Stats collection
            DateTime startTime = DateTime.Now;
            long totalBytesRead = 0, totalBytesWritten = 0;
            int fileCount = 0;

            progressBar.Value = 0;
            var progress = new Progress<int>(value => {
                // Update the UI with the progress
                progressBar.Value = value;
            });

            if (_SourceRB != sourceRB.DeleteOnly) {

                foreach (var folder in folders) {
                    var files = searchPatterns.SelectMany(pattern => Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly));
                    int totalFiles = files.Count();
                    var folderName = new DirectoryInfo(folder).Name; // Just the parent folder name
                    this.Dispatcher.Invoke(() => folderLabel.Content = $"Processing: {folderName}");
                    var folderStats = new FolderStats {
                        FolderName = folder,
                        InputFileType = _InputType.ToString(),
                        OutputFileType = _OutputType.ToString(),
                        ResizeType = _ResizeRB.ToString()
                    };
                    fileCount = 0;
                    await System.Threading.Tasks.Task.Run(() => {
                        ((IProgress<int>)progress).Report(0);
                        ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = (Environment.ProcessorCount * 3) / 4 }; // Use 3/4 the available cores
                        Parallel.ForEach(files, parallelOptions, file => {
                            string outputFile = SetOutputFilePath(file);
                            using (var image = new MagickImage(file)) {
                                if (_OutputType.ToString().ToLower() == "webp" && (image.Width > 16000 || image.Height > 16000)) {
                                    AutoResizeHugeImages(image);
                                }
                                // Stats collection
                                long fileSize = new FileInfo(file).Length;
                                Interlocked.Add(ref totalBytesRead, fileSize);
                                // Add text to the image
                                if (copyRightNotice.Length > 1 || addFileName) {
                                    AddTextItems(file, copyRightNotice, addFileName, image);
                                }

                                IMagickImage<ushort> resizedImage = ResizeImage(image);
                                resizedImage.Format = _OutputType;
                                resizedImage.Quality = _ImageQuality;
                                try {
                                    resizedImage.Write(outputFile);
                                    resizedImage.Dispose();
                                    // Stats collection
                                    fileSize = new FileInfo(outputFile).Length;
                                    Interlocked.Add(ref totalBytesWritten, fileSize);

                                    // Update progress asynchronously
                                    ((IProgress<int>)progress).Report((int)((Interlocked.Increment(ref fileCount) * 100.0) / totalFiles));
                                }
                                catch (Exception ex) {
                                    HandleOutputException(outputFile, ex);
                                }

                            }
                        });

                        ((IProgress<int>)progress).Report(100);

                    });

                    UpdateStats(startTime, totalBytesRead, totalBytesWritten, fileCount, folderStats);
                }
                progressBar.Value = 100;
                System.Windows.MessageBox.Show("Conversion/Resizing phase complete!");
            }

            // Ensure the progress bar is fully filled when the folder is done
            bool keep = false;
            if (_SourceRB != sourceRB.Keep) {
                if (_SourceRB == sourceRB.Delete) {
                    if (System.Windows.MessageBox.Show("Final chance to keep the input files. Keep them after all?",
                                        "Check",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question) == MessageBoxResult.Yes) {
                        keep = true;
                    }
                }
                if (!keep) {
                    DeleteSourceFiles(folders, searchPatterns);
                }
            }

            progressBar.Value = 0;
            windowHandler.EndProcessing();
        }

        private static void AutoResizeHugeImages(MagickImage image) {
            // Resize to fit within the hardcoded limits of ImageMagick for WebP
            double scale = Math.Min(16000.0 / image.Width, 16000.0 / image.Height);
            uint newWidth = (uint)(image.Width * scale);
            uint newHeight = (uint)(image.Height * scale);
            image.Resize(newWidth, newHeight);
        }

        private void DeleteSourceFiles(List<string> folders, string[]? searchPatterns) {
            windowHandler.StartProcessing();
            string outputType = _OutputType.ToString().ToLower();
            foreach (var folder in folders) {
                var files = searchPatterns.SelectMany(pattern => Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly));
                ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = (Environment.ProcessorCount * 3) / 4 }; // Use 3/4 the available cores
                Parallel.ForEach(files, parallelOptions, file => {

                    if (File.Exists(file)) {
                        try {
                            File.Delete(file);
                        }
                        catch (Exception ex) {
                            if (System.Windows.MessageBox.Show("Could not delete " + file + " because " + ex.Message + " Continue (Yes) [recommended] or quit (No)?",
                                "Error",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Error) == MessageBoxResult.No) {
                                System.Windows.Application.Current.Shutdown();
                            }
                        }
                    }
                });
            }
            windowHandler.EndProcessing();
            System.Windows.MessageBox.Show("Deletion phase complete!");
        }

        private IMagickImage<ushort> ResizeImage(MagickImage image) {
            uint newWidth = image.Width;
            uint newHeight = image.Height;

            var resizedImage = image.Clone();
            switch (_ResizeRB) {
                case resizeRB.Height:
                    newWidth = (_ResizeAmount.Value * newWidth) / newHeight;
                    break;
                case resizeRB.Width:
                    newHeight = (_ResizeAmount.Value * newHeight) / newWidth;
                    break;
                case resizeRB.Percentage:
                    newWidth = (_ResizeAmount.Value * newWidth) / 100;
                    newHeight = (_ResizeAmount.Value * newHeight) / 100;
                    break;
            }
            if (_ResizeRB != resizeRB.None) {
                resizedImage.Resize(new MagickGeometry(newWidth, newHeight));
            }

            return resizedImage;
        }

        private string[] GetSearchPatterns() {
            return _InputType.ToLower() switch {
                "webp" => ["*.webp", "*.WEBP", "*.webP"],
                "jpeg" or "jpg" => ["*.jpg", "*.JPG", "*.jpeg", "*.JPEG"],
                _ => [$"*.{_InputType.ToLower()}", $"*.{_InputType.ToUpper()}"]
            };
        }

        private void UpdateStats(DateTime startTime, long totalBytesRead, long totalBytesWritten, int fileCount, FolderStats folderStats) {
            // Stats collection
            folderStats.FilesProcessed = fileCount;
            folderStats.BytesRead = totalBytesRead;
            folderStats.BytesWritten = totalBytesWritten;
            folderStats.AvgInputFileSize = fileCount == 0 ? 0 : totalBytesRead / fileCount;
            folderStats.AvgOutputFileSize = fileCount == 0 ? 0 : totalBytesWritten / fileCount;
            folderStats.BytesSavedLost = totalBytesRead - totalBytesWritten;
            folderStats.ElapsedTime = (int)(DateTime.Now - startTime).TotalSeconds;
            folderStats.AvgTimePerFile = fileCount == 0 ? 0 : (long)(DateTime.Now - startTime).TotalMilliseconds / fileCount;

            // Add the stats to the collection for display
            StatsCollection.Add(folderStats);
        }

        private void HandleOutputException(string outputFile, Exception ex) {
            string message = "Could not create " + outputFile + " because " + ex.Message;
            _Errors.Add(message);
            _ErrorLogFile.WriteLineAsync(message);
            if (System.Windows.MessageBox.Show(message + " Continue (Yes) or quit (No)?",
                "Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error) == MessageBoxResult.No) {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void AddTextItems(string file, string copyRightNotice, bool addFileName, MagickImage image) {
            // Calculate font size based on image dimensions
            int fontSize = (int)Math.Max(8, Math.Min(image.Width, image.Height) * 0.035);

            // Estimate character width (this is very approximate)
            double charWidth = fontSize * 0.5; // assuming half the font size for each character width as an estimate

            // Create a drawable object to add text
            var drawables = new Drawables();

            // Add copyright notice (bottom-right corner)
            if (copyRightNotice.Length > 1) {
                string copyRight = copyRightNotice;
                double textWidth = copyRight.Length * charWidth; // Estimate text width
                int indent = (int)(textWidth + 10); // Add some extra padding

                drawables.FontPointSize(fontSize)
                         .Font("sans-serif")
                         .FillColor(MagickColors.White)
                         .Text(image.Width - indent, image.Height - 14, copyRight) // White shadow
                         .FillColor(MagickColors.Black)
                         .Text(image.Width - indent + 2, image.Height - 12, copyRight); // Black text
            }

            // Add filename (bottom-left corner)
            if (addFileName) {
                string fileName = Path.GetFileNameWithoutExtension(file) + "." + _OutputType.ToString().ToLower();
                drawables.FontPointSize(fontSize)
                         .Font("sans-serif")
                         .FillColor(MagickColors.White)
                         .Text(10, image.Height - 14, fileName) // White shadow
                         .FillColor(MagickColors.Black)
                         .Text(8, image.Height - 12, fileName); // Black text
            }

            // Draw the text onto the image
            drawables.Draw(image);
        }

        private string SetOutputFilePath(string file) {
            if (_OutputFolder != string.Empty) {
                string outputFile = TransposeFilePath(_RootFolder, file, _OutputFolder);
                string inputExt = Path.GetExtension(outputFile);
                string outputExt = "." + _OutputType.ToString();
                if (inputExt.ToLower() != outputExt.ToLower()) {
                    outputFile = outputFile.Replace(inputExt, outputExt.ToLower());
                }
                return outputFile;
            }
            else {
                return Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file)) + "." + _OutputType;
            }
        }

        private string TransposeFilePath(string rootFolder, string filePath, string outputFolder) {
            // Convert to full paths to ensure we're working with absolute paths
            rootFolder = Path.GetFullPath(rootFolder);
            filePath = Path.GetFullPath(filePath);
            outputFolder = Path.GetFullPath(outputFolder);

            // Ensure the file path starts with the root folder
            if (!filePath.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException("The file path must be within the root folder.");
            }

            // Get the relative path from rootFolder to filePath
            string relativePath = filePath.Substring(rootFolder.Length).TrimStart(Path.DirectorySeparatorChar);

            // Construct the new path in the output folder
            string newPath = Path.Combine(outputFolder, relativePath);

            // Ensure all directories in the new path exist
            string directoryPath = Path.GetDirectoryName(newPath);
            if (!Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            return newPath;
        }

        private void LoadFileTypes(System.Windows.Controls.ComboBox cboFileType) {
            cboFileType.Items.Add("bmp");
            cboFileType.Items.Add("gif");
            cboFileType.Items.Add("jpeg");
            cboFileType.Items.Add("jpg");
            cboFileType.Items.Add("png");
            cboFileType.Items.Add("tiff");
            cboFileType.Items.Add("webp");
        }

        private void txtQuality_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) {
            if (e.Text.Length > 0) {
                e.Handled = !int.TryParse(e.Text, out int value);
            }
            e.Handled = false;
        }

        private void txtResizeAmount_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e) {
            if (e.Text.Length > 0) {
                e.Handled = !uint.TryParse(txtResizeAmount.Text + e.Text, out uint value);
                _ResizeAmount = value;
            }
            else {
                _ResizeAmount = 0;
                e.Handled = false;
            }
        }

        private void SetResizeRB() {
            if (rbNone.IsChecked == true) {
                _ResizeRB = resizeRB.None;
            }
            else if (rbPercentage.IsChecked == true) {
                _ResizeRB = resizeRB.Percentage;
            }
            else if (rbWidth.IsChecked == true) {
                _ResizeRB = resizeRB.Width;
            }
            else if (rbHeight.IsChecked == true) {
                _ResizeRB = resizeRB.Height;
            }
            else {
                _ResizeRB = resizeRB.NotSet;
            }
            if (_ResizeRB != resizeRB.None) {
                if (!ValidDirectory(_OutputFolder)) {
                    // Do we need a message here?
                }
            }
            Settings1.Default.ResizeType = _ResizeRB.ToString();
            Settings1.Default.Save();
        }

        private void rbNone_Click(object sender, RoutedEventArgs e) {
            SetResizeRB();
            txtResizeAmount.Text = "";
            _ResizeAmount = 0;
            txtResizeAmount.IsEnabled = false;
        }

        private void rbPercentage_Click(object sender, RoutedEventArgs e) {
            SetResizeRB();
            txtResizeAmount.IsEnabled = true;
            var result = ValidRange(txtResizeAmount.Text, 50, 100);
            if (!result.valid) {
                txtResizeAmount.Text = "";
                _ResizeAmount = 0;
            }
        }

        private bool ValidDirectory(string folder) {
            if (folder == null) {
                return false;
            }
            if (folder == string.Empty) {
                return false;
            }
            string fullPath = Path.GetFullPath(folder);
            if (!Directory.Exists(fullPath)) {
                return false;
            }
            return true;
        }

        private void rbWidth_Click(object sender, RoutedEventArgs e) {
            SetResizeRB();
            var result = ValidRange(txtResizeAmount.Text, 16, 1000);
            if (!result.valid) {
                txtResizeAmount.Text = "";
                _ResizeAmount = 0;
            }
        }

        private void rbHeight_Click(object sender, RoutedEventArgs e) {
            SetResizeRB();
            txtResizeAmount.IsEnabled = true;
            var result = ValidRange(txtResizeAmount.Text, 16, 1000);
            if (!result.valid) {
                txtResizeAmount.Text = "";
                _ResizeAmount = 0;
            }
        }

        private void txtQuality_LostFocus(object sender, RoutedEventArgs e) {
            var result = ValidRange(txtQuality.Text, 50, 100);
            if (result.valid) {
                _ImageQuality = (uint)result.value;
                Settings1.Default.Quality = result.value;
                Settings1.Default.Save();
            }
            else {
                txtQuality.Text = "";
                txtQuality.Focus();
            }
        }

        private (int value, bool valid) ValidRange(string textValue, int lowerValue, int upperValue) {
            if (int.TryParse(textValue, out int value)) {
                if (value >= lowerValue && value <= upperValue) {
                    return (value, true);
                }
            }
            System.Windows.MessageBox.Show("Value must be in range " + lowerValue.ToString() + " to " + upperValue.ToString(), "Error", MessageBoxButton.OK);
            return (-1, false);
        }
        private void txtResizeAmount_LostFocus(object sender, RoutedEventArgs e) {
            int lowerValue = 0;
            int upperValue = 0;
            _ResizeAmount = 0;
            switch (_ResizeRB) {
                case resizeRB.None:
                    txtResizeAmount.Text = "";
                    return;
                case resizeRB.Percentage:
                    lowerValue = 5;
                    upperValue = 100;
                    break;
                case resizeRB.Width:
                case resizeRB.Height:
                    lowerValue = 16;
                    upperValue = 10000;
                    break;
            }

            var result = ValidRange(txtResizeAmount.Text, lowerValue, upperValue);
            if (result.valid) {
                _ResizeAmount = (uint)result.value;
                Settings1.Default.Amount = (int)result.value;
                Settings1.Default.Save();
            }
            else {
                _ResizeAmount = 0;
                txtResizeAmount.Text = "";
                txtResizeAmount.Focus();
                return;
            }
        }
        private void SetSourceRB() {
            FolderSelector.Enabled = true;

            if (rbDelete.IsChecked == true) {
                _SourceRB = sourceRB.Delete; ;
            }
            else if (rbKeep.IsChecked == true) {
                _SourceRB = sourceRB.Keep;
            }
            else if (rbDeleteOnly.IsChecked == true) {
                _SourceRB = sourceRB.DeleteOnly;
            }
            Settings1.Default.KeepInput = _SourceRB == sourceRB.Keep;
            Settings1.Default.Save();
        }

        private bool ValidationSuccessful() {
            if (!_Ready) {
                return false;
            }
            FolderSelector.Enabled = true;
            if (_OutputType.ToString().ToLower() == _InputType && txtOutputFolder.Text == string.Empty) {
                System.Windows.MessageBox.Show("If the input and output file types are the same, you must select a separate output folder.", "Error", MessageBoxButton.OK);
                return false;
            }
            if ((_ResizeAmount ?? 0) == 0 && (_ResizeRB != resizeRB.None)) {
                System.Windows.MessageBox.Show("You must specify a resize value and an output path unless you selected \"None\", i.e. no change in size.", "Error", MessageBoxButton.OK);
                return false;
            }
            if (!ValidDirectory(_OutputFolder) && (_ResizeRB != resizeRB.None)) {
                System.Windows.MessageBox.Show("You must specify a resize value and an output path unless you selected \"None\", i.e. no change in size.", "Error", MessageBoxButton.OK);
                return false;
            }
            if ((_SourceRB == sourceRB.Delete) && (_ResizeRB != resizeRB.None) && !ValidDirectory(_OutputFolder)) {
                if (System.Windows.MessageBox.Show("You are deleting the input files and resizing the output files. If resizing makes the output files smaller, image quality will suffer. " +
                    "Are you certain you want to do this?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) {
                    return false;
                }
            }
            return true;
        }

        private void rbDelete_Click(object sender, RoutedEventArgs e) {
            SetSourceRB();
        }

        private void rbKeep_Click(object sender, RoutedEventArgs e) {
            SetSourceRB();
        }

        private void rbDeleteOnly_Click(object sender, RoutedEventArgs e) {
            SetSourceRB();
        }

        private void GetOutputFilePath() {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (_OutputFolder == string.Empty && _RootFolder != string.Empty) {
                _OutputFolder = _RootFolder;
            }
            dialog.InitialDirectory = _OutputFolder;
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok) {
                _OutputFolder = dialog.FileName;
                txtOutputFolder.Text = _OutputFolder;
                Settings1.Default.OutputFolder = _OutputFolder;
                Settings1.Default.Save();

                if (FolderSelector.RootFolder == string.Empty) {
                    FolderSelector.RootFolder = _OutputFolder;
                }
            }
        }

        private void ClearOutputFolder_Click(object sender, RoutedEventArgs e) {
            txtOutputFolder.Text = string.Empty;
            _OutputFolder = string.Empty;
            Settings1.Default.OutputFolder = _OutputFolder;
            Settings1.Default.Save();
        }
        private void btnOutputFolder_Click(object sender, RoutedEventArgs e) {
            GetOutputFilePath();
        }

        private void chkSaveFolder_Click(object sender, RoutedEventArgs e) {
            btnOutputFolder.IsEnabled = chkSaveFolder.IsChecked.Value;
            Settings1.Default.SaveElseWhere = chkSaveFolder.IsChecked.Value;
            Settings1.Default.Save();
        }

        private void cboInputFileType_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            _InputType = ((System.Windows.Controls.ComboBox)sender).SelectedValue.ToString();
            Settings1.Default.InputFileType = _InputType;
            Settings1.Default.Save();
        }

        private void cboOutputFileType_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            string outputType = ((System.Windows.Controls.ComboBox)sender).SelectedValue.ToString();
            _OutputType = OutputType(outputType);
            Settings1.Default.OutputFileType = outputType;
            Settings1.Default.Save();
        }

        private MagickFormat OutputType(string outputType) {
            switch (outputType) {
                case "bmp":
                    return MagickFormat.Bmp;
                case "gif":
                    return MagickFormat.Gif;
                case "png":
                    return MagickFormat.Png;
                case "tiff":
                    return MagickFormat.Tiff;
                case "webp":
                    return MagickFormat.WebP;
            }
            return MagickFormat.Jpg;
        }
        private void SaveStatsToCSV_Click(object sender, RoutedEventArgs e) {
            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            if (saveFileDialog.ShowDialog() == true) {
                using (var writer = new StreamWriter(saveFileDialog.FileName))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture)) {
                    csv.WriteRecords(StatsCollection);
                }
                System.Windows.MessageBox.Show("Stats have been saved to CSV file: " + saveFileDialog.FileName, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnHelp_Click(object sender, RoutedEventArgs e) {
            string xpsFilePath = Path.Combine(GetExePath(), "CONVERT AND RESIZE IMAGE FOLDERS.xps");
            HelpViewer helpWindow = new HelpViewer();
            helpWindow.SetDocument(xpsFilePath);
            helpWindow.ShowDialog(); // or Show() if you prefer non-modal
        }
        //private void btnCloseHelp_Click(object sender, RoutedEventArgs e) {
        //    Help.IsOpen = false;
        //}

        private void winMain1_Unloaded(object sender, RoutedEventArgs e) {
            _ErrorLogFile.Close();
            _ErrorLogFile.Dispose();
        }

        private void txtCopyRight_LostFocus(object sender, RoutedEventArgs e) {
            Settings1.Default.CopyrightNotice = txtCopyRight.Text;
            Settings1.Default.Save();
        }

        private void chkFileName_LostFocus(object sender, RoutedEventArgs e) {
            Settings1.Default.AddFileName = (bool)chkFileName.IsChecked;
            Settings1.Default.Save();
        }

        private void txtOutputFolder_TextChanged(object sender, TextChangedEventArgs e) {
            _OutputFolder = txtOutputFolder.Text;
        }
    }
    public class ByteToMBConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is long byteCount) {
                return (byteCount / (1024 * 1024)).ToString("N0", CultureInfo.CurrentCulture);
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class ByteToKBConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is long byteCount) {
                return (byteCount / 1024).ToString("N0", CultureInfo.CurrentCulture);
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
    public class FolderStats {
        private string _folderName;
        public string FolderName {
            get => _folderName;
            set {
                if (value != null) {
                    int lastSlash = Math.Max(value.LastIndexOf('\\'), value.LastIndexOf('/'));
                    _folderName = lastSlash >= 0 ? value.Substring(lastSlash + 1) : value;
                }
                else {
                    _folderName = null;
                }
            }
        }
        public string InputFileType { get; set; }
        public string OutputFileType { get; set; }
        public string ResizeType { get; set; }
        public int FilesProcessed { get; set; }
        public long BytesRead { get; set; } // In bytes, can be converted to MB for display
        public long AvgInputFileSize { get; set; } // In bytes, can be converted to KB for display
        public long BytesWritten { get; set; }
        public long AvgOutputFileSize { get; set; }
        public long BytesSavedLost { get; set; } // In bytes, can be converted to MB and percentage
        public int ElapsedTime { get; set; }
        public long AvgTimePerFile { get; set; } // In milliseconds
    }
    public class WindowHandler {
        private Window window;

        public WindowHandler(Window window) {
            this.window = window;
        }

        /// <summary>
        /// Disables the window and sets the cursor to waiting state.
        /// </summary>
        public void StartProcessing() {
            // Disable the window to prevent user interaction
            window.IsEnabled = false;

            // Change the cursor to indicate waiting
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        }

        /// <summary>
        /// Re-enables the window and sets the cursor back to default.
        /// </summary>
        public void EndProcessing() {
            // Re-enable the window for user interaction
            window.IsEnabled = true;

            // Return the cursor to default state
            Mouse.OverrideCursor = null;
        }
    }
}