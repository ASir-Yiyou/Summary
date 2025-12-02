using Newtonsoft.Json.Linq;

namespace Summary.Common.EFCore.Diagnostic
{
    public class BaseEntityChangeInfo
    {
        public string? TableName { get; set; }

        public DiagnosticEntityState EntityState { get; set; }

        //[Column(TypeName = "jsonb")]
        public JObject? OriginalValues { get; set; }

        //[Column(TypeName = "jsonb")]
        public JObject? CurrentValues { get; set; }
    }
}