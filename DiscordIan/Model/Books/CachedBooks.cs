using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordIan.Model
{
    public class CachedBooks
    {
        public int LastViewedBook { get; set; }
        public BookList BookList { get; set; }
    }
}
