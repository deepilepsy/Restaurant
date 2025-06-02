using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Restaurant.Models
{
    [Table("customers")]
    public class Customer
    {
        [Key]
        [Column("customer_id")]
        public int CustomerId { get; set; }
        
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

        // Navigation properties - Remove these to avoid EF confusion
        // public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        // public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    }
    
    [Table("reservations")]
    public class Reservation
    {
        [Key]
        [Column("reservation_id")]
        public int ReservationId { get; set; }
    
        [Required]
        [Column("customer_id")]
        public int CustomerId { get; set; }
    
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

        // Navigation properties with explicit foreign key specification
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
    
        [ForeignKey("TableId")]
        public virtual RestaurantTable? Table { get; set; }

        [ForeignKey("ServedById")]
        public virtual Staff? ServedBy { get; set; }

        // Remove this to avoid circular reference issues
        // public virtual Receipt? Receipt { get; set; }
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
        
        // Remove collections to avoid EF confusion
        // public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        // public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
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

        // Remove navigation collections to avoid EF confusion
        // public virtual ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
        // public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        // public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
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

    // New models for order management
    [Table("menu_items")]
    public class MenuItem
    {
        [Key]
        [Column("item_id")]
        public int ItemId { get; set; }

        [Required]
        [Column("item_name")]
        [StringLength(255)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [Column("category")]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [Column("subcategory")]
        [StringLength(50)]
        public string? Subcategory { get; set; }

        [Required]
        [Column("price")]
        [StringLength(20)]
        public string Price { get; set; } = string.Empty;

        [Column("calories")]
        public int? Calories { get; set; }

        // Remove navigation collection to avoid EF confusion
        // public virtual ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();
    }

    [Table("receipts")]
    public class Receipt
    {
        [Key]
        [Column("receipt_id")]
        public int ReceiptId { get; set; }

        [Required]
        [Column("reservation_id")]
        public int ReservationId { get; set; }

        [Column("table_id")]
        public int? TableId { get; set; }

        [Column("customer_id")]
        public int? CustomerId { get; set; }

        [Required]
        [Column("staff_id")]
        public int StaffId { get; set; }

        [Required]
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties with explicit foreign key specification
        [ForeignKey("ReservationId")]
        public virtual Reservation? Reservation { get; set; }

        [ForeignKey("TableId")]
        public virtual RestaurantTable? Table { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [ForeignKey("StaffId")]
        public virtual Staff? Staff { get; set; }

        public virtual ICollection<ReceiptItem> ReceiptItems { get; set; } = new List<ReceiptItem>();
    }

    [Table("receipt_items")]
    public class ReceiptItem
    {
        [Key]
        [Column("receipt_item_id")]
        public int ReceiptItemId { get; set; }

        [Required]
        [Column("receipt_id")]
        public int ReceiptId { get; set; }

        [Required]
        [Column("item_id")]
        public int ItemId { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("unit_price")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column("total_price")]
        public decimal TotalPrice { get; set; }

        [Column("special_notes")]
        public string? SpecialNotes { get; set; }

        // Navigation properties with explicit foreign key specification
        [ForeignKey("ReceiptId")]
        public virtual Receipt? Receipt { get; set; }

        [ForeignKey("ItemId")]
        public virtual MenuItem? MenuItem { get; set; }
    }

    // View Models for the reservation system
    public class ReservationCreateViewModel
    {
        // Table and reservation details (pre-populated from booking page)
        public int TableId { get; set; }
        public int CustomerId { get; set; }
        
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
    
    // ViewModels for Order Management
    public class OrderManagementViewModel
    {
        public Reservation Reservation { get; set; } = new Reservation();
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
        public Receipt? ExistingReceipt { get; set; } // For handling existing receipts
    }

    public class OrderRequest
    {
        public int ReservationId { get; set; }
        public int TableId { get; set; }
        public int? CustomerId { get; set; }
        public int StaffId { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public string? SpecialNotes { get; set; }
    }

    public class OrderItem
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public int? Calories { get; set; }
    }
}