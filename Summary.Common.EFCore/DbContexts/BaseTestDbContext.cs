using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json.Linq;
using Summary.Common.EFCore.Diagnostic;
using Summary.Common.Extensions;
using Summary.Domain.Interfaces;
using System.Linq.Expressions;
using System.Reflection;

namespace Summary.Common.EFCore.DbContexts
{
    public abstract class BaseTestDbContext<TUserId, KTenantId> : DbContext where TUserId : notnull
    {
        public ITestSession<TUserId, KTenantId> TestSession { get; }

        //private static readonly ConcurrentDictionary<Type, Action<object, TUserId>?> UserIdSetters = new();
        //private static readonly ConcurrentDictionary<Type, Action<object, DateTime>?> DateTimeSetters = new();
        private static readonly MethodInfo ConfigureGlobalFiltersMethodInfo =
            typeof(BaseTestDbContext<TUserId, KTenantId>).GetMethod(nameof(ConfigureGlobalFilters), BindingFlags.Instance | BindingFlags.NonPublic)!;

        protected BaseTestDbContext(DbContextOptions dbContextOptions, ITestSession<TUserId, KTenantId> session)
            : base(dbContextOptions)
        {
            TestSession = session;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                ConfigureGlobalFiltersMethodInfo
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [modelBuilder, entityType]);

                if (entityType.ClrType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICreateEntity<,>)))
                {
                    modelBuilder.Entity(entityType.ClrType)
                            .Property(nameof(ICreateEntity<object, object>.CreationUserId))
                            .IsRequired();
                }
            }
        }

        #region filter

        protected void ConfigureGlobalFilters<TEntity>(ModelBuilder modelBuilder, IMutableEntityType mutableEntityType)
        where TEntity : class
        {
            if (mutableEntityType.BaseType == null && ShouldFilterEntity<TEntity>())
            {
                var filter = CreateQueryFilterExpression(typeof(TEntity));
                if (filter != null)
                {
                    modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
                }
            }
        }

        protected virtual bool ShouldFilterEntity<TEntity>()
        {
            if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity))) return true;
            if (typeof(IHaveTenant<KTenantId>).IsAssignableFrom(typeof(TEntity))) return true;
            return false;
        }

        protected virtual LambdaExpression? CreateQueryFilterExpression(Type entityType)
        {
            Expression? finalExpression = null;
            var parameter = Expression.Parameter(entityType, "e");

            if (typeof(ISoftDelete).IsAssignableFrom(entityType))
            {
                var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                finalExpression = Expression.Equal(isDeletedProperty, Expression.Constant(false));
            }

            if (typeof(IHaveTenant<>).MakeGenericType(typeof(KTenantId)).IsAssignableFrom(entityType))
            {
                var sessionProperty = Expression.Property(Expression.Constant(this), nameof(this.TestSession));
                var sessionTenantId = Expression.Property(sessionProperty, nameof(this.TestSession.TenantId));
                var entityTenantIdProperty = Expression.Property(parameter, nameof(IHaveTenant<KTenantId>.TenantId));
                var tenantFilter = Expression.Equal(entityTenantIdProperty, Expression.Convert(sessionTenantId, entityTenantIdProperty.Type));

                finalExpression = finalExpression == null ? tenantFilter : Expression.AndAlso(finalExpression, tenantFilter);
            }

            return finalExpression != null ? Expression.Lambda(finalExpression, parameter) : null;
        }

        #endregion filter

        #region Create ChangeInfo

        protected virtual void AddChangeInfos(EntityEntry entityEntry)
        {
            switch (entityEntry.State)
            {
                case EntityState.Added:
                case EntityState.Modified:
                    CreateChangeInfo(entityEntry);
                    break;
                case EntityState.Deleted:
                    CreateChangeInfo(entityEntry);
                    break;
            }
        }

        protected virtual EFCoreEntityChangeInfo? CreateChangeInfo(EntityEntry entityEntry)
        {
            EFCoreEntityChangeInfo ChangeInfo = new()
            {
                TableName = entityEntry.Metadata.GetDefaultTableName()
                         ?? entityEntry.Entity.GetType().Name,
                EntityState = GetDiagnosticEntityState(entityEntry.State),
                OriginalValues =
                    entityEntry.State == EntityState.Added
                    ? null
                    : GetOriginalEntryValue(entityEntry),
                CurrentValues =
                    entityEntry.State == EntityState.Deleted
                    ? null
                    : GetCurrentEntryValue(entityEntry),

                EntityEntry = entityEntry,
            };
            return ChangeInfo;
        }

        protected virtual void HandleChangeInfos(IList<EFCoreEntityChangeInfo> changeInfos) { }

        protected virtual Task HandleChangeInfosAsync(IList<EFCoreEntityChangeInfo> changeInfos, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private static JObject GetOriginalEntryValue(EntityEntry entityEntry)
        {
            JObject value = [];
            foreach (var property in entityEntry.Properties)
            {
                value[property.Metadata.Name] = null == property.OriginalValue
                    ? JValue.CreateNull()
                    : JToken.FromObject(property.OriginalValue);
            }
            return value;
        }

        private static JObject GetCurrentEntryValue(EntityEntry entityEntry)
        {
            JObject value = [];
            foreach (var property in entityEntry.Properties)
            {
                value[property.Metadata.Name] = null == property.CurrentValue
                    ? JValue.CreateNull()
                    : JToken.FromObject(property.CurrentValue);
            }
            return value;
        }

        #endregion Create ChangeInfo

        protected virtual bool CheckOwnedEntityChange(EntityEntry entry)
        {
            return entry.State == EntityState.Modified ||
                   entry.References.Any(r =>
                       r.TargetEntry != null &&
                       r.TargetEntry.Metadata.IsOwned() &&
                       CheckOwnedEntityChange(r.TargetEntry));
        }

        #region SaveChanges Overrides

        public override int SaveChanges()
        {
            try
            {
                ProcessTrackedEntries();
                return base.SaveChanges();
            }
            finally
            {
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                ProcessTrackedEntries();
                return await base.SaveChangesAsync(cancellationToken);
            }
            finally
            {
            }
        }

        #endregion SaveChanges Overrides

        #region Entry Processing Logic

        private List<EFCoreEntityChangeInfo> ProcessTrackedEntries()
        {
            var changeInfos = new List<EFCoreEntityChangeInfo>(); // 局部变量
            var now = DateTime.UtcNow;

            SetTenantIdOnNewEntities();

            var entries = ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Detached && e.State != EntityState.Unchanged)
                .ToList();

            foreach (var entry in entries)
            {
                if (entry.State != EntityState.Modified && CheckOwnedEntityChange(entry))
                {
                    entry.State = EntityState.Modified;
                }

                ConceptEntry(entry, now);

                // 获取变更信息并添加到列表
                var info = CreateChangeInfo(entry);
                if (info != null)
                {
                    changeInfos.Add(info);
                }
            }

            return changeInfos;
        }

        protected virtual void ConceptEntry(EntityEntry entityEntry, DateTime now)
        {
            switch (entityEntry.State)
            {
                case EntityState.Added:
                    ConceptsForAddEntity(entityEntry, now);
                    break;

                case EntityState.Modified:
                    ConceptsForModifyEntity(entityEntry, now);
                    break;

                case EntityState.Deleted:
                    ConceptsForDeleteEntity(entityEntry, now);
                    break;
            }
        }

        #endregion Entry Processing Logic

        #region Default Property Setters

        private void SetUserIdProperty(object entity, string propertyName, bool onlySetIfDefault = false)
        {
            var propertyInfo = entity.GetType().GetProperty(propertyName);
            if (propertyInfo == null || TestSession == null) return;

            if (onlySetIfDefault)
            {
                var currentValue = propertyInfo.GetValue(entity);
                var propertyType = propertyInfo.PropertyType;

                object? defaultValue = null;
                if (propertyType.IsValueType)
                {
                    defaultValue = Activator.CreateInstance(propertyType);
                }

                if (!object.Equals(currentValue, defaultValue))
                {
                    return;
                }
            }

            if (propertyInfo.PropertyType.IsAssignableFrom(typeof(TUserId)))
            {
                propertyInfo.SetValue(entity, TestSession.UserId);
            }
            else
            {
                try
                {
                    var targetType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                    var convertedValue = Convert.ChangeType(TestSession.UserId, targetType);
                    propertyInfo.SetValue(entity, convertedValue);
                }
                catch (Exception) { }
            }
        }

        private void SetDateTimeProperty(object entity, string propertyName, DateTime value, bool onlySetIfDefault = false)
        {
            var propertyInfo = entity.GetType().GetProperty(propertyName);
            if (propertyInfo == null || !(propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))) return;

            if (onlySetIfDefault)
            {
                var currentValue = propertyInfo.GetValue(entity);
                if (currentValue != null && ((DateTime)currentValue) != default) return;
            }
            propertyInfo.SetValue(entity, value);
        }

        protected virtual void ConceptsForDeleteEntity(EntityEntry entityEntry, DateTime now)
        {
            if (entityEntry.Entity is ISoftDelete softDeleteEntity)
            {
                entityEntry.State = EntityState.Modified;
                softDeleteEntity.IsDeleted = true;
            }

            if (entityEntry.Entity.GetType().IsAssignableToGenericType(typeof(IDeleteEntity<>)))
            {
                SetUserIdProperty(entityEntry.Entity, "DeleteUserId");
                SetDateTimeProperty(entityEntry.Entity, "DeleteTime", now);
            }
        }

        protected virtual void ConceptsForModifyEntity(EntityEntry entityEntry, DateTime now)
        {
            var entityType = entityEntry.Entity.GetType();
            if (entityType.IsAssignableToGenericType(typeof(IModifyEntity<,>)) ||
                entityType.IsAssignableToGenericType(typeof(IModifyEntity<,,>)))
            {
                SetUserIdProperty(entityEntry.Entity, "ModifyEntityId");
                SetDateTimeProperty(entityEntry.Entity, "ModifiedTime", now);
            }
        }

        protected virtual void ConceptsForAddEntity(EntityEntry entityEntry, DateTime now)
        {
            CheckAndSetId(entityEntry);
            var entityType = entityEntry.Entity.GetType();

            if (entityType.IsAssignableToGenericType(typeof(ICreateEntity<,>)))
            {
                SetUserIdProperty(entityEntry.Entity, "CreationUserId", onlySetIfDefault: true);
                SetDateTimeProperty(entityEntry.Entity, "CreationTime", now, onlySetIfDefault: true);
            }
        }

        private void SetTenantIdOnNewEntities()
        {
            foreach (var entry in ChangeTracker.Entries<IHaveTenant<KTenantId>>().Where(e => e.State == EntityState.Added))
            {
                entry.Entity.TenantId = TestSession.TenantId;
            }
        }

        protected virtual void CheckAndSetId(EntityEntry entry)
        {
            if (entry.Entity is IEntity<Guid> entity && entity.Id == Guid.Empty)
            {
                var idPropertyEntry = entry.Property("Id");
                if (idPropertyEntry != null && idPropertyEntry.Metadata.ValueGenerated == ValueGenerated.Never)
                {
                    entity.Id = Guid.NewGuid();
                }
            }
        }

        #endregion Default Property Setters

        public DiagnosticEntityState GetDiagnosticEntityState(EntityState entityState)
        {
            switch (entityState)
            {
                case EntityState.Added:
                    return DiagnosticEntityState.Added;

                case EntityState.Modified:
                    return DiagnosticEntityState.Modified;

                case EntityState.Deleted:
                    return DiagnosticEntityState.Deleted;

                default:
                    return DiagnosticEntityState.Deleted;
                    //default:
                    //throw new InvalidOperationException($"Unsupported EntityState: {entityState}");
            }
        }
    }
}