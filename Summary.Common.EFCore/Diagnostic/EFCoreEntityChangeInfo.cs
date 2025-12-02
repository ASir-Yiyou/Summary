using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Summary.Common.EFCore.Diagnostic
{
    public class EFCoreEntityChangeInfo : BaseEntityChangeInfo
    {
        public EntityEntry? EntityEntry { get; set; }
    }
}