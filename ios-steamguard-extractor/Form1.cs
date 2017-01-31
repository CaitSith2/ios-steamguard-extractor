using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Claunia.PropertyList;
using Newtonsoft.Json;

namespace ios_steamguard_extractor
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        static int SearchBytes(byte[] haystack, byte[] needle, int start)
        {
            var len = needle.Length;
            var limit = haystack.Length - len;
            for (var i = start; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k]) break;
                }
                if (k == len) return i;
            }
            return -1;
        }

        private void btnGetSteamGuardData_Click(object sender, EventArgs e)
        {
            var iosBackups = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "Apple Computer", "MobileSync", "Backup");
            if (!Directory.Exists(iosBackups))
            {
                txtResults.Text = @"No ios backups found";
                return;
            }
            foreach (var d in Directory.GetDirectories(iosBackups))
            {
                var name = new DirectoryInfo(d).Name;
                if (!File.Exists(Path.Combine(d, "Manifest.mbdb")))
                {
                    txtResults.AppendText($"Directory {name} is not a valid ios backup, skipping" + Environment.NewLine + Environment.NewLine);
                    continue;
                }
                txtResults.AppendText($"Processing Directory {name}" + Environment.NewLine);
                var data = File.ReadAllBytes(Path.Combine(d, "Manifest.mbdb"));
                var steamfiles = Encoding.UTF8.GetBytes("AppDomain-com.valvesoftware.Steam");
                for (var index = 0; ; index += steamfiles.Length)
                {
                    index = SearchBytes(data, steamfiles, index);
                    if (index == -1) break;
                    var index2 = index + steamfiles.Length;
                    var filelen = data[index2] << 8 | data[index2 + 1];
                    var temp = new byte[filelen];
                    Array.Copy(data, index2 + 2, temp, 0, filelen);
                    var steamfilename = Encoding.UTF8.GetString(temp);
                    if (!steamfilename.StartsWith("Documents/Steamguard-")) continue;
                    var hash =
                        new SHA1Managed().ComputeHash(
                            Encoding.UTF8.GetBytes("AppDomain-com.valvesoftware.Steam-" + steamfilename));
                    var hashstr = BitConverter.ToString(hash).Replace("-", "");
                    if (File.Exists(Path.Combine(d, hashstr)))
                    {
                        txtResults.AppendText($"Processing {steamfilename}" + Environment.NewLine);
                        try
                        {
                            var sgdata = BinaryPropertyListParser.Parse(File.ReadAllBytes(Path.Combine(d, hashstr)));
                            var sglist = ((NSArray) ((NSDictionary) sgdata)["$objects"]).GetArray();
                            var auth = new SteamAuthenticator()
                            {
                                SharedSecret = sglist[14].ToString(),
                                Uri = sglist[15].ToString(),
                                Steamid = sglist[16].ToString(),
                                RevocationCode = sglist[17].ToString(),
                                SerialNumber = sglist[18].ToString(),
                                TokenGid = sglist[19].ToString(),
                                IdentitySecret = sglist[20].ToString(),
                                Secret = sglist[21].ToString(),
                                ServerTime = sglist[22].ToString(),
                                AccountName = sglist[23].ToString(),
                                SteamguardScheme = sglist[24].ToString(),
                                Status = sglist[25].ToString()
                            };
                            txtResults.AppendText(Environment.NewLine);

                            txtResults.AppendText("In WinAuth, Add Steam Authenticator. Select the Import Android Tab" +
                                                  Environment.NewLine + Environment.NewLine);

                            txtResults.AppendText("Paste this into the steam_uuid.xml text box" + Environment.NewLine);
                            txtResults.AppendText($"android:{name}" + Environment.NewLine + Environment.NewLine);

                            txtResults.AppendText(
                                "Paste the following data, including the {} into the SteamGuare-NNNNNNNNN... text box" +
                                Environment.NewLine);

                            txtResults.AppendText(JsonConvert.SerializeObject(auth, Formatting.Indented) +
                                                  Environment.NewLine + Environment.NewLine);
                        }
                        catch (PropertyListFormatException) //The only way this should happen is if we opened an encrypted backup.
                        {
                            txtResults.AppendText("Error: Encrypted backups are not supported. You need to create a decrypted backup to proceed." + Environment.NewLine);
                            break;
                        }
                        catch (Exception ex)
                        {
                            txtResults.AppendText($"An Exception occurred while processing: {ex.Message}");
                        }

                    }
                    else
                    {
                        txtResults.AppendText($"Error: {steamfilename} is missing from ios backup, aborting" + Environment.NewLine);
                        break;
                    }
                }

                txtResults.AppendText("Done" + Environment.NewLine + Environment.NewLine);
            }
        }
    }

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
}
}
