namespace Message.Domain.MessageAggregate;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("messageTypeLookup")]
public partial class messageTypeLookup
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
    public messageTypeLookup()
    {
        Commons = new HashSet<CommonMessage>();
    }

    public int id { get; set; }

    [StringLength(50)]
    public string type { get; set; }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<CommonMessage> Commons { get; set; }
}
