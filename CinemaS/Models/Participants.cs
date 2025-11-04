using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Participants", Schema = "dbo")]
    public class Participants
    {
    [Key]
    [Column("Participants_ID")]
    [StringLength(10)]
    public string ParticipantsId { get; set; }
    [MaxLength(100)]
    public string? BirthName { get; set; }
    [MaxLength(100)]
    public string? NickName { get; set; }
    public byte? Gender { get; set; }
    [MaxLength(100)]
    public string? Nationality { get; set; }
    [Column("Mini_Bio")]
    [MaxLength(500)]
    public string? MiniBio { get; set; }
    [MaxLength(200)]
    public string? Avatar { get; set; }

    }
}
