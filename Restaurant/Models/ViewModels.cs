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

        [Required]
        [Column("surname")]
        [StringLength(255)]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [Column("tel_no")]
        [StringLength(25)]
        public string TelNo { get; set; } = string.Empty;

        [Column("email")]
        [StringLength(255)]
        public string? Email { get; set; }
    }

    [Table("reservation_details")]
    public class ReservationDetail
    {
        [Key]
        [Column("res_details_id")]
        public int ResDetailsId { get; set; }

        [Required]
        [Column("guest_number")]
        public int GuestNumber { get; set; }

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

        [Column("special_requests")]
        public string? SpecialRequests { get; set; }
    }
    
    [Table("reservations")]
    public class Reservation
    {
        [Key]
        [Column("reservation_id")]
        public int ReservationId { get; set; }
    
        [Required]
        [Column("res_details_id")]
        public int ResDetailsId { get; set; }

        [Required]
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Required]
        [Column("table_id")]
        public int TableId { get; set; }

        // Navigation properties
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
    
        [ForeignKey("TableId")]
        public virtual RestaurantTable? Table { get; set; }

        [ForeignKey("ResDetailsId")]
        public virtual ReservationDetail? ReservationDetail { get; set; }
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

        [Required]
        [Column("surname")]
        [StringLength(255)]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [Column("job")]
        [StringLength(25)]
        public string Job { get; set; } = string.Empty;

        [Required]
        [Column("tel_no")]
        [StringLength(25)]
        public string TelNo { get; set; } = string.Empty;
    }

    [Table("user_credentials")]
    public class UserCredential
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("username")]
        [StringLength(16)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("password")]
        [StringLength(16)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Column("user_type")]
        [StringLength(10)]
        public string UserType { get; set; } = string.Empty;
    }

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

        [Required]
        [Column("staff_id")]
        public int StaffId { get; set; }

        [Required]
        [Column("total_amount")]
        [StringLength(20)]
        public string TotalAmount { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("ReservationId")]
        public virtual Reservation? Reservation { get; set; }

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
        [StringLength(20)]
        public string UnitPrice { get; set; } = string.Empty;

        [Column("special_notes")]
        public string? SpecialNotes { get; set; }

        // Navigation properties
        [ForeignKey("ReceiptId")]
        public virtual Receipt? Receipt { get; set; }

        [ForeignKey("ItemId")]
        public virtual MenuItem? MenuItem { get; set; }
    }

    // View Models for the reservation system
    public class ReservationCreateViewModel
    {
        public int TableId { get; set; }
        public int CustomerId { get; set; }
        
        [Required]
        public DateTime ReservationDate { get; set; }
        
        [Required]
        public string ReservationHour { get; set; } = string.Empty;
        
        [Required]
        [Range(1, 10, ErrorMessage = "Number of guests must be between 1 and 10")]
        public int GuestNumber { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(255, ErrorMessage = "First name cannot exceed 255 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(255, ErrorMessage = "Last name cannot exceed 255 characters")]
        public string Surname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(25, ErrorMessage = "Phone number cannot exceed 25 characters")]
        public string TelNo { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
        public string? Email { get; set; }

        public string? SpecialRequests { get; set; }

        public virtual RestaurantTable? Table { get; set; }
    }

    public class AdminPanelView
    {
        public List<Staff> StaffMembers { get; set; } = new List<Staff>();
        public List<Reservation> UpcomingReservations { get; set; } = new List<Reservation>();
    
        // Properties that match HomeController expectations
        public int TotalStaff { get; set; }
        public int TotalWaiters { get; set; }
        public int ActiveReservations { get; set; } 
    }

    public class BookingViewModel
    {
        public List<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();  // Changed from AvailableTables to Tables to match controller
        public int? SelectedTableId { get; set; }
        public string? SelectedDate { get; set; }
        public string? SelectedTime { get; set; }
        public int? SelectedGuests { get; set; }
        
        // Keep the original property as well for compatibility
        public List<RestaurantTable> AvailableTables { get; set; } = new List<RestaurantTable>();
        public Dictionary<string, List<int>> OccupiedTablesByHour { get; set; } = new Dictionary<string, List<int>>();
    }

    public class OrderManagementViewModel
    {
        public Reservation Reservation { get; set; } = new Reservation();
        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
        public Receipt? ExistingReceipt { get; set; }
    }

    // Order submission models that match HomeController expectations
    public class OrderSubmissionModel
    {
        public int ReservationId { get; set; }
        public int StaffId { get; set; }
        public string TotalAmount { get; set; } = string.Empty;
        public List<OrderItemModel> Items { get; set; } = new List<OrderItemModel>();
        public string? SpecialNotes { get; set; }
    }

    public class OrderItemModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int Calories { get; set; }
    }

    // Additional DTOs and ViewModels
    public class LoginDto
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }

    public class TableAvailabilityDto
    {
        public int TableId { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
    }

    public class UpdateReservationStatusRequest
    {
        public int ReservationId { get; set; }
        public string Status { get; set; } = string.Empty;
    }
    
    // Legacy models for backward compatibility
    public class OrderRequest
    {
        public int ReservationId { get; set; }
        public int StaffId { get; set; }
        public string TotalAmount { get; set; } = string.Empty;
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
        public string? SpecialNotes { get; set; }
    }

    public class OrderItem
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int? Calories { get; set; }
    }
}