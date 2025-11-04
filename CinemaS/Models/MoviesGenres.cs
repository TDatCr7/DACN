using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Movies_Genres", Schema = "dbo")]
    public class MoviesGenres
    {
    [Column("Movie_Genre_ID")]
    [StringLength(10)]
    public string MovieGenreId { get; set; }
    [Column("Movies_ID")]
    [StringLength(10)]
    public string MoviesId { get; set; }
    [Column("Genres_ID")]
    [StringLength(10)]
    public string GenresId { get; set; }
    // Composite Key (Movie_Genre_ID, Movies_ID, Genres_ID) will be configured in DbContext

    }
}
