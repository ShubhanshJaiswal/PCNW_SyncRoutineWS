using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PCNW.Models
{
    [Table("FileStorage")]

    public class TblFileStorage
    {
        [Key]
        public int Id { get; set; }
        public string? FileStorage { get; set; }
    }
}
