using System.ComponentModel.DataAnnotations.Schema;

namespace PCNW.Models;

[Table("ProjectTraffic")]
public class ProjectTraffic
{
    public int ID { get; set; }
    public int ProjectId{ get; set; }
    public int AccessedBy { get; set; }
    public DateTime AccessedDate { get; set; } = DateTime.Now;


}
