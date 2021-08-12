using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace DiscordIan.Model.GameReview
{
    public class GameDetailModel
    {
        public partial class Detail
        {
            public double Id { get; set; }

            public string Slug { get; set; }

            public string Name { get; set; }

            public string Name_Original { get; set; }

            public string Description { get; set; }

            public double? Metacritic { get; set; }

            public MetacriticPlatform[] Metacritic_Platforms { get; set; }

            public DateTimeOffset Released { get; set; }

            public bool Tba { get; set; }

            public DateTimeOffset Updated { get; set; }

            public Uri Background_Image { get; set; }

            public Uri Background_Image_Additional { get; set; }

            public Uri Website { get; set; }

            public double Rating { get; set; }

            public double Rating_Top { get; set; }

            public Rating[] Ratings { get; set; }

            public Dictionary<string, double> Reactions { get; set; }

            public double Added { get; set; }

            public AddedByStatus Added_By_Status { get; set; }

            public double Playtime { get; set; }

            public double Screenshots_Count { get; set; }

            public double Movies_Count { get; set; }
            
            public double Creators_Count { get; set; }

            public double Achievements_Count { get; set; }

            public double Parent_Achievements_Count { get; set; }

            public Uri Reddit_Url { get; set; }

            public string Reddit_Name { get; set; }

            public string Reddit_Description { get; set; }

            public string Reddit_Logo { get; set; }

            public double Reddit_Count { get; set; }

            public double Twitch_Count { get; set; }

            public double Youtube_Count { get; set; }

            public double Reviews_Text_Count { get; set; }

            public double Ratings_Count { get; set; }

            public double Suggestions_Count { get; set; }

            public string[] Alternative_Names { get; set; }

            public Uri Metacritic_Url { get; set; }

            public double Parents_Count { get; set; }

            public double Additions_Count { get; set; }

            public double Game_Series_Count { get; set; }

            public object User_Game { get; set; }

            public double Reviews_Count { get; set; }

            public string Saturated_Color { get; set; }

            public string Dominant_Color { get; set; }

            public ParentPlatform[] Parent_Platforms { get; set; }

            public PlatformElement[] Platforms { get; set; }

            public Store[] Stores { get; set; }

            public Developer[] Developers { get; set; }

            public Developer[] Genres { get; set; }

            public Developer[] Tags { get; set; }

            public Developer[] Publishers { get; set; }

            public EsrbRating Esrb_Rating { get; set; }

            public Clip Clip { get; set; }

            public string Description_Raw { get; set; }
        }

        public partial class AddedByStatus
        {
            public double? Yet { get; set; }

            public double? Owned { get; set; }

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

        public partial class Developer
        {
            public double Id { get; set; }

            public string Name { get; set; }

            public string Slug { get; set; }

            public double Games_Count { get; set; }

            public Uri Image_Background { get; set; }

            public string Domain { get; set; }

            public string? Language { get; set; }
        }

        public partial class EsrbRating
        {
            public double Id { get; set; }

            public string Name { get; set; }

            public string Slug { get; set; }
        }

        public partial class MetacriticPlatform
        {
            public double Metascore { get; set; }

            public Uri Url { get; set; }

            public MetacriticPlatformPlatform Platform { get; set; }
        }

        public partial class MetacriticPlatformPlatform
        {
            public double Platform { get; set; }

            public string Name { get; set; }

            public string Slug { get; set; }
        }

        public partial class ParentPlatform
        {
            public EsrbRating Platform { get; set; }
        }

        public partial class PlatformElement
        {
            public PlatformPlatform Platform { get; set; }

            public DateTimeOffset Released_At { get; set; }

            public Requirements Requirements { get; set; }
        }

        public partial class PlatformPlatform
        {
            public double Id { get; set; }

            public string Name { get; set; }

            public string Slug { get; set; }

            public object Image { get; set; }

            public object Year_End { get; set; }

            public object Year_Start { get; set; }

            public double Games_Count { get; set; }

            public Uri Image_Background { get; set; }
        }

        public partial class Requirements
        {
            public string Minimum { get; set; }

            public string Recommended { get; set; }
        }

        public partial class Rating
        {
            public double Id { get; set; }

            public string Title { get; set; }

            public double Count { get; set; }

            public double Percent { get; set; }
        }

        public partial class Store
        {
            public double Id { get; set; }

            public Uri Url { get; set; }

            [JsonPropertyName("store")]
            public Developer StoreStore { get; set; }
        }
    }
}
