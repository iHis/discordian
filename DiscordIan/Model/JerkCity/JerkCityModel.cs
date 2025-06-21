using System.Collections.Generic;

namespace DiscordIan.Model.JerkCity
{
    public class JerkCityModel
    {
        public class Hifi
        {
            public string audio_url { get; set; }
            public string author { get; set; }
            public int create_date { get; set; }
            public string description { get; set; }
            public string duration { get; set; }
            public int hifi_id { get; set; }
            public string page_url { get; set; }
        }

        public class Episode
        {
            public int day { get; set; }
            public IList<IList<string>> dialog { get; set; }
            public int episode { get; set; }
            public int height { get; set; }
            public string image { get; set; }
            public int month { get; set; }
            public IList<string> players { get; set; }
            public double score { get; set; }
            public IList<string> tags { get; set; }
            public string thumb { get; set; }
            public string title { get; set; }
            public int width { get; set; }
            public int year { get; set; }
            public Hifi hifi { get; set; }
        }

        public class Hd
        {
            public string author { get; set; }
            public int height { get; set; }
            public string thumb_url { get; set; }
            public string url { get; set; }
            public int width { get; set; }
        }

        public class FrogTip
        {
            public string created_at { get; set; }
            public string source { get; set; }
            public string text { get; set; }
        }

        public class Bonequesthifi
        {
            public string created_at { get; set; }
            public string source { get; set; }
            public string text { get; set; }
        }

        public class Deucequest
        {
            public string created_at { get; set; }
            public string source { get; set; }
            public string text { get; set; }
        }

        public class Effigyquest
        {
            public string created_at { get; set; }
            public string source { get; set; }
            public string text { get; set; }
        }

        public class Jerkcityhd
        {
            public string created_at { get; set; }
            public string source { get; set; }
            public string text { get; set; }
        }

        public class Pantsquest
        {
            public string created_at { get; set; }
            public string source { get; set; }
            public string text { get; set; }
        }

        public class Spigotthebear
        {
            public string created_at { get; set; }
            public string source { get; set; }
            public string text { get; set; }
        }

        public class Tweets
        {
            public IList<FrogTip> FrogTips { get; set; }
            public IList<Bonequesthifi> bonequesthifi { get; set; }
            public IList<Deucequest> deucequest { get; set; }
            public IList<Effigyquest> effigyquest { get; set; }
            public IList<Jerkcityhd> jerkcityhd { get; set; }
            public IList<Pantsquest> pantsquest { get; set; }
            public IList<Spigotthebear> spigotthebear { get; set; }
        }

        public class Meta
        {
            public bool gay { get; set; }
            public IList<Hd> hd { get; set; }
            public int high { get; set; }
            public Tweets tweets { get; set; }
            public string version { get; set; }
        }

        public class Sums
        {
            public int dates { get; set; }
            public int episodes { get; set; }
            public int tags { get; set; }
            public int titles { get; set; }
            public int words { get; set; }
        }

        public class Search
        {
            public int limit { get; set; }
            public string query { get; set; }
            public double runtime { get; set; }
            public Sums sums { get; set; }
        }

        public class JerkResponse
        {
            public IList<Episode> episodes { get; set; }
            public Meta meta { get; set; }
            public Search search { get; set; }
        }
    }
}
