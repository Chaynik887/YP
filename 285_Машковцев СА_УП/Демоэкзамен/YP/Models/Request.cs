using System.ComponentModel.DataAnnotations.Schema;

namespace YP.Models
{
    [Table("requests")]
    public class Request
    {
        public int RequestID { get; set; }
        public DateTime StartDate { get; set; }
        public required string ClimateTechType { get; set; }
        public required string ClimateTechModel { get; set; }
        public required string ProblemDescription { get; set; }
        public int StatusID { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string? RepairParts { get; set; }
        public int? MasterID { get; set; }
        public int ClientID { get; set; }

        public virtual Status? Status { get; set; }
        public virtual User? Client { get; set; }
        public virtual User? Master { get; set; }
    }
}
