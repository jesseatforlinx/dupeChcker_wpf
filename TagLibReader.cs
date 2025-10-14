using TagLib;

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
            catch
            {
                return 0;
            }
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
    }
}
