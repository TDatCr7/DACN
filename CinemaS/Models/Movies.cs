using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Movies", Schema = "dbo")]
    public class Movies
    {
    [Key]
    [Column("Movies_ID")]
    [StringLength(10)]
    public string MoviesId { get; set; }
    [Required]
    [Column("Status_ID")]
    [StringLength(10)]
    public string StatusId { get; set; }
    [MaxLength(100)]
    public string? Title { get; set; }
    [MaxLength(500)]
    public string? Summary { get; set; }
    [Column("Detail_Description")]
    [MaxLength(500)]
    public string? DetailDescription { get; set; }
    [Column("Release_Date")]
    public DateTime? ReleaseDate { get; set; }
    [MaxLength(100)]
    public string? Language { get; set; }
    public int? Duration { get; set; }
    public double? Rating { get; set; }
    public int? Age { get; set; }
    [Column("Trailer_Link")]
    [MaxLength(500)]
    public string? TrailerLink { get; set; }
    [Column("Poster_Image")]
    [MaxLength(200)]
    public string? PosterImage { get; set; }
    [Column("Banner_Image")]
    [MaxLength(200)]
    public string? BannerImage { get; set; }
    [Column("Created_At")]
    public DateTime? CreatedAt { get; set; }
    [Column("Updated_At")]
    public DateTime? UpdatedAt { get; set; }

    }
}
