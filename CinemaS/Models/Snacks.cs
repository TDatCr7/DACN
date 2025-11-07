using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaS.Models
{
    [Table("Snacks", Schema = "dbo")]
    public class Snacks
    {
        [Key]
        [Column("Snack_ID")]
        [StringLength(10)]
        public string SnackId { get; set; }
        [Required]
        [Column("Snack_Type_ID")]
        [StringLength(10)]
        public string SnackTypeId { get; set; }
        [MaxLength(100)]
        public string? Name { get; set; }
        [Column(TypeName = "money")]
        public decimal? Price { get; set; }
        [MaxLength(100)]
        public string? Image { get; set; }
        [MaxLength(300)]
        public string? Description { get; set; }
        [Column("IsActive")]
        public bool IsActive { get; set; } = true;


    }
}
