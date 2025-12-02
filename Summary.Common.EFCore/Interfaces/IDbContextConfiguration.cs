namespace Summary.Common.EFCore.Interfaces
{
    public interface IDbContextConfiguration
    {
        string ConnectionString { get; set; }

        int DbType { get; set; }

        /// <summary>
        /// dbcontext唯一
        /// </summary>
        string DbContextFullName { get; set; }
    }

    public interface IMultipleDbContextConfiguration
    {
        public IEnumerable<IDbContextConfiguration> TestDbContextConfigurations { get; }
    }
}