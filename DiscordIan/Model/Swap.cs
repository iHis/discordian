﻿using System.Collections.Generic;

namespace DiscordIan.Model
{
    public class WordSwaps
    {
        public List<Swap> SwapList { get; set; }
    }

    public class Swap
    {
        public string inbound { get; set; }
        public string outbound { get; set; }
    }
}
