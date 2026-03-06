namespace Program.Models;

public class Sale
{
    public int SaleId { get; set; }
    public int SellerId { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; } = 0;
    
    public User? Seller { get; set; }
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
}



