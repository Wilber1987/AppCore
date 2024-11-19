using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using CAPA_DATOS;
using MailKit.Net.Imap;
using System.Net.Http.Headers;
using MailKit.Security;
using MailKit;
using MimeKit;
using MailKit.Search;

namespace CAPA_DATOS.Services;
public class MailConfig
{
    public string? HOST { get; set; }
    public string? USERNAME { get; set; }
    public string? PASSWORD { get; set; }
    public HostServices? HostService { get; set; }
    public AutenticationTypeEnum? AutenticationType { get; set; }
    //AUTH 2.0
    public string? TENAT { get; set; }
    public string? CLIENT { get; set; }
    public string? OBJECTID { get; set; }
    public string? CLIENT_SECRET { get; set; }
}

public enum HostServices
{
    OUTLOOK, GMAIL
}

public enum AutenticationTypeEnum
{
    AUTH2, BASIC
}
public class IMAPServices
{
    const int port = 993;
    private string? tenant_id = "8097a003-1162-40cb-ba74-f198eda4d6e9";
    private string? client_id = "b3161d3c-f437-47b7-aa3b-6a0ed3532f5b";
    private string? client_secret = "RrH8Q~O6hHqDetZWbNOYLQrdRgn.WupFPlSpBatO";
    private string? mail = "wilbermatusgonzalez@wexpdev.onmicrosoft.com";
    private string? password = "outlook.office365.com";
    private string? host = "outlook.office365.com";
    private AutenticationTypeEnum? AutenticationType = AutenticationTypeEnum.AUTH2;



    public HttpClient ApiClient { get; set; } = new HttpClient();
    public void InitializeClient()
    {
        if (ApiClient == null)
            ApiClient = new HttpClient();

        ApiClient.DefaultRequestHeaders.Accept.Clear();
        ApiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
    public ImapClient? GetClient(MailConfig config)
    {
        return null;//new ImapClient(config.HOST, config.USERNAME, config.PASSWORD, AuthMethods.Login, PORT, true);
    }

    public async Task<List<MimeMessage>> GetMessages(MailConfig mailConfig)
    {
        AutenticationType = mailConfig.AutenticationType;
        tenant_id = mailConfig?.TENAT;
        client_id = mailConfig?.CLIENT;
        client_secret = mailConfig?.CLIENT_SECRET;
        mail = mailConfig?.USERNAME;
        host = mailConfig?.HOST;
        password = mailConfig?.PASSWORD;
        List<MimeMessage> messages;
        if (AutenticationType == AutenticationTypeEnum.AUTH2)
        {
            var accessToken = await Auth2Utils.GetAccessTokenAsync(mailConfig);
            messages = await GetNotSeenMessagesAsync(accessToken);
        }
        else
        {
            messages = await GetNotSeenMessagesAsync(null);
        }
        return messages;
    }
    public async Task<List<MimeMessage>> GetAllMessagesAsync(AccessTokenModel accessToken)
    {
        using var client = new ImapClient();

        await IMAPConnectAsync(client, accessToken);

        var messages = new List<MimeMessage>();
        var uids = await client.Inbox.SearchAsync(SearchQuery.All);
        foreach (var uid in uids)
        {
            messages.Add(await client.Inbox.GetMessageAsync(uid));
        }

        await client.DisconnectAsync(true);
        return messages;
    }

    public async Task<List<MimeMessage>> GetNotSeenMessagesAsync(AccessTokenModel accessToken)
    {
        using var client = new ImapClient();
        if (accessToken == null)
        {
            //autenticacion basica user and password
            await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect);
            await client.AuthenticateAsync(mail, password);
        } else{
            //autenticacion con auth2
            await IMAPConnectAsync(client, accessToken);
        }
        
       await client.Inbox.OpenAsync(FolderAccess.ReadOnly);

        var inbox = await client.Inbox.OpenAsync(FolderAccess.ReadWrite);
        var uids = await client.Inbox.SearchAsync(SearchQuery.NotSeen);

        var messages = new List<MimeMessage>();

        var carpetaLeidos = await client.GetFolderAsync("Archivo");
        // Asegurarse de que la carpeta de destino exista
        if (carpetaLeidos == null)
        {
            // Si no existe, puedes crearla
            //carpetaLeidos = await client.Cre("Leidos", true);
        }
        foreach (var uid in uids)
        {
            var message = await client.Inbox.GetMessageAsync(uid);
            messages.Add(message);
            client.Inbox.MoveTo(uid, carpetaLeidos);
        }
        await client.DisconnectAsync(true);
        return messages;
    }

    // public async Task<List<MimeMessage>> GetNotSeenMessages()
    // {
    //     using var client = new ImapClient();

    //     await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect);
    //     // Autenticación con nombre de usuario y contraseña
    //     await client.AuthenticateAsync(mail, password);

    //     await client.Inbox.OpenAsync(FolderAccess.ReadOnly);
    //     var inbox = await client.Inbox.OpenAsync(FolderAccess.ReadWrite);
    //     var uids = await client.Inbox.SearchAsync(SearchQuery.NotSeen);

    //     var messages = new List<MimeMessage>();
    //     var carpetaLeidos = await client.GetFolderAsync("Archivo");
    //     // Asegurarse de que la carpeta de destino exista
    //     if (carpetaLeidos == null)
    //     {
    //         // Si no existe, puedes crearla
    //         //carpetaLeidos = await client.Cre("Leidos", true);
    //     }
    //     foreach (var uid in uids)
    //     {
    //         var message = await client.Inbox.GetMessageAsync(uid);
    //         messages.Add(message);
    //         client.Inbox.MoveTo(uid, carpetaLeidos);
    //     }
    //     await client.DisconnectAsync(true);
    //     return messages;
    // }

    public async Task<MimeMessage> GetFirstUnreadMessageAsync(AccessTokenModel accessToken)
    {
        using var client = new ImapClient();

        await IMAPConnectAsync(client, accessToken);

        int unread_index = client.Inbox.FirstUnread;
        return await client.Inbox.GetMessageAsync(unread_index);

    }

    private async Task IMAPConnectAsync(ImapClient client, AccessTokenModel accessToken, FolderAccess folderAccess = FolderAccess.ReadOnly)
    {
        var oauth2 = new SaslMechanismOAuth2(mail, accessToken.Access_token);

        await client.ConnectAsync(host, port, SecureSocketOptions.Auto);
        await client.AuthenticateAsync(oauth2);

        //await client.Inbox.OpenAsync(folderAccess);
    }
}
public class AccessTokenModel
{
    public string? Access_token { get; set; }
    public string? Token_type { get; set; }
    public int Expires_in { get; set; }
    public int Ext_expires_in { get; set; }
}



