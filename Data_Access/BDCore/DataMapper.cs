using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace APPCORE.BDCore.Abstracts
{
    public class WDataMapper
    {
        public WDataMapper(GDatosAbstract GDatos, BDQueryBuilderAbstract QueryBuilder)
        {
            this.GDatos = GDatos;
            this.QueryBuilder = QueryBuilder;
        }

        public GDatosAbstract GDatos { get; set; }
        public BDQueryBuilderAbstract QueryBuilder { get; set; }

        #region ORM INSERT, DELETE, UPDATES METHODS (SÍNCRONOS - SIN CAMBIOS)

        public object? InsertObject(EntityClass entity, bool fullInsert = true)
        {
            List<PropertyInfo> entityProps = entity.GetType().GetProperties().ToList();
            List<PropertyInfo> primaryKeyProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
            List<PropertyInfo> manyToOneProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(ManyToOne)) != null).ToList();
            
            if (fullInsert)
            {
                SetManyToOneProperties(entity, manyToOneProperties);
            }

            (string? strQuery, List<IDbDataParameter>? parameters) = QueryBuilder.BuildInsertQueryByObjectParameters(entity);

            if (strQuery == null)
            {
                return null;
            }
            
            object? idGenerated = GDatos?.ExcuteSqlQuery(strQuery, entity.GetSqlConnection(), entity.GetTransaction(), parameters);

            if (primaryKeyProperties.Count == 1)
            {
                PrimaryKey? pkInfo = (PrimaryKey?)Attribute.GetCustomAttribute(primaryKeyProperties[0], typeof(PrimaryKey));
                if (pkInfo?.Identity == true)
                {
                    Type? pkType = Nullable.GetUnderlyingType(primaryKeyProperties[0].PropertyType);
                    primaryKeyProperties[0].SetValue(entity, Convert.ChangeType(idGenerated, pkType));
                }
            }
            
            if (!fullInsert)
            {
                return entity;
            }

            List<PropertyInfo> oneToOneProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(OneToOne)) != null).ToList();

            foreach (var oneToOneProp in oneToOneProperties)
            {
                string? attributeName = oneToOneProp.Name;
                var attributeValue = (EntityClass?)oneToOneProp.GetValue(entity);

                if (attributeValue != null)
                {
                    OneToOne? oneToOne = (OneToOne?)Attribute.GetCustomAttribute(oneToOneProp, typeof(OneToOne));
                    PropertyInfo? keyColumn = entity?.GetType().GetProperty(oneToOne?.KeyColumn);
                    PropertyInfo? foreignKeyColumn = attributeValue.GetType().GetProperty(oneToOne?.ForeignKeyColumn);

                    if (foreignKeyColumn != null)
                    {
                        var primaryKeyValue = entity?.GetType()?.GetProperty(keyColumn?.Name)?.GetValue(entity);
                        foreignKeyColumn.SetValue(attributeValue, primaryKeyValue);
                        attributeValue?.SetSqlConnection(entity.GetSqlConnection());
                        attributeValue?.SetTransaction(entity.GetTransaction());
                        InsertObject(attributeValue);
                    }
                }
            }

            List<PropertyInfo> oneToManyProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(OneToMany)) != null).ToList();

            foreach (var oneToManyProp in oneToManyProperties)
            {
                string? attributeName = oneToManyProp.Name;
                var attributeValue = oneToManyProp.GetValue(entity);

                if (attributeValue != null)
                {
                    OneToMany? oneToMany = (OneToMany?)Attribute.GetCustomAttribute(oneToManyProp, typeof(OneToMany));

                    foreach (var value in ((IEnumerable)attributeValue))
                    {
                        PropertyInfo? keyColumn = entity?.GetType().GetProperty(oneToMany?.KeyColumn);
                        PropertyInfo? foreignKeyColumn = value.GetType().GetProperty(oneToMany?.ForeignKeyColumn);

                        if (foreignKeyColumn != null)
                        {
                            EntityClass entityValue = (EntityClass)value;
                            entityValue?.SetSqlConnection(entity.GetSqlConnection());
                            entityValue?.SetTransaction(entity.GetTransaction());
                            var primaryKeyValue = entity?.GetType()?.GetProperty(keyColumn?.Name)?.GetValue(entity);
                            InsertRelationatedObject(primaryKeyValue, entityValue, foreignKeyColumn);
                        }
                    }
                }
            }

            return entity;
        }

        private void SetManyToOneProperties(EntityClass entity, List<PropertyInfo> manyToOneProps)
        {
            if (manyToOneProps == null) return;

            foreach (var manyToOneProp in manyToOneProps)
            {
                var attributeValue = (EntityClass)manyToOneProp.GetValue(entity);

                if (attributeValue != null)
                {
                    ManyToOne? manyToOne = (ManyToOne?)Attribute.GetCustomAttribute(manyToOneProp, typeof(ManyToOne));
                    if (manyToOne!.isView)
                    {
                        continue;
                    }

                    PropertyInfo? keyColumn = attributeValue.GetType().GetProperty(manyToOne?.KeyColumn);
                    PropertyInfo? foreignKeyColumn = entity.GetType().GetProperty(manyToOne?.ForeignKeyColumn);

                    if (keyColumn != null)
                    {
                        if (keyColumn?.GetValue(attributeValue) == null)
                        {
                            attributeValue?.SetSqlConnection(entity.GetSqlConnection());
                            attributeValue?.SetTransaction(entity.GetTransaction());
                            this.InsertObject(attributeValue);
                        }
                    }

                    if (keyColumn != null && foreignKeyColumn != null)
                    {
                        var foreignKey = entity.GetType().GetProperty(foreignKeyColumn.Name);
                        var keyValue = attributeValue?.GetType()?.GetProperty(keyColumn?.Name)?.GetValue(attributeValue);

                        if (keyValue != null)
                        {
                            foreignKey?.SetValue(entity, keyValue);
                        }
                    }
                }
            }
        }

        private void InsertRelationatedObject(object foreignKeyValue, EntityClass entity, PropertyInfo foreignKeyColumn, bool isUpdate = false)
        {
            foreignKeyColumn.SetValue(entity, foreignKeyValue);
            List<PropertyInfo> entityProps = entity.GetType().GetProperties().ToList();
            var primaryKeyProperties = entityProps.Where(p => (PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
            var primaryKeyPropertiesIdentitys = entityProps
                .Where(p => (PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null
                && ((PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)))?.Identity == true).ToList();
            var values = primaryKeyProperties.Where(p => p.GetValue(entity) != null).ToList();

            if (primaryKeyProperties.Count == values.Count && isUpdate)
            {
                UpdateObject(entity, primaryKeyProperties.Select(p => p.Name).ToArray());
            }
            else if (primaryKeyPropertiesIdentitys.Count == 1 || primaryKeyPropertiesIdentitys.Count == 0)
            {
                this.InsertObject(entity);
            }
            else
            {
                throw new Exception("La entidad posee primary key sin identity y esta nulla");
            }
        }

        public object? UpdateObject(EntityClass entity, string[] IdObject)
        {
            List<PropertyInfo> entityProps = entity.GetType().GetProperties().ToList();
            List<PropertyInfo> primaryKeyProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
            List<PropertyInfo> manyToOneProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(ManyToOne)) != null).ToList();
            SetManyToOneProperties(entity, manyToOneProperties);
            (string? strQuery, List<IDbDataParameter>? parameters) = QueryBuilder.BuildUpdateQueryByObject(entity, IdObject);
            List<PropertyInfo> oneToManyProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(OneToMany)) != null).ToList();
            var entityType = entity.GetType();
            var newEntityInstance = Activator.CreateInstance(entityType);
            EntityClass newEntity = newEntityInstance as EntityClass;
            newEntity.SetSqlConnection(entity.GetSqlConnection());
            newEntity.SetTransaction(entity.GetTransaction());

            foreach (var primaryKeyProperty in primaryKeyProperties)
            {
                var primaryKeyValue = primaryKeyProperty.GetValue(entity);
                primaryKeyProperty.SetValue(newEntity, primaryKeyValue);
            }
            
            var takeObjectMethod = typeof(WDataMapper).GetMethod("TakeObject");
            var genericMethod = takeObjectMethod?.MakeGenericMethod(entityType);
            var currentEntityInDatabase = genericMethod?.Invoke(this, [newEntity, "", false]);

            foreach (var oneToManyProp in oneToManyProperties)
            {
                string? attributeName = oneToManyProp.Name;
                var attributeValue = oneToManyProp.GetValue(entity);

                if (attributeValue != null)
                {
                    OneToMany? oneToMany = (OneToMany?)Attribute.GetCustomAttribute(oneToManyProp, typeof(OneToMany));
                    if (oneToMany == null) continue;
                    var incomingItems = ((IEnumerable)attributeValue).Cast<object>().ToList();
                    List<object> currentItemsInDatabase = ((IEnumerable?)oneToManyProp.GetValue(currentEntityInDatabase))?.Cast<object>().ToList() ?? [];
                    var itemsToDelete = currentItemsInDatabase
                        .Where(dbItem => !incomingItems.Any(newItem =>
                        {
                            var newItemKey = GetPrimaryKeyValue(newItem);
                            var dbItemKey = GetPrimaryKeyValue(dbItem);
                            return Equals(newItemKey, dbItemKey);
                        }))
                        .ToList();

                    foreach (var itemToDelete in itemsToDelete)
                    {
                        ((EntityClass)itemToDelete).SetConnection(entity.GetConnection());
                        ((EntityClass)itemToDelete).SetSqlConnection(entity.GetSqlConnection());
                        ((EntityClass)itemToDelete).SetTransaction(entity.GetTransaction());
                        Delete((EntityClass)itemToDelete, true);
                    }

                    foreach (var value in incomingItems)
                    {
                        PropertyInfo? keyColumn = entity?.GetType().GetProperty(oneToMany?.KeyColumn);
                        PropertyInfo? foreignKeyColumn = value.GetType().GetProperty(oneToMany?.ForeignKeyColumn);

                        if (foreignKeyColumn != null)
                        {
                            EntityClass entityValue = (EntityClass)value;
                            entityValue?.SetSqlConnection(entity.GetSqlConnection());
                            entityValue?.SetTransaction(entity.GetTransaction());
                            var primaryKeyValue = entity?.GetType()?.GetProperty(keyColumn?.Name)?.GetValue(entity);
                            InsertRelationatedObject(primaryKeyValue, entityValue, foreignKeyColumn, true);
                        }
                    }
                }
            }

            if (strQuery != null)
            {
                GDatos?.ExcuteSqlQuery(strQuery, entity.GetSqlConnection(), entity.GetTransaction(), parameters);
            }

            return entity;
        }

        private object? GetPrimaryKeyValue(object obj)
        {
            var primaryKeyProp = obj.GetType().GetProperties()
                .FirstOrDefault(p => Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null);
            return primaryKeyProp?.GetValue(obj);
        }

        public object? UpdateObject(EntityClass Inst, string IdObject)
        {
            if (Inst.GetType().GetProperty(IdObject)?.GetValue(Inst) == null)
            {
                throw new Exception("El valor de la propiedad "
                    + IdObject + " en la instancia "
                    + Inst.GetType().Name + " es nulo y no se puede actualizar.");
            }

            (string? strQuery, List<IDbDataParameter>? parameters) = QueryBuilder.BuildUpdateQueryByObject(Inst, IdObject);
            return GDatos?.ExcuteSqlQuery(strQuery, Inst.GetSqlConnection(), Inst.GetTransaction(), parameters);
        }

        public object? Delete(EntityClass Inst, bool fullDelete = false)
        {
            (string? strQuery, List<IDbDataParameter>? parameters) = QueryBuilder.BuildDeleteQuery(Inst, fullDelete);
            return GDatos?.ExcuteSqlQueryWithOutScalar(strQuery, Inst.GetSqlConnection(), Inst.GetTransaction(), parameters);
        }

        public int Count(EntityClass Inst)
        {
            (string queryString, string queryCount, List<IDbDataParameter>? parameters) =
                QueryBuilder.BuildSelectQuery(Inst, "", 3);
            try
            {
                DataTable? Table = GDatos?.TraerDatosSQL(queryCount, Inst.GetSqlConnection(), Inst.GetTransaction(), parameters);
                return Convert.ToInt32(Table?.Rows[0][0]);
            }
            catch (Exception e)
            {
                GDatos?.ReStartData(e);
                LoggerServices.AddMessageError($"ERROR: BuildTable - {Inst.GetType().Name} - {queryString}", e);
                throw;
            }
        }

        #endregion

        #region LECTURA DE OBJETOS (SÍNCRONOS - SIN CAMBIOS)

        public List<T> TakeList<T>(EntityClass Inst, string CondSQL = "", bool isSimpleFind = false)
        {
            DataTable? Table = BuildTable(Inst, ref CondSQL, isSimpleFind);
            List<T> ListD = AdapterUtil.ConvertDataTable<T>(Table, Inst);
            return ListD;
        }

        public T? TakeObject<T>(EntityClass Inst, string CondSQL = "", bool isSimpleFind = false)
        {
            Inst!.filterData!.Add(FilterData.Limit(1));
            (string queryString, string queryCount, List<IDbDataParameter>? parameters) = QueryBuilder.BuildSelectQuery(Inst, CondSQL, isSimpleFind ? 3 : 0);

            try
            {
                if (!queryCount.ToUpper().Contains(" WHERE "))
                {
                    throw new Exception($"No es posible buscar el objeto. La entidad {Inst.GetType().Name} requiere filtros o parámetros con valores para hacer la comparación.");
                }

                DataTable? Table = GDatos?.TraerDatosSQL(queryString, Inst.GetSqlConnection(), Inst.GetTransaction(), parameters);

                if (Table?.Rows.Count != 0)
                {
                    var CObject = AdapterUtil.ConvertRow<T>(Inst, Table?.Rows[0]);
                    return CObject;
                }
                else
                {
                    return default;
                }
            }
            catch (System.Exception e)
            {
                GDatos?.ReStartData(e);
                LoggerServices.AddMessageError($"ERROR: TakeList - {Inst.GetType().Name} - {queryString}", e);
                throw;
            }
        }

        public List<T> TakeListWithProcedure<T>(StoreProcedureClass Inst, List<Object> Params)
        {
            try
            {
                DataTable? Table = GDatos?.ExecuteProcedureWithSQL(Inst, Params);
                List<T> ListD = AdapterUtil.ConvertDataTable<T>(Table, Inst);
                return ListD;
            }
            catch (Exception e)
            {
                GDatos?.ReStartData(e);
                LoggerServices.AddMessageError("ERROR: TakeListWithProcedure", e);
                throw;
            }
        }

        public DataTable? BuildTable(EntityClass Inst, ref string CondSQL, bool isSimpleFind = false)
        {
            (string queryString, string queryCount, List<IDbDataParameter>? parameters) = QueryBuilder.BuildSelectQuery(Inst, CondSQL, isSimpleFind ? 3 : 0);

            try
            {
                DataTable? Table = GDatos?.TraerDatosSQL(queryString, Inst.GetSqlConnection(), Inst.GetTransaction(), parameters);
                return Table;
            }
            catch (Exception e)
            {
                GDatos?.ReStartData(e);
                string cadenaCompleta = string.Join(Environment.NewLine, parameters.Select(p => $"{p.ParameterName} = {p.Value}"));
                LoggerServices.AddMessageError($"ERROR: BuildTable \n\n {Inst.GetType().Name} \n\n {cadenaCompleta} \n {queryString}", e);
                throw;
            }
        }

        internal List<EntityProps> DescribeEntity(EntityClass entityClass)
        {
            if (GDatos.GetSqlType == SqlEnumType.MYSQL)
            {
                GDatos.TestConnection();
            }
            return GDatos.EntityDescription?.Where(x => x.TABLE_NAME.ToLower() == entityClass.GetType().Name.ToLower()).ToList() ?? new List<EntityProps>();
        }

        internal void SetPropertyNull(EntityClass entityClass, params string[] propertys)
        {
            string? strQuery = QueryBuilder.BuildUpdateNullsPropertys(entityClass, propertys);

            if (strQuery != null)
            {
                GDatos?.ExcuteSqlQuery(strQuery, entityClass.GetSqlConnection(), entityClass.GetTransaction());
            }
        }

        #endregion

        #region 🚀 MÉTODOS ASÍNCRONOS (NUEVOS)

        /// <summary>
        /// Versión asíncrona de InsertObject. Libera el hilo durante operaciones de BD.
        /// </summary>
        public async Task<object?> InsertObjectAsync(EntityClass entity, bool fullInsert = true)
        {
            List<PropertyInfo> entityProps = entity.GetType().GetProperties().ToList();
            List<PropertyInfo> primaryKeyProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
            List<PropertyInfo> manyToOneProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(ManyToOne)) != null).ToList();
            
            if (fullInsert)
            {
                SetManyToOneProperties(entity, manyToOneProperties);
            }

            (string? strQuery, List<IDbDataParameter>? parameters) = QueryBuilder.BuildInsertQueryByObjectParameters(entity);

            if (strQuery == null)
            {
                return null;
            }
            
            // 🚀 Usar método async
            object? idGenerated = await GDatos!.ExcuteSqlQueryAsync(strQuery, entity.GetSqlConnection(), entity.GetTransaction(), parameters);

            if (primaryKeyProperties.Count == 1)
            {
                PrimaryKey? pkInfo = (PrimaryKey?)Attribute.GetCustomAttribute(primaryKeyProperties[0], typeof(PrimaryKey));
                if (pkInfo?.Identity == true)
                {
                    Type? pkType = Nullable.GetUnderlyingType(primaryKeyProperties[0].PropertyType);
                    primaryKeyProperties[0].SetValue(entity, Convert.ChangeType(idGenerated, pkType));
                }
            }
            
            if (!fullInsert)
            {
                return entity;
            }

            List<PropertyInfo> oneToOneProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(OneToOne)) != null).ToList();

            foreach (var oneToOneProp in oneToOneProperties)
            {
                string? attributeName = oneToOneProp.Name;
                var attributeValue = (EntityClass?)oneToOneProp.GetValue(entity);

                if (attributeValue != null)
                {
                    OneToOne? oneToOne = (OneToOne?)Attribute.GetCustomAttribute(oneToOneProp, typeof(OneToOne));
                    PropertyInfo? keyColumn = entity?.GetType().GetProperty(oneToOne?.KeyColumn);
                    PropertyInfo? foreignKeyColumn = attributeValue.GetType().GetProperty(oneToOne?.ForeignKeyColumn);

                    if (foreignKeyColumn != null)
                    {
                        var primaryKeyValue = entity?.GetType()?.GetProperty(keyColumn?.Name)?.GetValue(entity);
                        foreignKeyColumn.SetValue(attributeValue, primaryKeyValue);
                        attributeValue?.SetSqlConnection(entity.GetSqlConnection());
                        attributeValue?.SetTransaction(entity.GetTransaction());
                        await InsertObjectAsync(attributeValue);
                    }
                }
            }

            List<PropertyInfo> oneToManyProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(OneToMany)) != null).ToList();

            foreach (var oneToManyProp in oneToManyProperties)
            {
                string? attributeName = oneToManyProp.Name;
                var attributeValue = oneToManyProp.GetValue(entity);

                if (attributeValue != null)
                {
                    OneToMany? oneToMany = (OneToMany?)Attribute.GetCustomAttribute(oneToManyProp, typeof(OneToMany));

                    foreach (var value in ((IEnumerable)attributeValue))
                    {
                        PropertyInfo? keyColumn = entity?.GetType().GetProperty(oneToMany?.KeyColumn);
                        PropertyInfo? foreignKeyColumn = value.GetType().GetProperty(oneToMany?.ForeignKeyColumn);

                        if (foreignKeyColumn != null)
                        {
                            EntityClass entityValue = (EntityClass)value;
                            entityValue?.SetSqlConnection(entity.GetSqlConnection());
                            entityValue?.SetTransaction(entity.GetTransaction());
                            var primaryKeyValue = entity?.GetType()?.GetProperty(keyColumn?.Name)?.GetValue(entity);
                            await InsertRelationatedObjectAsync(primaryKeyValue, entityValue, foreignKeyColumn);
                        }
                    }
                }
            }

            return entity;
        }

        /// <summary>
        /// Versión asíncrona de InsertRelationatedObject
        /// </summary>
        private async Task InsertRelationatedObjectAsync(object foreignKeyValue, EntityClass entity, PropertyInfo foreignKeyColumn, bool isUpdate = false)
        {
            foreignKeyColumn.SetValue(entity, foreignKeyValue);
            List<PropertyInfo> entityProps = entity.GetType().GetProperties().ToList();
            var primaryKeyProperties = entityProps.Where(p => (PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
            var primaryKeyPropertiesIdentitys = entityProps
                .Where(p => (PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null
                && ((PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)))?.Identity == true).ToList();
            var values = primaryKeyProperties.Where(p => p.GetValue(entity) != null).ToList();

            if (primaryKeyProperties.Count == values.Count && isUpdate)
            {
                await UpdateObjectAsync(entity, primaryKeyProperties.Select(p => p.Name).ToArray());
            }
            else if (primaryKeyPropertiesIdentitys.Count == 1 || primaryKeyPropertiesIdentitys.Count == 0)
            {
                await this.InsertObjectAsync(entity);
            }
            else
            {
                throw new Exception("La entidad posee primary key sin identity y esta nulla");
            }
        }

        /// <summary>
        /// Versión asíncrona de UpdateObject
        /// </summary>
        public async Task<object?> UpdateObjectAsync(EntityClass entity, string[] IdObject)
        {
            List<PropertyInfo> entityProps = entity.GetType().GetProperties().ToList();
            List<PropertyInfo> primaryKeyProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
            List<PropertyInfo> manyToOneProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(ManyToOne)) != null).ToList();
            SetManyToOneProperties(entity, manyToOneProperties);
            (string? strQuery, List<IDbDataParameter>? parameters) = QueryBuilder.BuildUpdateQueryByObject(entity, IdObject);
            List<PropertyInfo> oneToManyProperties = entityProps.Where(p => Attribute.GetCustomAttribute(p, typeof(OneToMany)) != null).ToList();
            var entityType = entity.GetType();
            var newEntityInstance = Activator.CreateInstance(entityType);
            EntityClass newEntity = newEntityInstance as EntityClass;
            newEntity.SetSqlConnection(entity.GetSqlConnection());
            newEntity.SetTransaction(entity.GetTransaction());

            foreach (var primaryKeyProperty in primaryKeyProperties)
            {
                var primaryKeyValue = primaryKeyProperty.GetValue(entity);
                primaryKeyProperty.SetValue(newEntity, primaryKeyValue);
            }
            
            var takeObjectMethod = typeof(WDataMapper).GetMethod("TakeObject");
            var genericMethod = takeObjectMethod?.MakeGenericMethod(entityType);
            var currentEntityInDatabase = genericMethod?.Invoke(this, [newEntity, "", false]);

            foreach (var oneToManyProp in oneToManyProperties)
            {
                string? attributeName = oneToManyProp.Name;
                var attributeValue = oneToManyProp.GetValue(entity);

                if (attributeValue != null)
                {
                    OneToMany? oneToMany = (OneToMany?)Attribute.GetCustomAttribute(oneToManyProp, typeof(OneToMany));
                    if (oneToMany == null) continue;
                    var incomingItems = ((IEnumerable)attributeValue).Cast<object>().ToList();
                    List<object> currentItemsInDatabase = ((IEnumerable?)oneToManyProp.GetValue(currentEntityInDatabase))?.Cast<object>().ToList() ?? [];
                    var itemsToDelete = currentItemsInDatabase
                        .Where(dbItem => !incomingItems.Any(newItem =>
                        {
                            var newItemKey = GetPrimaryKeyValue(newItem);
                            var dbItemKey = GetPrimaryKeyValue(dbItem);
                            return Equals(newItemKey, dbItemKey);
                        }))
                        .ToList();

                    foreach (var itemToDelete in itemsToDelete)
                    {
                        ((EntityClass)itemToDelete).SetConnection(entity.GetConnection());
                        ((EntityClass)itemToDelete).SetSqlConnection(entity.GetSqlConnection());
                        ((EntityClass)itemToDelete).SetTransaction(entity.GetTransaction());
                        await DeleteAsync((EntityClass)itemToDelete, true);
                    }

                    foreach (var value in incomingItems)
                    {
                        PropertyInfo? keyColumn = entity?.GetType().GetProperty(oneToMany?.KeyColumn);
                        PropertyInfo? foreignKeyColumn = value.GetType().GetProperty(oneToMany?.ForeignKeyColumn);

                        if (foreignKeyColumn != null)
                        {
                            EntityClass entityValue = (EntityClass)value;
                            entityValue?.SetSqlConnection(entity.GetSqlConnection());
                            entityValue?.SetTransaction(entity.GetTransaction());
                            var primaryKeyValue = entity?.GetType()?.GetProperty(keyColumn?.Name)?.GetValue(entity);
                            await InsertRelationatedObjectAsync(primaryKeyValue, entityValue, foreignKeyColumn, true);
                        }
                    }
                }
            }

            if (strQuery != null)
            {
                await GDatos!.ExcuteSqlQueryAsync(strQuery, entity.GetSqlConnection(), entity.GetTransaction(), parameters);
            }

            return entity;
        }

        /// <summary>
        /// Versión asíncrona de Delete
        /// </summary>
        public async Task<bool> DeleteAsync(EntityClass Inst, bool fullDelete = false)
        {
            (string? strQuery, List<IDbDataParameter>? parameters) = QueryBuilder.BuildDeleteQuery(Inst, fullDelete);
            return await GDatos!.ExcuteSqlQueryWithOutScalarAsync(strQuery, Inst.GetSqlConnection(), Inst.GetTransaction(), parameters);
        }

        /// <summary>
        /// Versión asíncrona de TakeList
        /// </summary>
        public async Task<List<T>> TakeListAsync<T>(EntityClass Inst, string CondSQL = "", bool isSimpleFind = false)
        {
            DataTable? Table = await BuildTableAsync(Inst, CondSQL, isSimpleFind);
            List<T> ListD = AdapterUtil.ConvertDataTable<T>(Table, Inst);
            return ListD;
        }

        /// <summary>
        /// Versión asíncrona de TakeObject
        /// </summary>
        public async Task<T?> TakeObjectAsync<T>(EntityClass Inst, string CondSQL = "", bool isSimpleFind = false)
        {
            Inst!.filterData!.Add(FilterData.Limit(1));
            (string queryString, string queryCount, List<IDbDataParameter>? parameters) = QueryBuilder.BuildSelectQuery(Inst, CondSQL, isSimpleFind ? 3 : 0);

            try
            {
                if (!queryCount.ToUpper().Contains(" WHERE "))
                {
                    throw new Exception($"No es posible buscar el objeto. La entidad {Inst.GetType().Name} requiere filtros o parámetros con valores para hacer la comparación.");
                }

                // 🚀 Usar método async
                DataTable? Table = await GDatos!.TraerDatosSQLAsync(queryString, Inst.GetSqlConnection(), Inst.GetTransaction(), parameters);

                if (Table?.Rows.Count != 0)
                {
                    var CObject = AdapterUtil.ConvertRow<T>(Inst, Table?.Rows[0]);
                    return CObject;
                }
                else
                {
                    return default;
                }
            }
            catch (System.Exception e)
            {
                GDatos?.ReStartData(e);
                LoggerServices.AddMessageError($"ERROR: TakeListAsync - {Inst.GetType().Name} - {queryString}", e);
                throw;
            }
        }

        /// <summary>
        /// Versión asíncrona de BuildTable
        /// </summary>
        public async Task<DataTable?> BuildTableAsync(EntityClass Inst, string CondSQL, bool isSimpleFind = false)
        {
            (string queryString, string queryCount, List<IDbDataParameter>? parameters) = QueryBuilder.BuildSelectQuery(Inst, CondSQL, isSimpleFind ? 3 : 0);

            try
            {
                // 🚀 Usar método async
                DataTable? Table = await GDatos!.TraerDatosSQLAsync(queryString, Inst.GetSqlConnection(), Inst.GetTransaction(), parameters);
                return Table;
            }
            catch (Exception e)
            {
                GDatos?.ReStartData(e);
                string cadenaCompleta = string.Join(Environment.NewLine, parameters.Select(p => $"{p.ParameterName} = {p.Value}"));
                LoggerServices.AddMessageError($"ERROR: BuildTableAsync \n\n {Inst.GetType().Name} \n\n {cadenaCompleta} \n {queryString}", e);
                throw;
            }
        }

        #endregion
    }
}