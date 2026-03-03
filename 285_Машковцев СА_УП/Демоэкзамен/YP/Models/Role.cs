using System.ComponentModel.DataAnnotations.Schema;

namespace YP.Models
{
    [Table("roles")]
    public class Role
    {
        public int RoleID { get; set; }
        public required string RoleName { get; set; }
    }
}
