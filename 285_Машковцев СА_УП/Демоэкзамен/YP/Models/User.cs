using System.ComponentModel.DataAnnotations.Schema;

namespace YP.Models
{
    [Table("users")]
    public class User
    {
        public int UserID { get; set; }
        public required string Fio { get; set; }
        public required string Phone { get; set; }
        public required string Login { get; set; }
        public required string Password { get; set; }
        public required int RoleID { get; set; }

        public virtual Role? Role { get; set; }
    }
}
