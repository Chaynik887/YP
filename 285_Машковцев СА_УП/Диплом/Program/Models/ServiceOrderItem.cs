namespace Program.Models;

public class ServiceOrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ServiceId { get; set; }
    public decimal PriceAtOrder { get; set; }
    
    public ServiceOrder? ServiceOrder { get; set; }
    public Service? Service { get; set; }
}

