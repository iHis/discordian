using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordIan.Model
{
    public class ImgCacheModel
    {
        public ulong UserId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public string Prompt { get; set; }
    }
}
