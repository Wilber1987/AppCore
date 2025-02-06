using iText.Html2pdf;

namespace CAPA_DATOS.Services
{
	public class PdfService
	{		
		
		public static byte[] ConvertHtmlToPdf(string htmlContent, string? pageType)
		{
			using (var memoryStream = new MemoryStream())
			{
				// Configurar el tamaño de la página
				var pageSize = GetPageSize(pageType);

				// Crear un PdfWriter vinculado al MemoryStream
				using (var writer = new iText.Kernel.Pdf.PdfWriter(memoryStream))
				{
					// Inicializar el documento PDF con el tamaño de página configurado
					using (var pdfDocument = new iText.Kernel.Pdf.PdfDocument(writer))
					{
						// Establecer el tamaño de la página
						pdfDocument.SetDefaultPageSize(pageSize);

						// Configurar propiedades de conversión
						ConverterProperties properties = new ConverterProperties();
						properties.SetBaseUri(""); // Manejo de rutas relativas para imágenes base64

						// Convertir el HTML con estilos y encabezados
						HtmlConverter.ConvertToPdf(htmlContent, pdfDocument, properties);
					}
				}

				// Retornar el contenido del MemoryStream como un arreglo de bytes
				return memoryStream.ToArray();
			}
		}
		/*public async Task<byte[]> GeneratePdfWithPlaywright(string htmlContent, string? pageType)
		{
			using var playwright = await Playwright.CreateAsync();
			await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
			var page = await browser.NewPageAsync();
			await page.SetContentAsync(htmlContent);
			var pdfStream = await page.PdfStreamAsync(new PagePdfOptions { Format =  pageType ?? "A4"  });
			using var memoryStream = new MemoryStream();
			await pdfStream.CopyToAsync(memoryStream);
			return memoryStream.ToArray();
		}*/
		private static  iText.Kernel.Geom.PageSize GetPageSize(string? pageType)
		{
			switch (pageType)
			{
				case "A4":
					return iText.Kernel.Geom.PageSize.A4;
				case "A4-horizontal":
					return new  iText.Kernel.Geom.PageSize( iText.Kernel.Geom.PageSize.A4.GetHeight(),  iText.Kernel.Geom.PageSize.A4.GetWidth());
				case "carta":
					return new  iText.Kernel.Geom.PageSize(612, 792); // Carta
				case "carta-horizontal":
					return new  iText.Kernel.Geom.PageSize(612, 792).Rotate(); // Carta Horizontal
				case "oficio":
					return new  iText.Kernel.Geom.PageSize(816, 1056); // Oficio
				case "oficio-horizontal":
					return new  iText.Kernel.Geom.PageSize(816, 1056).Rotate(); // Oficio Horizontal
				default:
					return  iText.Kernel.Geom.PageSize.A4; // Default to A4 if no valid type is provided
			}
		}
	}
}
