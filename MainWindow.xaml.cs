using Ookii.Dialogs.Wpf;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DupeChecker
{   
    public partial class MainWindow : Window
    {
        private ObservableCollection<VideoFile> videoFiles = new();

        public MainWindow()
        {
            InitializeComponent();
            ApplyWindowClip();
            Loaded += (_, __) => EnableBlur();
            
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

        private void FolderPathBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb && !tb.IsKeyboardFocusWithin)
            {
                e.Handled = true; // 阻止默认点击行为
                tb.Focus();       // 设置焦点
                tb.SelectAll();   // 全选内容
            }
        }

        private void FolderPathBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null)
            {
                tb.SelectAll();
            }
        }

        private async void Sort_Click(object sender, RoutedEventArgs e)
        {
            await LoadFilesAsync();
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
                            UpdateFileStats();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法删除文件: {ex.Message}");
                    }
                }
            }
        }

        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader header && header.Column != null)
            {
                string sortBy = header.Content.ToString().Replace("▲", "").Replace("▼", "").Trim();

                // 决定排序方向
                ListSortDirection direction = GetSortDirection(header);

                // 排序数据
                Sort(sortBy, direction);

                // 更新箭头显示
                UpdateHeaderArrow(header, direction);
            }
        }

        /// <summary>
        /// 根据点击的列决定排序方向
        /// </summary>
        private ListSortDirection GetSortDirection(GridViewColumnHeader header)
        {
            if (_lastHeaderClicked == header)
                return _lastDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

            return ListSortDirection.Descending; // 新列默认降序
        }

        /// <summary>
        /// 更新箭头显示
        /// </summary>
        private void UpdateHeaderArrow(GridViewColumnHeader header, ListSortDirection direction)
        {
            // 清理上一个箭头
            if (_lastHeaderClicked != null)
            {
                string oldHeader = _lastHeaderClicked.Content.ToString().Replace("▲", "").Replace("▼", "").Trim();
                _lastHeaderClicked.Content = oldHeader;
            }

            // 给当前列加箭头
            string baseHeader = header.Content.ToString().Replace("▲", "").Replace("▼", "").Trim();
            header.Content = $"{baseHeader} {(direction == ListSortDirection.Ascending ? "▲" : "▼")}";

            _lastHeaderClicked = header;
            _lastDirection = direction;
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

            var files = GetVideoFiles(folderPath);
            videoFiles.Clear();
            FileListView.ItemsSource = videoFiles; // 只绑定一次

            var videoFileList = await Task.Run(() => LoadVideoFileMetadata(files));
            // 默认排序
            var sortedList = videoFileList.OrderByDescending(v => v.DurationSeconds).ToList();

            // 更新 UI
            foreach (var vf in sortedList)
                videoFiles.Add(vf);

            UpdateFileStats();
        }

        /// <summary>
        /// 获取指定目录及子目录下的所有视频文件路径
        /// </summary>
        private List<string> GetVideoFiles(string folderPath)
        {
            string[] exts = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".ts", ".m4v" };
            return Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                            .Where(f => exts.Contains(Path.GetExtension(f).ToLower()))
                            .ToList();
        }

        /// <summary>
        /// 并行读取视频文件元数据
        /// </summary>
        private List<VideoFile> LoadVideoFileMetadata(List<string> files)
        {
            var list = new System.Collections.Concurrent.ConcurrentBag<VideoFile>();

            System.Threading.Tasks.Parallel.ForEach(files, file =>
            {
                try
                {
                    long sizeBytes = new FileInfo(file).Length;
                    int durationSeconds = TagLibReader.GetDurationSeconds(file);
                    string duration = TagLibReader.GetFormattedDuration(file);

                    list.Add(new VideoFile
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        Size = FormatSize(sizeBytes),
                        SizeBytes = sizeBytes,
                        Duration = duration,
                        DurationSeconds = durationSeconds,
                        Modified = File.GetLastWriteTime(file)
                    });
                }
                catch { }
            });

            return list.ToList();
        }

        /// <summary>
        /// 更新文件数量和最近修改日期显示
        /// </summary>
        private void UpdateFileStats()
        {
            FileCountText.Text = $"{videoFiles.Count} files";
            if (videoFiles.Count > 0)
            {
                var recent = videoFiles.Max(f => f.Modified);
                MostRecentText.Text = $"Last: {recent.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024 * 1000)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            else
                return $"{bytes / (1024.0 * 1024):F1} MB";
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            var anim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(120)));
            anim.Completed += (s, _) =>
            {
                // 用 BeginInvoke 延迟到下一帧执行最小化
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.BeginAnimation(Window.OpacityProperty, null); // 停止动画
                    this.Opacity = 1;
                    this.WindowState = WindowState.Minimized;
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            };
            this.BeginAnimation(Window.OpacityProperty, anim);
        }
        private void Maximize_Click(object sender, RoutedEventArgs e) => this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) // 双击最大化/还原
            {
                WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
            }
            else
            {
                DragMove();
            }
        }

        #region 窗口样式
        private void ApplyWindowClip()
        {
            var radius = 5; // 圆角半径
            var rect = new RectangleGeometry();
            rect.RadiusX = radius;
            rect.RadiusY = radius;
            rect.Rect = new Rect(0, 0, this.Width, this.Height);
            this.Clip = rect;

            // 窗口大小改变时也要更新
            this.SizeChanged += (s, e) =>
            {
                rect.Rect = new Rect(0, 0, this.ActualWidth, this.ActualHeight);
            };
        }

        private void EnableBlur()
        {
            var windowHelper = new WindowInteropHelper(this);

            var accent = new AccentPolicy();
            accent.AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND;
           
            accent.GradientColor = unchecked((int)0xD9F3F1F1); ;



            int sizeOfAccent = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(sizeOfAccent);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                SizeOfData = sizeOfAccent,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4, // Windows 10 1803+
            ACCENT_ENABLE_HOSTBACKDROP = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        #endregion

        private async void BtnSort_Click(object sender, RoutedEventArgs e)
        {
            await LoadFilesAsync();
        }
    }
}