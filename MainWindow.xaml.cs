using Ookii.Dialogs.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace DupeChecker
{   
    public partial class MainWindow : Window
    {
        private ObservableCollection<VideoFile> videoFiles = new();

        public MainWindow()
        {
            InitializeComponent();
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

        //双击打开视频文件
        private void FileListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FileListView.SelectedItem is VideoFile vf)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(vf.FullPath) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show($"无法打开文件: {ex.Message}"); }
            }
        }

        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
            {
                string sortBy = header.Content.ToString().Replace("▲", "").Replace("▼", "").Trim();
                ListSortDirection direction;

                if (_lastHeaderClicked == header)
                {
                    // 点击同一列，反转排序方向
                    direction = _lastDirection == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                }
                else
                {
                    // 新列默认升序
                    direction = ListSortDirection.Descending;
                }

                Sort(sortBy, direction);

                // 清理上一个箭头
                if (_lastHeaderClicked != null)
                {
                    string oldHeader = _lastHeaderClicked.Content.ToString().Replace("▲", "").Replace("▼", "").Trim();
                    _lastHeaderClicked.Content = oldHeader;
                }

                // 给当前列加箭头
                string baseHeader = sortBy;
                header.Content = $"{baseHeader} {(direction == ListSortDirection.Ascending ? "▲" : "▼")}";

                _lastHeaderClicked = header;
                _lastDirection = direction;
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            // 拿到当前View
            ICollectionView dataView = CollectionViewSource.GetDefaultView(FileListView.ItemsSource);

            dataView.SortDescriptions.Clear();

            switch (sortBy)
            {
                case "文件名":
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VideoFile.Name), direction));
                    break;
                case "大小":
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VideoFile.SizeBytes), direction));
                    break;
                case "时长":
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VideoFile.DurationSeconds), direction));
                    break;
                case "修改日期":
                    dataView.SortDescriptions.Add(new SortDescription(nameof(VideoFile.Modified), direction));
                    break;
            }

            dataView.Refresh();
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