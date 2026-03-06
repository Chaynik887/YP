namespace Program.Models;

public class Supply
{
    public int SupplyId { get; set; }
    public string? SupplierName { get; set; }
    public int ReceivedByUserId { get; set; }
    public DateTime SupplyDate { get; set; } = DateTime.Now;
    public decimal TotalCost { get; set; } = 0;
    public string? Notes { get; set; }
    
    public User? ReceivedByUser { get; set; }
    public ICollection<SupplyItem> SupplyItems { get; set; } = new List<SupplyItem>();
}

