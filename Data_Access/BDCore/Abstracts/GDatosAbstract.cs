using System.Data;
using System.Data.Common; // ⬅️ IMPORTANTE: Para DbConnection y DbCommand
using System.Transactions;
using APPCORE.BDCore.Abstracts;
using APPCORE.BDCore.MySqlImplementations;
using APPCORE.BDCore.PostgresImplementations;
using APPCORE.BDCore.SQLServerImplementations;

namespace APPCORE
{
    public abstract class GDatosAbstract
    {
        public string? Database { get; set; }

        protected IDbConnection SQLMCon
        {
            get
            {
                if (this.MTConnection != null && this.MTConnection.ConnectionString?.Length > 0)
                {
                    return this.MTConnection;
                }
                this.MTConnection = CrearConexion(ConexionString ?? " ");
                return this.MTConnection;
            }
        }

        public string? ConexionString;
        protected TransactionScope? MTransaccion;
        protected bool globalTransaction;
        protected IDbConnection? MTConnection;

        public abstract IDbConnection CrearConexion(string cadena);
        protected abstract IDbCommand ComandoSql(string comandoSql, IDbConnection connection);
        protected abstract IDataAdapter CrearDataAdapterSql(string comandoSql, IDbConnection connection);
        protected abstract IDataAdapter CrearDataAdapterSql(IDbCommand comandoSql);
        public abstract object ExecuteProcedure(StoreProcedureClass Inst, List<object> Params);
        public abstract DataTable ExecuteProcedureWithSQL(StoreProcedureClass Inst, List<object> Params);

        public List<EntityProps>? EntityDescription { get; set; }
        public SqlEnumType GetSqlType { get; set; }

        // ==================== TRANSACCIONES ====================
        public void BeginGlobalTransaction()
        {
            if (this.GetSqlType == SqlEnumType.MYSQL) return;
            if (this.globalTransaction)
                throw new InvalidOperationException("No se puede iniciar una segunda global transaction sin antes haber finalizado la anterior");
            MTransaccion = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled); // ⬅️ Importante para async
            this.globalTransaction = true;
        }

        public void CommitGlobalTransaction()
        {
            if (this.GetSqlType == SqlEnumType.MYSQL) return;
            if (this.MTransaccion != null)
            {
                try { MTransaccion.Complete(); }
                catch (Exception ex)
                {
                    LoggerServices.AddMessageError("Error committing transaction", ex);
                    throw;
                }
                finally
                {
                    MTransaccion.Dispose();
                    this.globalTransaction = false;
                }
            }
        }

        public void RollBackGlobalTransaction()
        {
            if (this.GetSqlType == SqlEnumType.MYSQL) return;
            if (this.MTransaccion != null)
            {
                this.MTransaccion = null;
                this.globalTransaction = false;
            }
        }

        // ==================== MÉTODOS SÍNCRONOS (EXISTENTES - SIN CAMBIOS) ====================
        
        public bool TestConnection()
        {
            try
            {
                using (SQLMCon)
                {
                    SQLMCon.Open();
                    string describeEntityQuery = GetSqlType switch
                    {
                        SqlEnumType.SQL_SERVER => SQLServerEntityQuerys.DescribeEntitys,
                        SqlEnumType.POSTGRES_SQL => PostgreEntityQuerys.DescribeEntitys,
                        SqlEnumType.MYSQL => MySqlEntityQuerys.DescribeEntityQuery.Replace("entityDatabase", Database),
                        _ => ""
                    };
                    this.EntityDescription = AdapterUtil.ConvertDataTable<EntityProps>(
                        TraerDatosSQL(describeEntityQuery, SQLMCon, null, null), new EntityProps());
                    EnsureSoftDeleteColumn(SQLMCon);
                    this.EntityDescription = AdapterUtil.ConvertDataTable<EntityProps>(
                        TraerDatosSQL(describeEntityQuery, SQLMCon, null, null), new EntityProps());
                }
                return true;
            }
            catch (Exception ex)
            {
                LoggerServices.AddMessageError("error conectando a bd", ex);
                throw;
            }
        }

        private void EnsureSoftDeleteColumn(IDbConnection connection)
        {
            if (EntityDescription == null || !EntityDescription.Any()) return;

            var tables = EntityDescription.GroupBy(x => new { x.TABLE_SCHEMA, x.TABLE_NAME });
            foreach (var table in tables)
            {
                bool hasIsDeleted = table.Any(c => c.COLUMN_NAME.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase));
                if (hasIsDeleted) continue;

                string schema = table.Key.TABLE_SCHEMA;
                string tableName = table.Key.TABLE_NAME;
                string sql = GetSqlType switch
                {
                    SqlEnumType.SQL_SERVER => $@"ALTER TABLE [{schema}].[{tableName}] ADD IsDeleted BIT NOT NULL DEFAULT(0)",
                    SqlEnumType.POSTGRES_SQL => $@"ALTER TABLE ""{schema}"".""{tableName}"" ADD COLUMN ""IsDeleted"" BOOLEAN NOT NULL DEFAULT FALSE",
                    SqlEnumType.MYSQL => $@"ALTER TABLE `{tableName}` ADD COLUMN `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0",
                    _ => throw new NotSupportedException("Motor no soportado")
                };

                try
                {
                    using var command = ComandoSql(sql, connection);
                    command.ExecuteNonQuery();
                    LoggerServices.AddMessageInfo($"Columna IsDeleted agregada a {tableName}");
                }
                catch (Exception ex)
                {
                    LoggerServices.AddMessageError($"Error agregando IsDeleted en {tableName}", ex);
                }
            }
        }

        public object? ExcuteSqlQuery(string strQuery, IDbConnection dbConnection, IDbTransaction? dbTransaction, List<IDbDataParameter>? parameters = null)
        {
            return ExecuteWithRetry(() =>
            {
                using (var command = ComandoSql(strQuery, dbConnection))
                {
                    command.Transaction = dbTransaction;
                    SetParametersInCommand(parameters, command);
                    var scalar = command.ExecuteScalar();
                    return scalar == DBNull.Value ? true : Convert.ToInt32(scalar);
                }
            });
        }

        public object? ExcuteSqlQueryWithOutScalar(string strQuery, IDbConnection dbConnection, IDbTransaction? dbTransaction, List<IDbDataParameter>? parameters = null)
        {
            try
            {
                using (var command = ComandoSql(strQuery, dbConnection))
                {
                    command.Transaction = dbTransaction;
                    SetParametersInCommand(parameters, command);
                    command.ExecuteNonQuery();
                    return true;
                }
            }
            catch (System.Exception)
            {
                ReStartData();
                return false;
            }
        }

        protected object ExecuteWithRetry(Func<object> operation, int maxRetries = 0)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    return operation();
                }
                catch (Exception ex)
                {
                    if (retries >= maxRetries)
                    {
                        LoggerServices.AddMessageError("ERROR: Max retries reached. Operation failed.", ex);
                        this.ReStartData(ex);
                        throw;
                    }
                    retries++;
                    Console.WriteLine($"read retry query => {retries}");
                    Task.Delay(100).Wait(); // ⚠️ SÍNCRONO - Bloquea el hilo
                }
            }
        }

        public void ReStartData(Exception ex)
        {
            ReStartData();
            LoggerServices.AddMessageError("Transaction failed and connection restarted.", ex);
        }

        public void ReStartData()
        {
            globalTransaction = false;
            this.MTConnection = null;
            this.MTransaccion = null;
        }

        public DataTable TraerDatosSQL(string queryString, IDbConnection dbConnection, IDbTransaction? dbTransaction, List<IDbDataParameter>? parameters = null)
        {
            return (DataTable)ExecuteWithRetry(() =>
            {
                DataTable resultTable = new DataTable();
                using (var command = ComandoSql(queryString, dbConnection))
                {
                    command.Transaction = dbTransaction;
                    SetParametersInCommand(parameters, command);
                    using (var reader = command.ExecuteReader())
                    {
                        resultTable.Load(reader);
                    }
                    return resultTable;
                }
            });
        }

        public DataTable TraerDatosSQL(IDbCommand Command)
        {
            return (DataTable)ExecuteWithRetry(() =>
            {
                DataSet ObjDS = new DataSet();
                CrearDataAdapterSql(Command).Fill(ObjDS);
                return ObjDS.Tables[0].Copy();
            });
        }

        public DataTable TraerDatosSQL(string queryString)
        {
            return (DataTable)ExecuteWithRetry(() =>
            {
                DataSet ObjDS = new DataSet();
                CrearDataAdapterSql(ComandoSql(queryString, CrearConexion(ConexionString ?? ""))).Fill(ObjDS);
                return ObjDS.Tables[0].Copy();
            });
        }

        // ==================== 🚀 MÉTODOS ASÍNCRONOS (NUEVOS) ====================

        /// <summary>
        /// Versión asíncrona de ExecuteWithRetry. LIBERA el hilo durante los reintentos.
        /// </summary>
        protected async Task<object?> ExecuteWithRetryAsync(Func<Task<object?>> operation, int maxRetries = 3)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    if (retries >= maxRetries)
                    {
                        LoggerServices.AddMessageError("ERROR: Max retries reached (async). Operation failed.", ex);
                        this.ReStartData(ex);
                        throw;
                    }
                    retries++;
                    Console.WriteLine($"async retry query => {retries}");
                    await Task.Delay(100 * retries); // ⬅️ Exponential backoff + LIBERA el hilo
                }
            }
        }

        /// <summary>
        /// Ejecuta una consulta SQL asíncrona con ExecuteScalarAsync
        /// </summary>
        public async Task<object?> ExcuteSqlQueryAsync(string strQuery, IDbConnection dbConnection, IDbTransaction? dbTransaction, List<IDbDataParameter>? parameters = null)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                // Casting a DbCommand para acceder a métodos async nativos
                using var command = ComandoSql(strQuery, dbConnection);
                var dbCommand = command as DbCommand;
                
                if (dbCommand == null)
                {
                    // Fallback síncrono si el provider no soporta DbCommand
                    command.Transaction = dbTransaction;
                    SetParametersInCommand(parameters, command);
                    var scalar = command.ExecuteScalar();
                    return scalar == DBNull.Value ? true : Convert.ToInt32(scalar);
                }

                 dbCommand.Transaction = (DbTransaction?)dbTransaction;
                SetParametersInCommand(parameters, dbCommand);
                
                if (dbCommand.Connection?.State != ConnectionState.Open)
                    await dbCommand.Connection!.OpenAsync();

                var result = await dbCommand.ExecuteScalarAsync();
                return result == DBNull.Value ? true : Convert.ToInt32(result);
            });
        }

        /// <summary>
        /// Ejecuta una consulta SQL asíncrona sin resultado (INSERT, UPDATE, DELETE)
        /// </summary>
        public async Task<bool> ExcuteSqlQueryWithOutScalarAsync(string strQuery, IDbConnection dbConnection, IDbTransaction? dbTransaction, List<IDbDataParameter>? parameters = null)
        {
            try
            {
                return await ExecuteWithRetryAsync(async () =>
                {
                    using var command = ComandoSql(strQuery, dbConnection);
                    var dbCommand = command as DbCommand;

                    if (dbCommand == null)
                    {
                        command.Transaction = dbTransaction;
                        SetParametersInCommand(parameters, command);
                        command.ExecuteNonQuery();
                        return true;
                    }

                     dbCommand.Transaction = (DbTransaction?)dbTransaction;
                    SetParametersInCommand(parameters, dbCommand);

                    if (dbCommand.Connection?.State != ConnectionState.Open)
                        await dbCommand.Connection!.OpenAsync();

                    await dbCommand.ExecuteNonQueryAsync();
                    return true;
                }) as bool? ?? false;
            }
            catch (Exception)
            {
                ReStartData();
                return false;
            }
        }

        /// <summary>
        /// Versión asíncrona de TraerDatosSQL. Usa ExecuteReaderAsync + DataTable.LoadAsync
        /// </summary>
        public async Task<DataTable> TraerDatosSQLAsync(string queryString, IDbConnection dbConnection, IDbTransaction? dbTransaction, List<IDbDataParameter>? parameters = null)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                DataTable resultTable = new DataTable();
                using var command = ComandoSql(queryString, dbConnection);
                var dbCommand = command as DbCommand;

                if (dbCommand == null)
                {
                    // Fallback síncrono
                    command.Transaction = dbTransaction;
                    SetParametersInCommand(parameters, command);
                    using var reader = command.ExecuteReader();
                    resultTable.Load(reader);
                    return resultTable;
                }

                 dbCommand.Transaction = (DbTransaction?)dbTransaction;
                SetParametersInCommand(parameters, dbCommand);

                if (dbCommand.Connection?.State != ConnectionState.Open)
                    await dbCommand.Connection!.OpenAsync();

                using var dbReader = await dbCommand.ExecuteReaderAsync();
                resultTable.Load(dbReader);
                return resultTable;
            }) as DataTable ?? new DataTable();
        }

        // ==================== HELPERS ====================

        private void SetParametersInCommand(List<IDbDataParameter>? parameters, IDbCommand command)
        {
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.Add(CloneParameter(param));
                }
            }
        }

        private IDbDataParameter? CloneParameter(IDbDataParameter originalParam)
        {
            IDbDataParameter? newParam = (IDbDataParameter?)Activator.CreateInstance(originalParam.GetType());
            if (newParam == null) return null;

            foreach (var prop in originalParam.GetType().GetProperties())
            {
                if (prop.CanWrite)
                {
                    prop.SetValue(newParam, prop.GetValue(originalParam));
                }
            }
            return newParam;
        }
    }
}