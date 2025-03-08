using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Controls;
using ImageMagick;
using Microsoft.WindowsAPICodePack.Dialogs;
using static CRIMP.FolderSelector;
using ImageMagick.Drawing;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Media;
using System.Text;

namespace CRIMP {
    public partial class MainWindow : System.Windows.Window {
        private WindowHandler windowHandler;
        public ObservableCollection<FolderStats> StatsCollection { get; set; }
        string _RootFolder = string.Empty;
        List<string> _SourceFolders = new List<string>();
        List<string> _SelectedFolders = new List<string>();
        string _ErrorLogName = string.Empty;
        StreamWriter _ErrorLogFile;
        public List<string> _Errors = new List<string>();

        // Values that are stored via Settings1
        string _OutputFolder = string.Empty;
        uint _ImageQuality = 80;
        string _InputType = "jpg";
        uint? _ResizeAmount = null;
        bool _Ready = false;
        MagickFormat _OutputType = MagickFormat.Jpg;
        string _FontName;
        System.Windows.FontStyle _FontStyle;
        System.Windows.FontWeight _FontWeight;
        int _FontSize = 12;

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

        private enum speedRB {
            Fast,
            Slow
        }
        speedRB _SpeedRB = speedRB.Fast;
        public MainWindow() {
            try {
                InitializeComponent();
                windowHandler = new WindowHandler(this);
                FolderSelector.SelectedFoldersChanged += FolderSelector_SelectedFoldersChanged;
                StatsCollection = new ObservableCollection<FolderStats>();
                // StatsGrid.ItemsSource = StatsCollection;
                this.Closing += winMain1_Closing;
                Loaded += (s, e) => InitializeFonts();
            }
            catch (Exception ex) {
                MessageBox.Show($"Crash: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void winMain1_Loaded(object sender, RoutedEventArgs e) {
            LoadFileTypes(cboInputFileType);
            LoadFileTypes(cboOutputFileType);
            //InitializeFonts();
            FolderSelector.Enabled = true;
            LoadSettings();
            _Errors.Clear();
            _Ready = true;
        }

        private void winMain1_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            SaveSettings();
        }

        private void winMain1_Unloaded(object sender, RoutedEventArgs e) {
            // Doesn't get here
        }

        private string GetExePath() {
            string exePath = Assembly.GetExecutingAssembly().Location;

            // Extract the directory from the path
            return Path.GetDirectoryName(exePath);
        }
        private void InitializeFonts() {
            // Populate the ComboBox with available fonts
            foreach (var family in Fonts.SystemFontFamilies) {
                FontComboBox.Items.Add(family);
            }
            FontComboBox.SelectedIndex = 0; // Default selection
            _FontName = FontComboBox.SelectedItem?.ToString() ?? "Arial";
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

            _SpeedRB = Settings1.Default.Speed == "2" ? speedRB.Fast : speedRB.Slow;

            _ImageQuality = (uint)Settings1.Default.Quality;
            txtQuality.Text = _ImageQuality.ToString();

            txtCopyRight.Text = Settings1.Default.CopyrightNotice.ToString();
            if (txtCopyRight.Text.Length == 0) {
                txtCopyRight.Text = "©";
            }

            _FontSize = Settings1.Default.FontSize;
            txtFontSize.Text = _FontSize.ToString();
            chkBold.IsChecked = Settings1.Default.FontWeight;
            chkItalic.IsChecked = Settings1.Default.FontStyle;
            if (!string.IsNullOrEmpty(Settings1.Default.FontName)) {
                // Find the index of the font name in the ComboBox
                int index = FontComboBox.Items.IndexOf(Settings1.Default.FontName);
                if (index != -1) {
                    FontComboBox.SelectedIndex = index; // This will also trigger the SelectionChanged event, saving the font again
                }
            }

            chkSaveElsewhere.IsChecked = Settings1.Default.SaveElseWhere;
            _OutputFolder = Settings1.Default.OutputFolder;
            txtOutputFolder.Text = _OutputFolder;
        }

        private void SaveSettings() {

            Settings1.Default.ResizeType = _ResizeRB.ToString();
            Settings1.Default.KeepInput = _SourceRB == sourceRB.Keep;
            Settings1.Default.Speed = _SpeedRB == speedRB.Fast ? "2" : "8";

            string selectedFontName = FontComboBox.SelectedItem.ToString();
            Settings1.Default.FontName = selectedFontName;

            Settings1.Default.Quality = (int)_ImageQuality;

            Settings1.Default.SaveElseWhere = chkSaveElsewhere.IsChecked.Value;
            Settings1.Default.Amount = (int)_ResizeAmount;

            Settings1.Default.AddFileName = chkFileName.IsChecked.Value;
            Settings1.Default.OutputFolder = _OutputFolder;
            Settings1.Default.InputFileType = _InputType;
            Settings1.Default.OutputFileType = cboOutputFileType.SelectedItem.ToString();
            Settings1.Default.CopyrightNotice = txtCopyRight.Text;
            Settings1.Default.FontName = FontComboBox.SelectedItem.ToString();
            Settings1.Default.FontStyle = chkItalic.IsChecked.Value;
            Settings1.Default.FontWeight = chkBold.IsChecked.Value;
            Settings1.Default.FontSize = (int)_FontSize;

            Settings1.Default.Save();
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
            StateOfSaveButtons(false);
            _SourceFolders = e.Folders;
            if (_SourceFolders.Count == 0) {
                System.Windows.MessageBox.Show("No folders selected", "ERROR", MessageBoxButton.OK);
                return;
            }

            if (_SourceRB == sourceRB.DeleteOnly) {
                if (System.Windows.MessageBox.Show("You have chosen to delete every " + _InputType + " file in the checked folders. This is your final chance to keep the input files. Click \"Yes\" to keep them after all. Click \"No\" to say goodbye to them.",
                    "Check",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes) {
                    return;
                }
            }

            if (ValidationSuccessful()) {
                StatsCollection.Clear();
                _RootFolder = FolderSelector.RootFolder;
                windowHandler.StartProcessing();
                ConvertResizeDelete(_SourceFolders);
                windowHandler.EndProcessing();
                StateOfSaveButtons(true);
            }
        }

        private void StateOfSaveButtons(bool state) {
            btnSaveHTML.IsEnabled = state;
            btnSaveCSV.IsEnabled = state;
            btnSaveBoth.IsEnabled = state;
            btnClipBoard.IsEnabled = state;
        }

        private async void ConvertResizeDelete(List<string> folders) {

            _ErrorLogName = Path.Combine(Path.GetTempPath(), "Error_Log_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt");
            _ErrorLogFile = new StreamWriter(_ErrorLogName);

            uint newWidth = _ResizeAmount == null ? 0 : _ResizeAmount.Value;
            uint newHeight = newWidth;

            var searchPatterns = GetSearchPatterns();
            string outputType = _OutputType.ToString().ToLower();
            string copyRightNotice = txtCopyRight.Text;
            bool addFileName = chkFileName.IsChecked ?? false;
            bool differentFolder = chkSaveElsewhere.IsChecked ?? false;
            //string UserSelectedMethod = "0";
            string UserSelectedMethod = _SpeedRB == speedRB.Fast ? "2" : "6";

            // Stats collection
            DateTime startTime = DateTime.Now;

            progressBar.Value = 0;
            var progress = new Progress<int>(value => {
                // Update the UI with the progress
                progressBar.Value = value;
            });

            if (_SourceRB != sourceRB.DeleteOnly) {

                foreach (var folder in folders) {
                    var files = searchPatterns.SelectMany(pattern => Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly)).Distinct(StringComparer.OrdinalIgnoreCase);
                    int totalFiles = files.Count();
                    //var folderName = new DirectoryInfo(folder).Name; // Just the parent folder name
                    //this.Dispatcher.Invoke(() => folderLabel.Content = $"Processing: {folderName}");
                    this.Dispatcher.Invoke(() => folderLabel.Content = $"Processing: {folder}");
                    var folderStats = new FolderStats {
                        FolderName = folder,
                        InputFileType = _InputType.ToString(),
                        OutputFileType = _OutputType.ToString(),
                        ResizeType = _ResizeRB.ToString()
                    };
                    long totalBytesRead = 0, totalBytesWritten = 0;
                    int fileCount = 0;

                    await Task.Run(() => {
                        ((IProgress<int>)progress).Report(0);
                        ParallelOptions opts = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 2 }; // 23 cores
                        Parallel.ForEach(files, opts, file => {
                            string outputFile = SetOutputFilePath(file, differentFolder);
                            try {
                                using (var image = new MagickImage(file)) {
                                    if ((_OutputType == MagickFormat.WebP) && (image.Width > 16000 || image.Height > 16000)) {
                                        AutoResizeHugeImages(image); // In-place resize
                                    }
                                    long fileSize = new FileInfo(file).Length;
                                    Interlocked.Add(ref totalBytesRead, fileSize);

                                    MagickImage resizedImage = ResizeImage(image); // Might return image
                                    bool isCloned = resizedImage != image; // Check if cloned
                                    resizedImage.Format = _OutputType; // WebP
                                    resizedImage.Quality = _ImageQuality; // 80
                                    if (_OutputType == MagickFormat.WebP) {
                                        resizedImage.Settings.SetDefine(MagickFormat.WebP, "method", UserSelectedMethod); // Faster is "2", Slower is "6".
                                    }
                                    if (copyRightNotice.Length > 1 || addFileName) {
                                        AddTextItems(file, copyRightNotice, addFileName, resizedImage);
                                    }
                                    try {
                                        resizedImage.Write(outputFile);
                                    }
                                    catch (Exception ex) {
                                        HandleOutputException(outputFile, ex);
                                    }
                                    if (isCloned) resizedImage.Dispose(); // Dispose only if cloned
                                    fileSize = new FileInfo(outputFile).Length;
                                    Interlocked.Add(ref totalBytesWritten, fileSize);

                                    int localCount = Interlocked.Increment(ref fileCount);
                                    if (localCount % 20 == 0 || localCount == totalFiles) {
                                        ((IProgress<int>)progress).Report((int)((localCount * 100.0) / totalFiles));
                                    }
                                }
                            }
                            catch (Exception ex) {
                                HandleInputException(file, ex);
                            }
                        });

                        ((IProgress<int>)progress).Report(100);
                    });

                    UpdateStats(startTime, totalBytesRead, totalBytesWritten, fileCount, folderStats);
                    startTime = DateTime.Now;
                    lstErrorLog.Items.Clear();
                    foreach (string errorMsg in _Errors) {
                        lstErrorLog.Items.Add(errorMsg);
                    }
                }
                progressBar.Value = 100;
                await ResultsViewer.EnsureCoreWebView2Async(null); // Null uses default env
                ResultsViewer.NavigateToString(GenerateHtml(StatsCollection));
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
            _ErrorLogFile.Close();
            _ErrorLogFile.Dispose();
            txtErrorLog.Text = _ErrorLogName;
        }

        private static void AutoResizeHugeImages(MagickImage image) {
            // Resize to fit within the hardcoded limits of ImageMagick for WebP
            double scale = Math.Min(16000.0 / image.Width, 16000.0 / image.Height);
            uint newWidth = (uint)(image.Width * scale);
            uint newHeight = (uint)(image.Height * scale);
            image.Resize(newWidth, newHeight);
        }

        private void DeleteSourceFiles(List<string> folders, string[]? searchPatterns) {
            string outputType = _OutputType.ToString().ToLower();
            foreach (var folder in folders) {
                var files = searchPatterns.SelectMany(pattern => Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly)).Distinct(StringComparer.OrdinalIgnoreCase);
                ParallelOptions parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = (Environment.ProcessorCount * 3) / 4 }; // Use 3/4 the available cores
                Parallel.ForEach(files, parallelOptions, file => {

                    if (File.Exists(file)) {
                        if (FileThrewNoErrors(file)) {
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
                    }
                });
            }
            System.Windows.MessageBox.Show("Deletion phase complete!");
        }

        private bool FileThrewNoErrors(string file) {

            foreach (string error in _Errors) {
                if (error.Contains(file)) {
                    return false;
                }
            }
            return true;
        }

        private MagickImage ResizeImage(MagickImage image) {
            if (_ResizeRB == resizeRB.None) {
                return image; // No clone, use original
            }

            uint newWidth = image.Width;
            uint newHeight = image.Height;
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
            var resizedImage = (MagickImage)image.Clone();
            resizedImage.Resize(new MagickGeometry(newWidth, newHeight));
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
            if (System.Windows.MessageBox.Show(message + " Continue (Yes) [recommended] or quit (No)?",
                "Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error) == MessageBoxResult.No) {
                System.Windows.Application.Current.Shutdown();
            }
        }
        private void HandleInputException(string inputFile, Exception ex) {
            string message = "Could not process " + inputFile + " because " + ex.Message;
            _Errors.Add(message);
            _ErrorLogFile.WriteLineAsync(message);
            if (System.Windows.MessageBox.Show(message + " Continue (Yes) [recommended] or quit (No)?",
                "Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error) == MessageBoxResult.No) {
                System.Windows.Application.Current.Shutdown();
            }
        }
        private void AddTextItems(string file, string copyRightNotice, bool addFileName, MagickImage image) {
            // Calculate font size based on image dimensions
            int fontSize = (_FontSize < 8 || _FontSize > 64) ? (int)Math.Max(8, Math.Min(image.Width, image.Height) * 0.025) : _FontSize;

            // Estimate character width (this is very approximate)
            double charWidth = fontSize * 0.6; // assuming half the font size for each character width as an estimate

            // Create a drawable object to add text
            var drawables = new Drawables();

            // Add copyright notice (bottom-right corner)
            if (copyRightNotice.Length > 1) {
                string copyRight = copyRightNotice;
                double textWidth = copyRight.Length * charWidth; // Estimate text width
                int indent = (int)(textWidth + 10); // Add some extra padding

                drawables.FontPointSize(fontSize)
                         .Font(_FontName)
                         .FillColor(MagickColors.White)
                         .Text(image.Width - indent, image.Height - 20, copyRight) // White shadow
                         .FillColor(MagickColors.Black)
                         .Text(image.Width - indent + 2, image.Height - 17, copyRight); // Black text
            }

            // Add filename (bottom-left corner)
            if (addFileName) {
                string fileName = Path.GetFileNameWithoutExtension(file) + "." + _OutputType.ToString().ToLower();
                drawables.FontPointSize(fontSize)
                         .Font(_FontName)
                         .FillColor(MagickColors.White)
                         .Text(10, image.Height - 20, fileName) // White shadow
                         .FillColor(MagickColors.Black)
                         .Text(8, image.Height - 17, fileName); // Black text
            }

            // Draw the text onto the image
            drawables.Draw(image);
        }

        private string GenerateHtml(ObservableCollection<FolderStats> stats) {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><title>Crimp Results</title>");
            html.AppendLine("<style>");
            html.AppendLine("table { border-collapse: collapse; width: 95%; margin: 15px auto; font-family: 'Segoe UI', Arial, sans-serif; font-size: 14px; border: 1px solid #ccc; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }");
            html.AppendLine("th, td { border: 1px solid #bbb; padding: 8px; text-align: right; }");
            html.AppendLine("th { background-color: #78A8C0; color: white; font-weight: bold; text-transform: uppercase; }");
            html.AppendLine("tr:nth-child(odd) { background-color: #A8D0E0; }");
            html.AppendLine("tr:hover { background-color: #e0f7fa; }");
            html.AppendLine(".total { font-weight: bold; background-color: #d0e8f2; color: #004d40; }");
            html.AppendLine(".folder { text-align: left; font-style: italic; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            html.AppendLine("<h2 style='text-align: center; font-family: \"Segoe UI\", Arial, sans-serif; color: #21618C;'>Processing Results</h2>");
            html.AppendLine("<table><tr><th>Folder</th><th>Files</th><th>Input (MB)</th><th>Output (MB)</th><th>Savings (MB)</th><th>Time (sec)</th><th>Avg Input (KB)</th><th>Avg Output (KB)</th><th>Avg Time/File (ms)</th></tr>");

            double totalInput = 0, totalOutput = 0, totalSavings = 0;
            int totalFiles = 0, totalTime = 0;
            foreach (var stat in stats) {
                html.AppendLine($"<tr><td>{stat.FolderName}</td><td>{stat.FilesProcessed}</td><td>{(stat.BytesRead / 1024 / 1024):N0}</td><td>{(stat.BytesWritten / 1024 / 1024):N0}</td><td>{(stat.BytesSavedLost / 1024 / 1024):N0}</td><td>{stat.ElapsedTime}</td><td>{(stat.AvgInputFileSize / 1024):N0}</td><td>{(stat.AvgOutputFileSize / 1024):N0}</td><td>{stat.AvgTimePerFile}</td></tr>");
                totalInput += stat.BytesRead; totalOutput += stat.BytesWritten; totalSavings += stat.BytesSavedLost;
                totalFiles += stat.FilesProcessed; totalTime += stat.ElapsedTime;
            }
            html.AppendLine($"<tr class='total'><td>Total</td><td>{totalFiles}</td><td>{(totalInput / 1024 / 1024):N0}</td><td>{(totalOutput / 1024 / 1024):N0}</td><td>{(totalSavings / 1024 / 1024):N0}</td><td>{totalTime}</td><td>{(totalFiles == 0 ? 0 : totalInput / totalFiles / 1024):N0}</td><td>{(totalFiles == 0 ? 0 : totalOutput / totalFiles / 1024):N0}</td><td>{(totalFiles == 0 ? 0 : totalTime * 1000 / totalFiles)}</td></tr>");
            html.AppendLine("</table></body></html>");
            return html.ToString();
        }

        private void SaveHtml_Click(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "HTML Files (*.html)|*.html", DefaultExt = "html" };
            if (dialog.ShowDialog() == true) {
                File.WriteAllText(dialog.FileName, GenerateHtml(StatsCollection));
            }
        }

        private void SaveCsv_Click(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", DefaultExt = "csv" };
            if (dialog.ShowDialog() == true) {
                var csv = new StringBuilder();
                csv.AppendLine("Folder,Files,Input Size (MB),Output Size (MB),Savings (MB),Time (s),Avg Input (KB),Avg Output (KB),Avg Time/File (ms)");
                foreach (var stat in StatsCollection) {
                    csv.AppendLine($"{EscapeCsv(stat.FolderName)}," +
                                   $"{stat.FilesProcessed}," +
                                   $"\"{stat.BytesRead / 1024 / 1024:N0}\"," +
                                   $"\"{stat.BytesWritten / 1024 / 1024:N0}\"," +
                                   $"\"{stat.BytesSavedLost / 1024 / 1024:N0}\"," +
                                   $"{stat.ElapsedTime}," +
                                   $"\"{stat.AvgInputFileSize / 1024:N0}\"," +
                                   $"\"{stat.AvgOutputFileSize / 1024:N0}\"," +
                                   $"\"{stat.AvgTimePerFile:N0}\"");
                }
                File.WriteAllText(dialog.FileName, csv.ToString());
            }
        }

        private string EscapeCsv(string value) {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n")) {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }

        private void SaveBoth_Click(object sender, RoutedEventArgs e) {
            SaveHtml_Click(sender, e);
            SaveCsv_Click(sender, e);
        }

        private string GenerateHtmlTable(ObservableCollection<FolderStats> stats) {
            var table = new StringBuilder();
            table.AppendLine("<table><tr><th>Folder</th><th>Files</th><th>Input (MB)</th><th>Output (MB)</th><th>Savings (MB)</th><th>Time (s)</th><th>Avg Input (KB)</th><th>Avg Output (KB)</th><th>Avg Time/File (ms)</th></tr>");

            double totalInput = 0, totalOutput = 0, totalSavings = 0;
            int totalFiles = 0, totalTime = 0;
            foreach (var stat in stats) {
                table.AppendLine($"<tr><td class='folder'>{stat.FolderName}</td><td>{stat.FilesProcessed}</td><td>{(stat.BytesRead / 1024 / 1024):N0}</td><td>{(stat.BytesWritten / 1024 / 1024):N0}</td><td>{(stat.BytesSavedLost / 1024 / 1024):N0}</td><td>{stat.ElapsedTime}</td><td>{(stat.AvgInputFileSize / 1024):N0}</td><td>{(stat.AvgOutputFileSize / 1024):N0}</td><td>{stat.AvgTimePerFile}</td></tr>");
                totalInput += stat.BytesRead; totalOutput += stat.BytesWritten; totalSavings += stat.BytesSavedLost;
                totalFiles += stat.FilesProcessed; totalTime += stat.ElapsedTime;
            }
            table.AppendLine($"<tr class='total'><td>Total</td><td>{totalFiles}</td><td>{(totalInput / 1024 / 1024):N0}</td><td>{(totalOutput / 1024 / 1024):N0}</td><td>{(totalSavings / 1024 / 1024):N0}</td><td>{totalTime}</td><td>{(totalFiles == 0 ? 0 : totalInput / totalFiles / 1024):N0}</td><td>{(totalFiles == 0 ? 0 : totalOutput / totalFiles / 1024):N0}</td><td>{(totalFiles == 0 ? 0 : totalTime * 1000 / totalFiles)}</td></tr>");
            table.AppendLine("</table>");
            return table.ToString();
        }

        private void CopyToClipboard_Click(object sender, RoutedEventArgs e) {
            Clipboard.SetText(GenerateHtmlTable(StatsCollection));
        }

        private string SetOutputFilePath(string file, bool differentFolder) {
            if (_OutputFolder != string.Empty && differentFolder) {
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
            if (_OutputType.ToString().ToLower() == _InputType && txtOutputFolder.Text == string.Empty && _SourceRB == sourceRB.DeleteOnly) {
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

        private void chkSaveElsewhere_Click(object sender, RoutedEventArgs e) {
            btnOutputFolder.IsEnabled = chkSaveElsewhere.IsChecked.Value;
            Settings1.Default.SaveElseWhere = chkSaveElsewhere.IsChecked.Value;
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
            rbFastMode.IsEnabled = _OutputType == MagickFormat.WebP;
            rbSlowMode.IsEnabled = _OutputType == MagickFormat.WebP;
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

        private void btnHelp_Click(object sender, RoutedEventArgs e) {
            string xpsFilePath = Path.Combine(GetExePath(), "CRIMP.xps");
            HelpViewer helpWindow = new HelpViewer();
            helpWindow.SetDocument(xpsFilePath);
            helpWindow.ShowDialog(); // or Show() if you prefer non-modal
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


        private void OpenErrorLog_Click(object sender, RoutedEventArgs e) {
            // remove invocation of NotePad for store.
        }

        private void chkItalic_Checked(object sender, RoutedEventArgs e) {
            UpdateFontStyle();
        }

        private void chkItalic_Unchecked(object sender, RoutedEventArgs e) {
            UpdateFontStyle();
        }
        private void UpdateFontStyle() {
            // Apply the font style to the ComboBox or its items
            var style = chkItalic.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;

            // If you want to change the ComboBox's text style directly:
            FontComboBox.FontStyle = style;
        }

        private void chkBold_Unchecked(object sender, RoutedEventArgs e) {
            UpdateFontWeight();
        }

        private void chkBold_Checked(object sender, RoutedEventArgs e) {
            UpdateFontWeight();
        }

        private void UpdateFontWeight() {
            var weight = chkBold.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
            FontComboBox.FontWeight = weight;
        }

        private void txtFontSize_TextChanged(object sender, TextChangedEventArgs e) {
            if (int.TryParse(txtFontSize.Text, out int value)) {
                if (value > 8 && value < 64) {
                    FontComboBox.FontSize = value;
                }
                _FontSize = value;
            }
        }

        private void FontComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (FontComboBox.SelectedItem != null) {
                string selectedFontName = FontComboBox.SelectedItem.ToString();
                //Settings1.Default.FontName = selectedFontName;
                //Settings1.Default.Save();
            }
        }

        private void rbFastMode_Click(object sender, RoutedEventArgs e) {
            _SpeedRB = rbFastMode.IsChecked.Value ? speedRB.Fast : speedRB.Slow;
            Settings1.Default.Speed = rbFastMode.IsChecked.Value ? "2" : "8";
            Settings1.Default.Save();
        }

        private void rbSlowMode_Click(object sender, RoutedEventArgs e) {
            _SpeedRB = rbSlowMode.IsChecked.Value ? speedRB.Slow : speedRB.Fast;
            Settings1.Default.Speed = rbSlowMode.IsChecked.Value ? "8" : "2";
            Settings1.Default.Save();
        }

        private void FontComboBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e) {
            if (FontComboBox.SelectedItem is FontFamily font) {
                _FontName = font.ToString(); // Update on change
            }
        }
    }

    public class StringToFontFamilyConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return value is string fontName ? new FontFamily(fontName) : new FontFamily("Arial");
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class FontNameToFontFamilyConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string fontName) {
                return new System.Windows.Media.FontFamily(fontName);
            }
            return null;
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
        private System.Windows.Window window;

        public WindowHandler(System.Windows.Window window) {
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