namespace Program.Models;

public class Product
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; } = 0; // закупочная цена
    public int StockQuantity { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}




