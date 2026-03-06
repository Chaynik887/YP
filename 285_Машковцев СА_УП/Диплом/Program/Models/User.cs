namespace Program.Models;

public class User
{
    public int UserId { get; set; }
    public string Login { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    
    public Role? Role { get; set; }
}




