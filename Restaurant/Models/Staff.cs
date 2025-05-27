using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restaurant.Models
{
    [Table("staff")]
    public class Staff
    {
        [Key]
        [Column("staff_id")]
        public int StaffId { get; set; }

        [Required]
        [Column("name")]
        [StringLength(255)]
        public string Name { get; set; }

        [Column("surname")]
        [StringLength(255)]
        public string Surname { get; set; }

        [Column("job")]
        [StringLength(25)]
        public string Job { get; set; }

        [Column("tel_no")]
        [StringLength(25)]
        public string TelNo { get; set; }
        
        public ICollection<Receipt> ReceiptsServed { get; set; }

    }
}