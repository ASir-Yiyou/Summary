using System.ComponentModel;

namespace Summary.Common.EFCore.Diagnostic
{
    public enum DiagnosticEntityState
    {
        Default,

        [Description("新增")]
        Added,

        [Description("修改")]
        Modified,

        [Description("删除")]
        Deleted
    }
}