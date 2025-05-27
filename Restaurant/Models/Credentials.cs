using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restaurant.Models
{
    [Table("Credentials")]
    public class Credentials
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        
        [Column("username")]
        [Required]
        [StringLength(50)]
        public string Username { get; set; }
        
        [Column("password")]
        [Required]
        [StringLength(255)] // Longer to accommodate hashed passwords
        public string Password { get; set; }
    }
}