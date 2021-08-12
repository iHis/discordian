using System;
using Discord;

namespace DiscordIan.Helper
{
    public static class EmbedHelper
    {
        public static EmbedFieldBuilder MakeField(string name, string value, bool inLine = false)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
            {
                throw new Exception("Invalid value in embed field.");
            }

            return new EmbedFieldBuilder()
            {
                Name = name,
                Value = value,
                IsInline = inLine
            };
        }

        public static EmbedAuthorBuilder MakeAuthor(string name, string url = null, string icon = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("Invalid value in embed author.");
            }

            return new EmbedAuthorBuilder()
            {
                Name = name,
                Url = url,
                IconUrl = icon
            };
        }
    }
}
