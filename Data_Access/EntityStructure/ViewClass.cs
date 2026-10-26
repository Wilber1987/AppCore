using System.Collections;
using System.Data;
using System.Data.Common; // ⬅️ Para OpenAsync y BeginTransactionAsync
using System.Reflection;
using APPCORE.BDCore.Abstracts;
namespace APPCORE;

public abstract class ViewClass : TransactionalClass
{
	private List<FilterData>? filters;
	// Lista de filtros de datos que pueden aplicarse a las consultas de la entidad
	public List<FilterData>? filterData
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
                //var method = typeof(WDataMapper).GetMethod("TakeList")?.MakeGenericMethod(entityType);
               Type[] paramTypes = [typeof(ViewClass), typeof(string), typeof(bool)];

                var method = typeof(WDataMapper)
                    .GetMethod("TakeObject", paramTypes)
                    ?.MakeGenericMethod(entityType);
               
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

}
