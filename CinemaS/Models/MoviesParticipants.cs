using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Movies_Participants", Schema = "dbo")]
    public class MoviesParticipants
    {
    [Column("Movie_Participant_ID")]
    [StringLength(10)]
    public string MovieParticipantId { get; set; }
    [Column("Movie_Role_ID")]
    [StringLength(10)]
    public string MovieRoleId { get; set; }
    [Column("Participants_ID")]
    [StringLength(10)]
    public string ParticipantsId { get; set; }
    [Column("Movies_ID")]
    [StringLength(10)]
    public string MoviesId { get; set; }
    // Composite Key (Movie_Participant_ID, Participants_ID, Movies_ID) will be configured in DbContext

    }
}
