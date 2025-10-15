namespace DupeChecker
{
    public static class TagLibReader
    {

        public static int GetDurationSeconds(string filePath)
        {
            try
            {
                var tfile = TagLib.File.Create(filePath);
                return (int)tfile.Properties.Duration.TotalSeconds;
            }
            catch{  }

            // fallback：使用Windows Shell属性读取时长（单位：秒）
            return GetShellDuration(filePath);
        }

        /// <summary>
        /// 获取视频时长（格式化 mm:ss 或 hh:mm:ss）
        /// </summary>
        public static string GetFormattedDuration(string filePath)
        {
            int seconds = GetDurationSeconds(filePath);
            if (seconds <= 0) return "";

            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        private static int GetShellDuration(string filePath)
        {
            try
            {
                Type shellAppType = Type.GetTypeFromProgID("Shell.Application");
                dynamic shell = Activator.CreateInstance(shellAppType);
                string folderPath = System.IO.Path.GetDirectoryName(filePath);
                string fileName = System.IO.Path.GetFileName(filePath);
                dynamic folder = shell.NameSpace(folderPath);
                dynamic item = folder?.ParseName(fileName);
                if (item != null)
                {
                    // 遍历属性列找到“长度”
                    int durationIndex = -1;
                    for (int i = 0; i < 300; i++)
                    {
                        string colName = folder.GetDetailsOf(null, i);
                        if (!string.IsNullOrEmpty(colName) &&
                            (colName.ToLower().Contains("length") || colName.Contains("时长")))
                        {
                            durationIndex = i;
                            break;
                        }
                    }

                    if (durationIndex >= 0)
                    {
                        string durStr = folder.GetDetailsOf(item, durationIndex);
                        if (TimeSpan.TryParse(durStr, out var ts))
                            return (int)ts.TotalSeconds;

                        // fallback: mm:ss 手动解析
                        var parts = durStr.Split(':');
                        if (parts.Length == 3)
                            return int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + int.Parse(parts[2]);
                        if (parts.Length == 2)
                            return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellDuration] Exception: {ex.Message}");
            }

            return 0;
        }
    }
}
