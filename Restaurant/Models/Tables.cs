using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restaurant.Models;

[Table("restaurant_tables")]
public class Tables
{
    [Key]
    [Column("table_id")]
    public int TableId { get; set; }

    [Required]
    [Column("max_capacity")]
    public int MaxCapacity { get; set; }

    [Required]
    [Column("served_by_id")]
    public int ServedById { get; set; }

    // Navigation Property
    [ForeignKey("ServedById")]
    public Staff ServedBy { get; set; }
    
    public ICollection<Receipt> Receipts { get; set; }

}
