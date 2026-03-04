using System.ComponentModel.DataAnnotations.Schema;

namespace YP.Models
{
    [Table("comments")]
    public class Comment
    {
        public int CommentID { get; set; }
        public required string Message { get; set; }
        public required int MasterID { get; set; }
        public required int RequestID { get; set; }

        [ForeignKey("MasterID")]
        public virtual User? Master { get; set; }

        [ForeignKey("RequestID")]
        public virtual Request? Request { get; set; }
    }
}
