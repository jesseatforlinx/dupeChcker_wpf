using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using Shell32;


namespace DupeChecker
{
    public static class ShellPropertyReader
    {
        /// <summary>
        /// 获取视频文件时长（格式化为 mm:ss 或 hh:mm:ss）
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

        /// <summary>
        /// 获取视频文件时长（秒）
        /// </summary>
        public static int GetDurationSeconds(string filePath)
        {
            try
            {
                string folderPath = System.IO.Path.GetDirectoryName(filePath);
                string fileName = System.IO.Path.GetFileName(filePath);

                Shell shell = new Shell();
                Folder folder = shell.NameSpace(folderPath);
                if (folder == null) return 0;

                FolderItem item = folder.ParseName(fileName);
                if (item == null) return 0;

                // 27 是 Explorer “长度”列（通常适用于视频文件）
                string dur = folder.GetDetailsOf(item, 27);
                if (string.IsNullOrEmpty(dur)) return 0;

                // dur 可能是 "mm:ss" 或 "hh:mm:ss"
                string[] parts = dur.Split(':');
                if (parts.Length == 3)
                    return int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + int.Parse(parts[2]);
                if (parts.Length == 2)
                    return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
            }
            catch
            {
                // 出错返回 0
            }
            return 0;
        }
    }
}
