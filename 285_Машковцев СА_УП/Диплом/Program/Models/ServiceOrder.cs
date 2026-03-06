namespace Program.Models;

public class ServiceOrder
{
    public int OrderId { get; set; }
    public int MasterId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Now;
    public decimal? TotalAmount { get; set; }
    public string? Notes { get; set; }
    
    public User? Master { get; set; }
    public ICollection<ServiceOrderItem> ServiceOrderItems { get; set; } = new List<ServiceOrderItem>();
}

