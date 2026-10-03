using System.Collections;
using System.Data;
using System.Data.Common; // ⬅️ Para OpenAsync y BeginTransactionAsync
using System.Reflection;
using APPCORE.BDCore.Abstracts;

namespace APPCORE;

public abstract class EntityClass : TransactionalClass
{
    private List<FilterData>? filters;

    public List<FilterData> filterData
    {
        get
        {
            if (filters == null)
            {
                filters = [];
            }
            return filters;
        }
        set
        {
            filters = value;
        }
    }

    public List<OrdeData>? orderData { get; set; }

    // ==================== MÉTODOS SÍNCRONOS (EXISTENTES - SIN CAMBIOS) ====================

    public List<T> Get<T>(string condition = "")
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            this.SetSqlConnection(conn);
            var Data = MDataMapper?.TakeList<T>(this, condition);
            return Data?.ToList() ?? new List<T>();
        }
    }

    public List<T> Where<T>(params FilterData[] where_condition)
    {
        if (IsValidFilter(where_condition))
        {
            return new List<T>();
        }

        if (filterData == null)
            filterData = new List<FilterData>();

        filterData.AddRange(where_condition.ToList());
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            this.SetSqlConnection(conn);
            var Data = MDataMapper?.TakeList<T>(this);
            return Data ?? new List<T>();
        }
    }

    public T? Find<T>(params FilterData[]? where_condition)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            this.SetSqlConnection(conn);
            if (filterData!.Count == 0)
            {
                filterData = where_condition?.ToList();
            }
            else
            {
                filterData.AddRange(where_condition?.ToList() ?? []);
            }
            var Data = MDataMapper != null ? MDataMapper.TakeObject<T>(this) : default(T);
            return Data;
        }
    }

    public T? SimpleFind<T>(params FilterData[]? where_condition)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            this.SetSqlConnection(conn);
            filterData = where_condition?.ToList();
            var Data = MDataMapper != null ? MDataMapper.TakeObject<T>(this, "", true) : default(T);
            return Data;
        }
    }

    public Boolean Exists()
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            this.SetSqlConnection(conn);

            Type entityType = this.GetType();
            PropertyInfo[] lst = this.GetType().GetProperties();
            var pkProperties = lst.Where(p => (PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
            var values = pkProperties.Where(p => p.GetValue(this) != null).ToList();

            if (pkProperties.Count == values.Count)
            {
                var method = typeof(WDataMapper).GetMethod("TakeList")?.MakeGenericMethod(entityType);
                var data = method?.Invoke(MDataMapper, [this, "", true]) as IList;
                return data?.Count > 0;
            }
            return false;
        }
    }

    public List<T> SimpleGet<T>()
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            this.SetSqlConnection(conn);
            var Data = MDataMapper?.TakeList<T>(this, "", true);
            return Data ?? new List<T>();
        }
    }

    public object? Save(bool fullInsert = true)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            var transaction = conn?.BeginTransaction();
            SetSqlConnection(conn);
            SetTransaction(transaction);
            try
            {
                var result = MDataMapper?.InsertObject(this, fullInsert);
                transaction?.Commit();
                return result;
            }
            catch (Exception e)
            {
                transaction?.Rollback();
                conn?.Dispose();
                LoggerServices.AddMessageError("ERROR: Save entity", e);
                throw;
            }
        }
    }

    public ResponseService Update()
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            var transaction = conn?.BeginTransaction();
            this.SetSqlConnection(conn);
            this.SetTransaction(transaction);
            try
            {
                PropertyInfo[] lst = this.GetType().GetProperties();
                var pkPropiertys = lst.Where(p => (PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
                var values = pkPropiertys.Where(p => p.GetValue(this) != null).ToList();

                if (pkPropiertys.Count == values.Count)
                {
                    this.Update(pkPropiertys.Select(p => p.Name).ToArray());
                    transaction?.Commit();
                    return new ResponseService() { status = 200, message = this.GetType().Name + " actualizado correctamente" };
                }
                else
                    return new ResponseService() { status = 500, message = "Error al actualizar: no se encuentra el registro " + this.GetType().Name };
            }
            catch (Exception e)
            {
                transaction?.Rollback();
                LoggerServices.AddMessageError("ERROR: Update entity", e);
                conn?.Dispose();
                return new ResponseService()
                {
                    status = 500,
                    message = "Error al actualizar: " + e.Message
                };
            }
        }
    }

    public bool Update(string Id)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            var transaction = conn?.BeginTransaction();
            this.SetSqlConnection(conn);
            this.SetTransaction(transaction);
            try
            {
                MDataMapper?.UpdateObject(this, Id);
                transaction?.Commit();
                return true;
            }
            catch (Exception e)
            {
                transaction?.Rollback();
                LoggerServices.AddMessageError("ERROR: Update entity", e);
                conn?.Dispose();
                return false;
            }
        }
    }

    public bool Update(string[] Id)
    {
        try
        {
            MDataMapper?.UpdateObject(this, Id);
            return true;
        }
        catch (Exception e)
        {
            LoggerServices.AddMessageError("ERROR: Update entity []ID", e);
            throw;
        }
    }

    public ResponseService Delete(bool fullDelete = false)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            conn?.Open();
            var transaction = conn?.BeginTransaction();
            SetSqlConnection(conn);
            SetTransaction(transaction);
            try
            {
                var result = MDataMapper?.Delete(this, fullDelete);
                transaction?.Commit();
                return new ResponseService() { status = 200, message = this.GetType().Name + " Eliminado correctamente" };
            }
            catch (Exception e)
            {
                transaction?.Rollback();
                conn?.Dispose();
                LoggerServices.AddMessageError("ERROR: Save entity", e);
                return new ResponseService()
                {
                    status = 500,
                    message = "Error al eliminar registro: " + e.Message
                };
            }
        }
    }

    public int Count(params FilterData[] where_condition)
    {
        if (IsValidFilter(where_condition))
        {
            return 0;
        }

        if (filterData == null)
            filterData = new List<FilterData>();

        filterData.AddRange(where_condition.ToList());
        using var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? "");
        conn?.Open();
        this.SetSqlConnection(conn);
        var Count = MDataMapper?.Count(this);
        return Count ?? 0;
    }

    public ResponseService SetPropertyNull(params string[] properties)
    {
        using var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? "");
        conn?.Open();
        var transaction = conn?.BeginTransaction();
        this.SetSqlConnection(conn);
        this.SetTransaction(transaction);
        try
        {
            PropertyInfo[] lst = this.GetType().GetProperties();
            var pkPropiertys = lst.Where(p => (PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
            var values = pkPropiertys.Where(p => p.GetValue(this) != null).ToList();

            if (pkPropiertys.Count == values.Count)
            {
                MDataMapper?.SetPropertyNull(this, properties);
                transaction?.Commit();
                return new ResponseService() { status = 200, message = this.GetType().Name + " actualizado correctamente" };
            }
            else
                return new ResponseService() { status = 500, message = "Error al actualizar: no se encuentra el registro " + this.GetType().Name };
        }
        catch (Exception e)
        {
            transaction?.Rollback();
            LoggerServices.AddMessageError("ERROR: Update entity", e);
            return new ResponseService()
            {
                status = 500,
                message = "Error al actualizar: " + e.Message
            };
        }
    }

    public List<EntityProps> DescribeEntity(SqlEnumType sqlEnumType)
    {
        List<EntityProps>? entityProps = MDataMapper?.DescribeEntity(this);
        if (entityProps?.Count == 0)
        {
            throw new Exception("La entidad buscada no existe: " + this.GetType().Name);
        }
        return entityProps ?? [];
    }

    private static bool IsValidFilter(FilterData[] where_condition)
    {
        return where_condition.Where(c => c.FilterType != "or"
                && c.FilterType != "and"
                && c.FilterType != "Not Null"
                && c.FilterType != "NotNull"
                && c.FilterType != "IsNull"
                && c.FilterType != "Is Null"
                && (c.Values == null || c.Values?.Count == 0)).ToList().Count > 0;
    }

    // ==================== 🚀 MÉTODOS ASÍNCRONOS (FLUJO 100% ASYNC) ====================

    /// <summary>
    /// Helper: Abre la conexión de forma asíncrona
    /// </summary>
    private static async Task OpenConnectionAsync(IDbConnection? conn)
    {
        if (conn is DbConnection dbConn)
        {
            await dbConn.OpenAsync();
        }
        else
        {
            conn?.Open(); // Fallback síncrono
        }
    }

    /// <summary>
    /// Helper: Inicia una transacción de forma asíncrona
    /// </summary>
    private static async Task<IDbTransaction?> BeginTransactionAsync(IDbConnection? conn)
    {
        if (conn is DbConnection dbConn)
        {
            return await dbConn.BeginTransactionAsync();
        }
        return conn?.BeginTransaction(); // Fallback síncrono
    }

    /// <summary>
    /// Versión asíncrona de Get<T>. Flujo 100% async sin Task.Run.
    /// </summary>
    public async Task<List<T>> GetAsync<T>(string condition = "")
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            await OpenConnectionAsync(conn);
            this.SetSqlConnection(conn);
            // 🚀 Llamada directa al método async del mapper
            var Data = MDataMapper != null 
                ? await MDataMapper.TakeListAsync<T>(this, condition) 
                : null;
            return Data?.ToList() ?? new List<T>();
        }
    }

    /// <summary>
    /// Versión asíncrona de Where<T>
    /// </summary>
    public async Task<List<T>> WhereAsync<T>(params FilterData[] where_condition)
    {
        if (IsValidFilter(where_condition))
        {
            return new List<T>();
        }

        if (filterData == null)
            filterData = new List<FilterData>();

        filterData.AddRange(where_condition.ToList());
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            await OpenConnectionAsync(conn);
            this.SetSqlConnection(conn);
            // 🚀 Llamada directa al método async del mapper
            var Data = MDataMapper != null 
                ? await MDataMapper.TakeListAsync<T>(this) 
                : null;
            return Data ?? new List<T>();
        }
    }

    /// <summary>
    /// Versión asíncrona de Find<T>
    /// </summary>
    public async Task<T?> FindAsync<T>(params FilterData[]? where_condition)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            await OpenConnectionAsync(conn);
            this.SetSqlConnection(conn);
            if (filterData!.Count == 0)
            {
                filterData = where_condition?.ToList();
            }
            else
            {
                filterData.AddRange(where_condition?.ToList() ?? []);
            }
            // 🚀 Llamada directa al método async del mapper
            var Data = MDataMapper != null 
                ? await MDataMapper.TakeObjectAsync<T>(this) 
                : default(T);
            return Data;
        }
    }

    /// <summary>
    /// Versión asíncrona de SimpleFind<T>
    /// </summary>
    public async Task<T?> SimpleFindAsync<T>(params FilterData[]? where_condition)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            await OpenConnectionAsync(conn);
            this.SetSqlConnection(conn);
            filterData = where_condition?.ToList();
            // 🚀 Llamada directa al método async del mapper
            var Data = MDataMapper != null 
                ? await MDataMapper.TakeObjectAsync<T>(this, "", true) 
                : default(T);
            return Data;
        }
    }

    /// <summary>
    /// Versión asíncrona de SimpleGet<T>
    /// </summary>
    public async Task<List<T>> SimpleGetAsync<T>()
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            await OpenConnectionAsync(conn);
            this.SetSqlConnection(conn);
            // 🚀 Llamada directa al método async del mapper
            var Data = MDataMapper != null 
                ? await MDataMapper.TakeListAsync<T>(this, "", true) 
                : null;
            return Data ?? new List<T>();
        }
    }

    /// <summary>
    /// Versión asíncrona de Save. Flujo 100% async.
    /// </summary>
    public async Task<object?> SaveAsync(bool fullInsert = true)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            await OpenConnectionAsync(conn);
            var transaction = await BeginTransactionAsync(conn);
            SetSqlConnection(conn);
            SetTransaction(transaction);
            try
            {
                // 🚀 Llamada directa al método async del mapper
                var result = MDataMapper != null 
                    ? await MDataMapper.InsertObjectAsync(this, fullInsert) 
                    : null;
                transaction?.Commit();
                return result;
            }
            catch (Exception e)
            {
                transaction?.Rollback();
                conn?.Dispose();
                LoggerServices.AddMessageError("ERROR: Save entity (async)", e);
                throw;
            }
        }
    }

    /// <summary>
    /// Versión asíncrona de Update(). Flujo 100% async.
    /// </summary>
    public async Task<ResponseService> UpdateAsync()
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            await OpenConnectionAsync(conn);
            var transaction = await BeginTransactionAsync(conn);
            this.SetSqlConnection(conn);
            this.SetTransaction(transaction);
            try
            {
                PropertyInfo[] lst = this.GetType().GetProperties();
                var pkPropiertys = lst.Where(p => (PrimaryKey?)Attribute.GetCustomAttribute(p, typeof(PrimaryKey)) != null).ToList();
                var values = pkPropiertys.Where(p => p.GetValue(this) != null).ToList();

                if (pkPropiertys.Count == values.Count)
                {
                    // 🚀 Llamada directa al método async del mapper
                    await UpdateInternalAsync(pkPropiertys.Select(p => p.Name).ToArray());
                    transaction?.Commit();
                    return new ResponseService() { status = 200, message = this.GetType().Name + " actualizado correctamente" };
                }
                else
                    return new ResponseService() { status = 500, message = "Error al actualizar: no se encuentra el registro " + this.GetType().Name };
            }
            catch (Exception e)
            {
                transaction?.Rollback();
                LoggerServices.AddMessageError("ERROR: Update entity (async)", e);
                conn?.Dispose();
                return new ResponseService()
                {
                    status = 500,
                    message = "Error al actualizar: " + e.Message
                };
            }
        }
    }

    /// <summary>
    /// Helper interno: Update por array de IDs, versión async
    /// </summary>
    private async Task UpdateInternalAsync(string[] Id)
    {
        // 🚀 Llamada directa al método async del mapper
        if (MDataMapper != null)
        {
            await MDataMapper.UpdateObjectAsync(this, Id);
        }
    }

    /// <summary>
    /// Versión asíncrona de Delete
    /// </summary>
    public async Task<ResponseService> DeleteAsync(bool fullDelete = false)
    {
        using (var conn = MDataMapper?.GDatos.CrearConexion(MDataMapper?.GDatos?.ConexionString ?? ""))
        {
            await OpenConnectionAsync(conn);
            var transaction = await BeginTransactionAsync(conn);
            SetSqlConnection(conn);
            SetTransaction(transaction);
            try
            {
                // 🚀 Llamada directa al método async del mapper
                var result = MDataMapper != null 
                    ? await MDataMapper.DeleteAsync(this, fullDelete) 
                    : false;
                transaction?.Commit();
                return new ResponseService() { status = 200, message = this.GetType().Name + " Eliminado correctamente" };
            }
            catch (Exception e)
            {
                transaction?.Rollback();
                conn?.Dispose();
                LoggerServices.AddMessageError("ERROR: Delete entity (async)", e);
                return new ResponseService()
                {
                    status = 500,
                    message = "Error al eliminar registro: " + e.Message
                };
            }
        }
    }
}