using Summary.Common.EFCore.Interfaces;

namespace Summary.Common.EFCore.Configurations
{
    public class TestDbContextConfiguration : ITestDbContextConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int DbType { get; set; }
        public string DbContextFullName { get; set; } = string.Empty;
    }

    public class TestMultipleDbContextConfiguration : ITestMultipleDbContextConfiguration
    {
        public IEnumerable<ITestDbContextConfiguration> TestDbContextConfigurations { get; set; }
            = new List<TestDbContextConfiguration>();
    }
}