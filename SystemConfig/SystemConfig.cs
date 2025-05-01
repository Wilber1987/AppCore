using APPCORE.Services;
using Microsoft.Extensions.Configuration;
namespace APPCORE.SystemConfig
{
	public class SystemConfig
	{
		public SystemConfig()
		{
			configuraciones = new Transactional_Configuraciones().Get<Transactional_Configuraciones>();
			TITULO = configuraciones.Find(c => c.Nombre != null &&
				c.Nombre.Equals(ConfiguracionesThemeEnum.TITULO.ToString()))?.Valor ?? TITULO;
			SUB_TITULO = configuraciones.Find(c => c.Nombre != null &&
				c.Nombre.Equals(ConfiguracionesThemeEnum.SUB_TITULO.ToString()))?.Valor ?? SUB_TITULO;
			NOMBRE_EMPRESA = configuraciones.Find(c => c.Nombre != null &&
				c.Nombre.Equals(ConfiguracionesThemeEnum.NOMBRE_EMPRESA.ToString()))?.Valor ?? NOMBRE_EMPRESA;
			LOGO_PRINCIPAL = configuraciones.Find(c => c.Nombre != null &&
				c.Nombre.Equals(ConfiguracionesThemeEnum.LOGO_PRINCIPAL.ToString()))?.Valor ?? LOGO_PRINCIPAL;
			MEDIA_IMG_PATH = configuraciones.Find(c => c.Nombre != null &&
				c.Nombre.Equals(ConfiguracionesThemeEnum.MEDIA_IMG_PATH.ToString()))?.Valor ?? MEDIA_IMG_PATH;
			VERSION = configuraciones.Find(c => c.Nombre != null &&
				c.Nombre.Equals(ConfiguracionesThemeEnum.VERSION.ToString()))?.Valor ?? VERSION;
			MEMBRETE_HEADER = configuraciones.Find(c => c.Nombre != null &&
				c.Nombre.Equals(ConfiguracionesThemeEnum.MEMBRETE_HEADER.ToString()))?.Valor ?? VERSION;
			MEMBRETE_FOOTHER = configuraciones.Find(c => c.Nombre != null &&
				c.Nombre.Equals(ConfiguracionesThemeEnum.MEMBRETE_FOOTHER.ToString()))?.Valor ?? VERSION;

		}
		public string TITULO = "TEMPLATE";
		public string SUB_TITULO = "Template";
		public string NOMBRE_EMPRESA = "TEMPLATE";
		public string LOGO_PRINCIPAL = "logo.png";
		public string MEDIA_IMG_PATH = "/media/img/";
		public string VERSION = "2024.07";
		public string MEMBRETE_HEADER = "";
		public string MEMBRETE_FOOTHER = "";
		public List<Transactional_Configuraciones> configuraciones = new List<Transactional_Configuraciones>();

		public static bool IsAutomaticCaseActive()
		{
			//TODO IMPLEMENTAR ESTE METODO
			return true;
		}
		public static bool IsNotificationsActive()
		{
			//TODO IMPLEMENTAR ESTE METODO
			return true;
		}
		public static bool IsMessagesActive()
		{
			//TODO IMPLEMENTAR ESTE METODO
			return false;
		}
		public static bool IsWhatsAppActive()
		{
			//TODO IMPLEMENTAR ESTE METODO
			return true;
		}
		public static bool IsQuestionnairesActive()
		{
			//TODO IMPLEMENTAR ESTE METODO
			return false;
		}
		public static IConfigurationRoot AppConfiguration()
		{
			return new ConfigurationBuilder()
					.SetBasePath(Directory.GetCurrentDirectory())
					.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
					.Build();
		}
		public static IConfigurationSection AppConfigurationSection(string sectionName)
		{
			return new ConfigurationBuilder()
					.SetBasePath(Directory.GetCurrentDirectory())
					.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
					.Build().GetSection(sectionName);
		}
		public static string? AppConfigurationValue(AppConfigurationList sectionName, string value)
		{
			return new ConfigurationBuilder()
					.SetBasePath(Directory.GetCurrentDirectory())
					.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
					.Build().GetSection(sectionName.ToString())[value];
		}

		internal static MailConfig? GetSMTPDefaultConfig()
		{
			string? domain = AppConfigurationValue(AppConfigurationList.Smtp, "Domain");
			string? user = AppConfigurationValue(AppConfigurationList.Smtp, "User");
			string? password = AppConfigurationValue(AppConfigurationList.Smtp, "Password");
			string? port = AppConfigurationValue(AppConfigurationList.Smtp, "Port");
			return new MailConfig()
			{
				HOST = domain,
				PASSWORD = password,
				USERNAME = user,
				PORT = port != null ? Convert.ToInt32(port) : null,
				AutenticationType = AutenticationTypeEnum.BASIC
			};
		}
	}


}
