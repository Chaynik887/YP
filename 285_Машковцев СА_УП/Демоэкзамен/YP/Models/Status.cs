using System.ComponentModel.DataAnnotations.Schema;

namespace YP.Models
{
    [Table("statuses")]
    public class Status
    {
        public int StatusID { get; set; }
        public required string StatusName { get; set; }
    }
}
