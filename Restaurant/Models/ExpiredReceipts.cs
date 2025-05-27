using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Restaurant.Models
{
    [Table("expired_receipt")]
    public class ExpiredReceipts
    {
        [Key]
        [Column("receipt_id")]
        public int ReceiptId { get; set; }

        [Required]
        [Column("name")]
        [StringLength(255)]
        public string Name { get; set; }

        [Column("surname")]
        [StringLength(255)]
        public string Surname { get; set; }

        [Required]
        [Column("tel_no")]
        [StringLength(25)]
        public string TelNo { get; set; }

        [Required]
        [Column("guest_number")]
        public int GuestNumber { get; set; }

        [Required]
        [Column("served_by_id")]
        public int ServedById { get; set; }

        [Required]
        [Column("table_id")]
        public int TableId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [Column("reservation_date")]
        public DateTime ReservationDate { get; set; }

        [Required]
        [Column("reservation_hour")]
        public TimeSpan ReservationHour { get; set; }

        // Navigation properties
        [ForeignKey("ServedById")]
        public Staff ServedBy { get; set; }

        [ForeignKey("TableId")]
        public Tables Table { get; set; }
    }
}