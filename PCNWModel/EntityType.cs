using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SyncRoutineWS.PCNWModel;
[Table("EntityType")]

public partial class TblEntityType
{
    [Key]
    public int EntityID { get; set; }
    [StringLength(150)]
    public string? EntityType { get; set; }
    public int? SortOrder { get; set; }
    public Nullable<bool> IsActive { get; set; }

}