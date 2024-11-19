using CAPA_DATOS.BDCore.Abstracts;
using CAPA_DATOS.BDCore.MySqlImplementations;
using CAPA_DATOS.MySqlImplementations;
using MySql.Data.MySqlClient;
using Renci.SshNet;




namespace CAPA_DATOS;

public class MySQLConnection
{
    public static WDataMapper? SQLM;

    static public bool IniciarConexion(string SGBD_USER, string SWGBD_PASSWORD, string SQLServer, string BDNAME, int PORT)
    {
        try
        {
            return createConexion(SQLServer, SGBD_USER, SWGBD_PASSWORD, BDNAME, PORT);
        }
        catch (Exception)
        {
            SQLM = null;
            return false;
            throw;
        }
    }
    private static bool createConexion(string SGBD_USER, string SWGBD_PASSWORD, string MySQLServer, string BDNAME, int Port = 3306)
    {
        string userSQLConexion = $"Server={MySQLServer};Port={Port};User ID={SGBD_USER};Password={SWGBD_PASSWORD};Database={BDNAME};";
        SQLM = new WDataMapper(new MySqlGDatos(userSQLConexion), new MySQLQueryBuilder());
        SQLM.GDatos.Database = BDNAME;
        if (SQLM.GDatos.TestConnection())
        {
            return true;
        }
        else
        {
            SQLM = null;
            return false;
        }
    }

    public static WDataMapper? BuildDataMapper(string MySQLServer, string SGBD_USER, string SWGBD_PASSWORD, string BDNAME, int Port = 3306)
    {
        string userSQLConexion = $"Server={MySQLServer};Port={Port};User ID={SGBD_USER};Password={SWGBD_PASSWORD};Database={BDNAME};SslMode=None;";
        WDataMapper mapper = new WDataMapper(new MySqlGDatos(userSQLConexion), new MySQLQueryBuilder());
        mapper.GDatos.Database = BDNAME;
        if (SQLM?.GDatos.TestConnection() == false)
        {
            return null;
        }
        return mapper;
    }
   
}


