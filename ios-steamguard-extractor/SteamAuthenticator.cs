using Newtonsoft.Json;

namespace ios_steamguard_extractor
{
    public class SteamAuthenticator
    {
        [JsonProperty("shared_secret")]
        public string SharedSecret { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("steamid")]
        public string Steamid { get; set; }

        [JsonProperty("revocation_code")]
        public string RevocationCode { get; set; }

        [JsonProperty("serial_number")]
        public string SerialNumber { get; set; }

        [JsonProperty("token_gid")]
        public string TokenGid { get; set; }

        [JsonProperty("identity_secret")]
        public string IdentitySecret { get; set; }

        [JsonProperty("secret_1")]
        public string Secret { get; set; }

        [JsonProperty("server_time")]
        public string ServerTime { get; set; }

        [JsonProperty("account_name")]
        public string AccountName { get; set; }

        [JsonProperty("steamguard_scheme")]
        public string SteamguardScheme { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        //Added for direct import of json blob to Archi's Steam Farm
        [JsonProperty("device_id")]
        public string DeviceID { get; set; }
    }
}