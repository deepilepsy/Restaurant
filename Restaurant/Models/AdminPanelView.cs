namespace Restaurant.Models
{
    public class AdminPanelView
    {
        public IEnumerable<Staff> StaffMembers { get; set; }
        public IEnumerable<Receipt> UpcomingReceipts { get; set; }
    }
}