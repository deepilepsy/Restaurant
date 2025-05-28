using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restaurant.Models
{
    // Database Models (matching your SQL schema exactly)
    
    [Table("reservations")]
    public class Reservation
    {
        [Key]
        [Column("reservation_id")]
        public int ReservationId { get; set; }

        [Required]
        [Column("name")]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("surname")]
        [StringLength(255)]
        public string? Surname { get; set; }

        [Required]
        [Column("tel_no")]
        [StringLength(25)]
        public string TelNo { get; set; } = string.Empty;

        [Column("email")]
        [StringLength(255)]
        public string? Email { get; set; }
        
        [Column("special_requests")]
        public string? SpecialRequests { get; set; }

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
        [StringLength(10)]
        public string ReservationHour { get; set; } = string.Empty;

        [Required]
        [Column("reservation_status")]
        [StringLength(10)]
        public string ReservationStatus { get; set; } = "active";

        // Navigation properties
        [ForeignKey("TableId")]
        public virtual RestaurantTable? Table { get; set; }

        [ForeignKey("ServedById")]
        public virtual Staff? ServedBy { get; set; }
    }

    [Table("restaurant_tables")]
    public class RestaurantTable
    {
        [Key]
        [Column("table_id")]
        public int TableId { get; set; }

        [Required]
        [Column("min_capacity")]
        public int MinCapacity { get; set; }

        [Required]
        [Column("max_capacity")]
        public int MaxCapacity { get; set; }

        [Required]
        [Column("served_by_id")]
        public int ServedById { get; set; }

        // Navigation properties
        [ForeignKey("ServedById")]
        public virtual Staff? ServedBy { get; set; }
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }

    [Table("staff")]
    public class Staff
    {
        [Key]
        [Column("staff_id")]
        public int StaffId { get; set; }

        [Required]
        [Column("name")]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [Column("surname")]
        [StringLength(255)]
        public string? Surname { get; set; }

        [Column("job")]
        [StringLength(25)]
        public string? Job { get; set; }

        [Column("tel_no")]
        [StringLength(25)]
        public string? TelNo { get; set; }

        // Navigation properties
        public virtual ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }

    [Table("credentials")]
    public class Credentials
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        [StringLength(16)]
        public string? Username { get; set; }

        [Column("password")]
        [StringLength(16)]
        public string? Password { get; set; }
    }

    [Table("staff_credentials")]
    public class StaffCredentials
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        [StringLength(16)]
        public string? Username { get; set; }

        [Column("password")]
        [StringLength(16)]
        public string? Password { get; set; }
    }

    // View Models for the reservation system

    public class ReservationCreateViewModel
    {
        // Table and reservation details (pre-populated from booking page)
        public int TableId { get; set; }
        
        [Required]
        public DateTime ReservationDate { get; set; }
        
        [Required]
        public string ReservationHour { get; set; } = string.Empty;
        
        [Required]
        [Range(1, 10, ErrorMessage = "Number of guests must be between 1 and 10")]
        public int GuestNumber { get; set; }

        // Customer information (to be filled in the form)
        [Required(ErrorMessage = "First name is required")]
        [StringLength(255, ErrorMessage = "First name cannot exceed 255 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Last name cannot exceed 255 characters")]
        public string? Surname { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(25, ErrorMessage = "Phone number cannot exceed 25 characters")]
        public string TelNo { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string? Email { get; set; }

        public string? SpecialRequests { get; set; }

        // Navigation property for table information
        public virtual RestaurantTable? Table { get; set; }
    }

    public class AdminPanelView
    {
        public List<Staff> StaffMembers { get; set; } = new List<Staff>();
        public List<Reservation> UpcomingReceipts { get; set; } = new List<Reservation>();
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }

    // For table availability checking
    public class TableAvailabilityDto
    {
        public int TableId { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    // For booking page
    public class BookingViewModel
    {
        public List<RestaurantTable> AvailableTables { get; set; } = new List<RestaurantTable>();
        public Dictionary<string, List<int>> OccupiedTablesByHour { get; set; } = new Dictionary<string, List<int>>();
    }

    // DTO for updating reservation status
    public class UpdateReservationStatusRequest
    {
        public int ReservationId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}