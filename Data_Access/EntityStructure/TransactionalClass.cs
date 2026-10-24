using System.Data;
using System.Data.Common;
using APPCORE.BDCore;
using APPCORE.BDCore.Abstracts;

namespace APPCORE;

public abstract class TransactionalClass
{
	internal IDbTransaction? Transaction;
	internal IDbConnection? SqlConection;
	internal WDataMapper? Conection;
	protected WDataMapper? MDataMapper
	{
		get
		{
			if (this.Conection != null)
				return this.Conection;
			else
				return Connections.Default;
		}
		set { Conection = value; }
	}
	internal WDataMapper? GetConnection()
	{
		return MDataMapper;
	}
	public void SetConnection(WDataMapper? wDataMapper)
	{
		MDataMapper = wDataMapper;
	}

	//TRANSACCIONES
	public void BeginGlobalTransaction()
	{
		MDataMapper?.GDatos.BeginGlobalTransaction();
	}
	public void CommitGlobalTransaction()
	{
		MDataMapper?.GDatos.CommitGlobalTransaction();
	}
	public void RollBackGlobalTransaction()
	{
		MDataMapper?.GDatos.RollBackGlobalTransaction();
	}
	

	internal IDbTransaction? GetTransaction()
	{
		return this.Transaction;
	}

	internal IDbConnection? GetSqlConnection()
	{
		return SqlConection;
	}
	

	internal void SetTransaction(IDbTransaction? transaction)
	{
		Transaction = transaction;
	}

	internal void SetSqlConnection(IDbConnection? connection)
	{
		SqlConection = connection;
	}
	public object? ExecuteSqlQuery(string query)
	{
		return MDataMapper?.GDatos.ExcuteSqlQueryWithOutScalar(query, GetSqlConnection(), GetTransaction());
	}

	/// <summary>
    /// Helper: Abre la conexión de forma asíncrona
    /// </summary>
    internal static async Task OpenConnectionAsync(IDbConnection? conn)
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
    internal static async Task<IDbTransaction?> BeginTransactionAsync(IDbConnection? conn)
    {
        if (conn is DbConnection dbConn)
        {
            return await dbConn.BeginTransactionAsync();
        }
        return conn?.BeginTransaction(); // Fallback síncrono
    }
}
