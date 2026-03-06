namespace Program.Models;

public class SupplyItem
{
    public int Id { get; set; }
    public int SupplyId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal CostPriceAtSupply { get; set; }
    
    public Supply? Supply { get; set; }
    public Product? Product { get; set; }
}

