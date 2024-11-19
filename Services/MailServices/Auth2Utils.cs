namespace CAPA_DATOS.Services;
public class Auth2Utils
{
    /*Client id
8033c947-1302-4264-9e65-31d65a7312bd
8033c947-1302-4264-9e65-31d65a7312bd

Directory
5acad0c6-2f8d-4d05-bd1e-692eb87e707b

Object ID 
e60440ba-42c0-444d-ba9f-2ee362e6b880

Value
8zX8Q~6Qo2cX9uE60OQMMbaAuXpD_SX9c6OoKatl

Secret ID
ffdde047-f25e-4029-a01b-0aafead055ab*/
    public static async Task<AccessTokenModel> GetAccessTokenAsync(MailConfig mailConfig)
    {
        string url = $"https://login.microsoftonline.com/{mailConfig.TENAT}/oauth2/v2.0/token";
        var data = new Dictionary<string, string>
            {
                {"grant_type", "client_credentials"},
                {"scope", "https://outlook.office365.com/.default"},
                {"client_id",  mailConfig.CLIENT},
                {"client_secret", mailConfig.CLIENT_SECRET}
            };
        using HttpClient client = new();
        var response = await client.PostAsync(url, new FormUrlEncodedContent(data));
        return await response.Content.ReadAsAsync<AccessTokenModel>();
    }
}