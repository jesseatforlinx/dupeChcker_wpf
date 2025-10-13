using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DupeChecker
{
    class VideoFile
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Size { get; set; }
        public long SizeBytes { get; set; }
        public string Duration { get; set; }
        public int DurationSeconds { get; set; }
        public DateTime Modified { get; set; }
    }
}
