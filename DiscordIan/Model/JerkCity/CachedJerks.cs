using System;

namespace DiscordIan.Model.JerkCity
{
    internal class CachedJerks
    {
        public DateTime CreatedAt { get; set; }
        public int LastViewedJerk { get; set; }
        public string[] JerkList { get; set; }
        public string SearchString { get; set; }
    }
}
