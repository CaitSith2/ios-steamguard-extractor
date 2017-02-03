using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

        private bool ProcessSteamGuardFile(string filename, string filepath, string deviceID)
        {
            txtResults.AppendText($"Processing {filename}" + Environment.NewLine);
            try
            {
                var sgdata = BinaryPropertyListParser.Parse(File.ReadAllBytes(filepath));
                var sglist = ((NSArray)((NSDictionary)sgdata)["$objects"]).GetArray();
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
                    Status = sglist[25].ToString(),
                    DeviceID = $"android:{deviceID}"
                };
                txtResults.AppendText(Environment.NewLine);

                txtResults.AppendText("In WinAuth, Add Steam Authenticator. Select the Import Android Tab" +
                                      Environment.NewLine + Environment.NewLine);

                txtResults.AppendText("Paste this into the steam_uuid.xml text box" + Environment.NewLine);
                txtResults.AppendText($"android:{deviceID}" + Environment.NewLine + Environment.NewLine);

                txtResults.AppendText(
                    "Paste the following data, including the {} into the SteamGuare-NNNNNNNNN... text box" +
                    Environment.NewLine);

                txtResults.AppendText(JsonConvert.SerializeObject(auth, Formatting.Indented) +
                                      Environment.NewLine + Environment.NewLine);

                txtResults.AppendText(
                    "Alternatively, you can paste the above json text into {botname}.maFile in your ASF config directory, if you use ASF"
                    + Environment.NewLine + Environment.NewLine);

            }
            catch (PropertyListFormatException) //The only way this should happen is if we opened an encrypted backup.
            {
                txtResults.AppendText("Error: Encrypted backups are not supported. You need to create a decrypted backup to proceed." + Environment.NewLine);
                return false;
            }
            catch (Exception ex)
            {
                txtResults.AppendText($"An Exception occurred while processing: {ex.Message}");
                return false;
            }
            return true;
        }

        private void ProcessIOS9Backup(string d)
        {
            var guid = ProcessInfoPlist(d);
            if (guid == null) return;
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
                    if (!ProcessSteamGuardFile(steamfilename, Path.Combine(d, hashstr), guid))
                        break;
                }
                else
                {
                    txtResults.AppendText($"Error: {steamfilename} is missing from ios backup, aborting" + Environment.NewLine);
                    break;
                }
            }
        }

        private void ProcessIOS10Backup(string d)
        {
            try
            {
                var guid = ProcessInfoPlist(d);
                if (guid == null) return;
                var dbConnection = new SQLiteConnection($"Data Source=\"{Path.Combine(d, "Manifest.db")}\";Version=3;");
                dbConnection.Open();
                var query =
                    "Select * from Files where domain is 'AppDomain-com.valvesoftware.Steam' and relativePath like 'Documents/Steamguard-%'";
                var dbCommand = new SQLiteCommand(query, dbConnection);
                var dbReader = dbCommand.ExecuteReader();
                while (dbReader.Read())
                {
                    var startID = dbReader["fileID"].ToString().Substring(0, 2);
                    var result = ProcessSteamGuardFile(dbReader["relativePath"].ToString(),
                        Path.Combine(d, startID, dbReader["fileID"].ToString()), guid);
                    if (!result) break;
                }
                dbConnection.Close();
            }
            catch (SQLiteException)
            {
                txtResults.AppendText("Error: Encrypted backups are not supported. You need to create a decrypted backup to proceed." + Environment.NewLine);
            }
            catch (Exception ex)
            {
                txtResults.AppendText($"An Exception occurred while processing: {ex.Message}");
            }
        }

        private string ProcessInfoPlist(string d)
        {
            try
            {
                var info = (NSDictionary) PropertyListParser.Parse(Path.Combine(d, "Info.plist"));
                txtResults.AppendText($"Processing backup: {info["Device Name"]} version {info["Product Version"]}" + Environment.NewLine);
                return info["Unique Identifier"].ToString();
            }
            catch (Exception ex)
            {
                txtResults.AppendText($"An Exception occurred while processing: {ex.Message}");
                return null;
            }
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
                if (File.Exists(Path.Combine(d, "Manifest.mbdb")))
                    ProcessIOS9Backup(d);
                else if (File.Exists(Path.Combine(d, "Manifest.db")))
                    ProcessIOS10Backup(d);
                else
                {
                    txtResults.AppendText($"Directory {name} is not in a recognized backup format." + Environment.NewLine +
                        "Listing contents of this directory.  Please open an issue and paste this listing as well as the Version of ios and itunes you are using." + Environment.NewLine + Environment.NewLine);
                    var count = 0;
                    foreach (var f in Directory.GetFiles(d))
                    {
                        var filename = Path.GetFileName(f).ToLower();

                        if (filename.Length == 40)
                        {
                            var chars = new []
                            {"0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b", "c", "d", "e", "f"};
                            filename = chars.Aggregate(filename, (current, c) => current.Replace(c, ""));
                            if (filename.Length == 0)
                            {
                                count++;
                                continue;
                            }
                        }
                        filename = Path.GetFileName(f);
                        txtResults.AppendText($"{filename}" + Environment.NewLine);
                    }
                    txtResults.AppendText(Environment.NewLine + $"Done listing files - Skipped {count} files" + Environment.NewLine +
                                          Environment.NewLine);
                    continue;
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

        [JsonProperty("device_id")]
        public string DeviceID { get; set; }
}
}
