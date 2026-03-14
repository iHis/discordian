using System;

namespace DiscordIan.Model.ImageAI
{
    public class ImgRequestModel
    {
        public string Model { get; set; } = "flux";
        public string Seed { get; set; } = "-1";
        public string Prompt { get; set; }
        public string ImageUrl { get; set; }
    }
}
