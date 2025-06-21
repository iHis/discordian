using System;

namespace DiscordIan.Model.ImageAI
{
    public class ImgCacheModel
    {
        public DateTime Timestamp { get; set; }
        public ulong UserId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }
        public ImgRequestModel Request { get; set; }
    }
}
