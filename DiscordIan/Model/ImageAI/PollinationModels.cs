using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordIan.Model.ImageAI
{
    public class PollinationModels
    {
        public string name { get; set; }
        public List<string> aliases { get; set; }
        public Pricing pricing { get; set; }
        public string description { get; set; }
        public List<string> input_modalities { get; set; }
        public List<string> output_modalities { get; set; }
        public bool tools { get; set; }
        public bool reasoning { get; set; }
        public int context_window { get; set; }
        public List<string> voices { get; set; }
        public bool is_specialized { get; set; }
    }

    public class Pricing
    {
        public string currency { get; set; }
        public double promptTextTokens { get; set; }
        public double promptImageTokens { get; set; }
        public double completionImageTokens { get; set; }
    }
}
