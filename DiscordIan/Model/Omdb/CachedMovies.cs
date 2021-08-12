using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordIan.Model.Omdb
{
    public class CachedMovies
    {
        public int LastViewedMovie { get; set; }
        public OmdbStub MovieStubs { get; set; }
    }
}
