using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;

namespace CAPA_DATOS.Services
{
    public class SeasonServices
    {
        public static List<SeassonData> SeassonDatas = new List<SeassonData>();
        public static void Set(string key, Object value, string seassonKey)
        {
            var find = SeassonDatas.Find(x => x.KeyName.Equals(key) && x.idetify.Equals(seassonKey));
            if (find != null)
            {
                SeassonDatas.Remove(find);
            }
            SeassonDatas.Add(new SeassonData()
            {
                KeyName = key,
                Value = System.Text.Json.JsonSerializer.Serialize(value),
                created = DateTime.Now,
                idetify = seassonKey
            });
        }

        public static T? Get<T>(string key, string seasonKey)
        {
            var find = SeassonDatas.Find(x => x.KeyName.Equals(key) && x.idetify.Equals(seasonKey));
            return find == null ? default : JsonConvert.DeserializeObject<T>(find.Value);
                //System.Text.Json.JsonSerializer.Deserialize<T>(find.Value);
        }

        public static void ClearSeason(string seasonKey)
        {
            SeassonDatas.RemoveAll(x => x.idetify.Equals(seasonKey));
        }
    }

}