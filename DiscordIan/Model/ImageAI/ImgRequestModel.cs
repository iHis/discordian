using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordIan.Model.ImageAI
{
    public class ImgRequestModel
    {
        public string Model { get; set; } = "flux";
        public string Seed { get; set; } = new Random().Next(1, 99999).ToString();
        public string Prompt { get; set; }
    }
}
