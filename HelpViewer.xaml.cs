using System;
using System.IO;
using System.Windows;
using System.Windows.Xps.Packaging;

namespace ConvertFoldersToWebP
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class HelpViewer : Window
    {
        public HelpViewer()
        {
            InitializeComponent();
        }
        public void SetDocument(string filePath) {
            if (File.Exists(filePath)) {
                try {
                    using (var xpsDocument = new XpsDocument(filePath, FileAccess.Read)) {
                        helpViewer.Document = xpsDocument.GetFixedDocumentSequence();
                    }
                }
                catch (Exception ex) {
                    System.Windows.MessageBox.Show($"An error occurred while opening the help file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else {
                System.Windows.MessageBox.Show("Help file not found", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
