using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace DiscordIan.Model.GameReview
{
    public class GameSummaryModel
    {
        public partial class Summary
        {
            public double Count { get; set; }

            public Uri Next { get; set; }

            public object Previous { get; set; }

            public Result[] Results { get; set; }

            public bool User_Platforms { get; set; }
        }

        public partial class Result
        {
            public string Slug { get; set; }

            public string Name { get; set; }

            public double Playtime { get; set; }

            public Platform[] Platforms { get; set; }

            public Store[] Stores { get; set; }

            public DateTimeOffset? Released { get; set; }

            public bool Tba { get; set; }

            public Uri Background_Image { get; set; }

            public double Rating { get; set; }

            public double Rating_Top { get; set; }

            public Rating[] Ratings { get; set; }

            public double Ratings_Count { get; set; }

            public double Reviews_Text_Count { get; set; }

            public double Added { get; set; }

            public AddedByStatus Added_By_Status { get; set; }

            public double? Metacritic { get; set; }

            public double Suggestions_Count { get; set; }

            public double Id { get; set; }

            public string Score { get; set; }

            public Clip Clip { get; set; }

            public Tag[] Tags { get; set; }

            public object User_Game { get; set; }

            public double Reviews_Count { get; set; }

            public string Saturated_Color { get; set; }

            public string Dominant_Color { get; set; }

            public ShortScreenshot[] Short_Screenshots { get; set; }

            public Platform[] Parent_Platforms { get; set; }

            public Genre[] Genres { get; set; }

            public double? Community_Rating { get; set; }
        }

        public partial class AddedByStatus
        {
            public double? Yet { get; set; }

            public double Owned { get; set; }

            public double? Beaten { get; set; }

            public double? Toplay { get; set; }

            public double? Dropped { get; set; }

            public double? Playing { get; set; }
        }

        public partial class Clip
        {
            [JsonPropertyName("clip")]
            public Uri ClipClip { get; set; }

            public Clips Clips { get; set; }

            public string Video { get; set; }

            public Uri Preview { get; set; }
        }

        public partial class Clips
        {
            [JsonPropertyName("320")]
            public Uri The320 { get; set; }

            [JsonPropertyName("640")]
            public Uri The640 { get; set; }

            public Uri Full { get; set; }
        }

        public partial class Genre
        {
            public double Id { get; set; }

            public string Name { get; set; }

            public string Slug { get; set; }
        }

        public partial class Platform
        {
            [JsonPropertyName("platform")]
            public Genre PlatformPlatform { get; set; }
        }

        public partial class Rating
        {
            public double Id { get; set; }

            public string Title { get; set; }

            public double Count { get; set; }

            public double Percent { get; set; }
        }

        public partial class ShortScreenshot
        {
            public double Id { get; set; }

            public Uri Image { get; set; }
        }

        public partial class Store
        {
            [JsonPropertyName("store")]
            public Genre StoreStore { get; set; }
        }

        public partial class Tag
        {
            public double Id { get; set; }

            public string Name { get; set; }

            public string Slug { get; set; }

            public string Language { get; set; }

            public double Games_Count { get; set; }

            public Uri Image_Background { get; set; }
        }
    }
}
