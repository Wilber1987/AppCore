using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace APPCORE.Services
{
    public class SessionServices
    {
        public static List<SessionData> SessionDatas = new List<SessionData>();
        public static void Set(string key, Object value, string sessionKey)
        {
            var find = SessionDatas.Find(x => x.KeyName!.Equals(key) && x.idetify!.Equals(sessionKey));
            if (find != null)
            {
                SessionDatas.Remove(find);
            }
            SessionDatas.Add(new SessionData()
            {
                KeyName = key,
                Value = System.Text.Json.JsonSerializer.Serialize(value),
                created = DateTime.Now,
                idetify = sessionKey
            });
        }

        public static T? Get<T>(string key, string seasonKey)
        {
            var find = SessionDatas.Find(x => x.KeyName!.Equals(key) && x.idetify!.Equals(seasonKey));
            return find == null ? default : JsonConvert.DeserializeObject<T>(find.Value ?? "{}");
                //System.Text.Json.JsonSerializer.Deserialize<T>(find.Value);
        }

        public static void ClearSeason(string seasonKey)
        {
            SessionDatas.RemoveAll(x => x.idetify!.Equals(seasonKey));
        }
    }

}