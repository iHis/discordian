using System;
using System.Collections.Generic;

namespace DiscordIan.Model
{
    public class HistoryModel
    {
        public List<HistoryItem> HistoryList { get; set; }
    }

    public class HistoryItem
    {
        public string UserName { get; set; }
        public string Service { get; set; }
        public string Input { get; set; }
        public string Timing { get; set; }
        public DateTime AddDate { get; set; }
    }
}
