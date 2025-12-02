using System;

namespace Summary.Domain.Interfaces
{
    public interface ITestSession<T, K> where T : notnull
    {
        T UserId { get; }

        K TenantId { get; }
    }

    public class TestSession : ITestSession<Guid, string>
    {
        public Guid UserId => new("0e5e72d8-2a29-4c15-a0c3-b4de9a13ac8c");

        public string TenantId { get; set; } = "TestSession";
    }
}