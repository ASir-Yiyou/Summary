namespace Summary.Common.EFCore.Interfaces
{
    public interface ITestDbContextConfiguration
    {
        string ConnectionString { get; set; }

        int DbType { get; set; }

        /// <summary>
        /// dbcontext唯一
        /// </summary>
        string DbContextFullName { get; set; }
    }

    public interface ITestMultipleDbContextConfiguration
    {
        public IEnumerable<ITestDbContextConfiguration> TestDbContextConfigurations { get; }
    }
}