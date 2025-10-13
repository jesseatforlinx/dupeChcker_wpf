using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System.Collections.ObjectModel;
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

namespace DupeChecker
{   
    public partial class MainWindow : Window
    {
        private ObservableCollection<VideoFile> videoFiles = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SortBySize_Click(object sender, RoutedEventArgs e)
        {
            var sorted = videoFiles.OrderByDescending(f => f.SizeBytes).ToList();
            videoFiles.Clear();
            foreach (var v in sorted) videoFiles.Add(v);
        }

        private void SortByDate_Click(object sender, RoutedEventArgs e)
        {
            var sorted = videoFiles.OrderByDescending(f => f.Modified).ToList();
            videoFiles.Clear();
            foreach (var v in sorted) videoFiles.Add(v);
        }

        private void SortByDuration_Click(object sender, RoutedEventArgs e)
        {
            var sorted = videoFiles.OrderByDescending(f => f.DurationSeconds).ToList();
            videoFiles.Clear();
            foreach (var v in sorted) videoFiles.Add(v);
        }

        private async void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select Folder",
                UseDescriptionForTitle = true,
            };

            if(dialog.ShowDialog(this) == true)
            {
                FolderPathBox.Text = dialog.SelectedPath;
                await LoadFilesAsync();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string filePath)
            {
                if (MessageBox.Show($"确定要删除文件吗？\n{filePath}", "确认删除", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Delete(filePath);
                        var itemToRemove = videoFiles.FirstOrDefault(f => f.FullPath == filePath);
                        if (itemToRemove != null)
                        {
                            videoFiles.Remove(itemToRemove);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法删除文件: {ex.Message}");
                    }
                }
            }
        }

        private void FileListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FileListView.SelectedItem is VideoFile vf)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(vf.FullPath) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show($"无法打开文件: {ex.Message}"); }
            }
        }

        private async Task LoadFilesAsync()
        {
            string folderPath = FolderPathBox.Text.Trim();
            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show("文件夹路径不存在");
                return;
            }

            string[] exts = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".ts", ".m4v" };
            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => exts.Contains(System.IO.Path.GetExtension(f).ToLower()))
                                 .ToList();

            videoFiles.Clear();
            FileListView.ItemsSource = videoFiles; // 只绑定一次

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    long sizeBytes = new FileInfo(file).Length;
                    string duration = null;

                    // 在 UI 线程调用 COM
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        duration = ShellPropertyReader.GetFormattedDuration(file);
                        videoFiles.Add(new VideoFile
                        {
                            Name = System.IO.Path.GetFileName(file),
                            FullPath = file,
                            Size = FormatSize(sizeBytes),
                            SizeBytes = sizeBytes,
                            Duration = duration,
                            DurationSeconds = ConvertDurationToSeconds(duration),
                            Modified = File.GetLastWriteTime(file)
                        });
                    });
                }
            });

            // ❌ 删除下面这一行，不要再赋值给 ItemsSource
            // FileListView.ItemsSource = videoFiles.OrderByDescending(f => f.SizeBytes).ToList();

            FileCountText.Text = $"{videoFiles.Count} files";
            if (videoFiles.Count > 0)
            {
                var recent = videoFiles.Max(f => f.Modified);
                MostRecentText.Text = $"Last: {recent:MMM dd, yyyy}";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1000)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            else
                return $"{bytes / (1024.0 * 1024):F1} MB";
        }

        private static int ConvertDurationToSeconds(string duration)
        {
            try
            {
                var parts = duration.Split(':').Select(int.Parse).ToArray();
                if (parts.Length == 3)
                    return parts[0] * 3600 + parts[1] * 60 + parts[2];
                if (parts.Length == 2)
                    return parts[0] * 60 + parts[1];
            }
            catch { }
            return 0;
        }
    }
}