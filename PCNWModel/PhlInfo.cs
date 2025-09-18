using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PCNW.Models
{
    [Table("PhlInfo")]

    public class PhlInfo
    {
        [Key]
        public int Id { get; set; }
        public int? MemId { get; set; }
        public int? ConId { get; set; }
        public string? Note { get; set; }
        public DateTime? BidDate { get; set; }
        public string? tComp { get; set; }
        public string? hComp { get; set; }
        public string? mValue { get; set; }
        public int? BidStatus { get; set; }
        public bool? IsActive { get; set; }
        public int CompType { get; set; }
        public int? PhlType { get; set; }
        public string? PST { get; set; }
        public int? ProjId { get; set; }
    }
}
