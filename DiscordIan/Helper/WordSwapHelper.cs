using System.IO;

namespace DiscordIan.Helper
{
    public class WordSwapHelper
    {
        public static string GetListFromFile()
        {
            var file = "WordSwap.json";
            if (File.Exists(file))
            {
                using (StreamReader r = new StreamReader(file))
                {
                    return r.ReadToEnd();
                }
            }

            return string.Empty;
        }
    }
}
