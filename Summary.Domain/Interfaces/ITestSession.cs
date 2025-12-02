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

    public class AppTestSession : ITestSession<Guid, Guid>
    {
        public Guid UserId => new("0e5e72d8-2a29-4c15-a0c3-b4de9a13ac8c");
        public Guid TenantId => new("1c4d5f6a-7b8c-9d0e-1f2a-3b4c5d6e7f80");
    }
}