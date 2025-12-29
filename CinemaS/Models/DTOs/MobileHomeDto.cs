using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CinemaS.Models.DTOs
{
        public class MobileHomeDto
        {
            [JsonPropertyName("banners")] public List<BannerDto> Banners { get; set; } = new();
            [JsonPropertyName("nowShowing")] public List<MovieCardDto> NowShowing { get; set; } = new();
            [JsonPropertyName("comingSoon")] public List<MovieCardDto> ComingSoon { get; set; } = new();
        }

        public class BannerDto
        {
            [JsonPropertyName("type")] public string Type { get; set; } = "";
            [JsonPropertyName("movieId")] public string MovieId { get; set; } = "";
            [JsonPropertyName("title")] public string Title { get; set; } = "";
            [JsonPropertyName("imageUrl")] public string ImageUrl { get; set; } = "";
        }

        public class MovieCardDto
        {
            [JsonPropertyName("movieId")] public string MovieId { get; set; } = "";
            [JsonPropertyName("title")] public string Title { get; set; } = "";
            [JsonPropertyName("posterUrl")] public string PosterUrl { get; set; } = "";
            [JsonPropertyName("summary")] public string Summary { get; set; } = "";
            [JsonPropertyName("durationMin")] public int DurationMin { get; set; }
            [JsonPropertyName("releaseDate")] public string ReleaseDate { get; set; } = "";
            [JsonPropertyName("isNowShowing")] public bool IsNowShowing { get; set; }
            [JsonPropertyName("ageRating")] public string AgeRating { get; set; } = "";
            [JsonPropertyName("genres")] public string Genres { get; set; } = "";
        }
}

