using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.IO;

using TShockAPI;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace CIDRbans
{
    /// <summary>CIDR Ban Manager</summary>
    public class CIDRBanManager
    {
        /// <summary> The IDbConnection object </summary>
        private IDbConnection db;
        /// <summary>Constructor for CIDRBanManager class</summary>
        public CIDRBanManager()
        {
            //Initialize connection to either SQLite or MySQL database
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                        host[0],
                        host.Length == 1 ? "3306" : host[1],
                        TShock.Config.MySqlDbName,
                        TShock.Config.MySqlUsername,
                        TShock.Config.MySqlPassword)
                    };
                    break;
                case "sqlite":
                    string dbPath = Path.Combine(TShock.SavePath, "CIDRBans.sqlite");
                    db = new SqliteConnection(String.Format("uri=file://{0},Version=3", dbPath));
                    break;
            }
            SqlTableCreator creator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? 
                (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
                creator.EnsureTableStructure(new SqlTable("CIDRBans",
                    new SqlColumn("CIDR", MySqlDbType.String) { Primary = true },
                    new SqlColumn("Reason", MySqlDbType.Text),
                    new SqlColumn("BanningUser", MySqlDbType.Text),
                    new SqlColumn("Date", MySqlDbType.Text),
                    new SqlColumn("Expiration", MySqlDbType.Text)));
        }

        /// <summary>Search CIDR bans with given IP</summary>
        /// <param name="IP">IP string for searching</param>
        /// <returns>CIDRBan object</returns>
        public CIDRBan GetCIDRBanByIP(string check)
        {
            List<CIDRBan> banlist = new List<CIDRBan>();
            try
            {
                using (var reader = db.QueryReader("SELECT * FROM CIDRBans"))
                {
                    while (reader.Read())
                        banlist.Add(new CIDRBan(reader.Get<string>("CIDR"), reader.Get<string>("Reason"),
                            reader.Get<string>("BanningUser"), reader.Get<string>("Date"), reader.Get<string>("Expiration")));
                    //Check if the IP matches any of range in CIDR Ban List
                    foreach (CIDRBan ban in banlist)
                    {
                        if (CIDRBan.Check(check, ban.CIDR))
                            return ban;
                        //If not, continue to next CIDR ban in range
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error(ex.ToString());
            }
            return null;
        }

        /// <summary>Search for all CIDR ranges banned</summary>
        /// <returns>List of CIDRBan objects</returns>
        public List<CIDRBan> GetCIDRBanList()
        {
            List<CIDRBan> banlist = new List<CIDRBan>();
            try
            {
                using (var reader = db.QueryReader("SELECT * FROM CIDRBans"))
                {
                    while (reader.Read())
                        banlist.Add(new CIDRBan(reader.Get<string>("CIDR"), reader.Get<string>("Reason"),
                            reader.Get<string>("BanningUser"), reader.Get<string>("Date"), reader.Get<string>("Expiration")));
                    return banlist;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error(ex.ToString());
            }
            return new List<CIDRBan>();
        }

        /// <summary>Add a CIDR range to ban list</summary>
        /// <param name="cidr">CIDR Range</param>
        /// <param name="reason">Ban Reason</param>
        /// <param name="user">Banning User</param>
        /// <param name="date">Date of banning</param>
        /// <param name="expire">Expiration Period</param>
        /// <returns>Success</returns>
        public bool AddCIDRBan(string cidr, string reason = "", string user = "", string date = "", string expire = "")
        {
            try
            {
                return db.Query("INSERT INTO CIDRBans (CIDR, Reason, BanningUser, Date, Expiration) " +
                    "VALUES (@0, @1, @2, @3, @4)", cidr, reason, user, date, expire) != 0;
            }
            catch (Exception ex)
            {
                TShock.Log.Error(ex.ToString());
            }
            return false;
        }

        /// <summary>Delete specified CIDR range</summary>
        /// <param name="cidr">CIDR Range</param>
        /// <returns>Success</returns>
        public bool DelCIDRBanByRange(string cidr)
        {
            try
            {
                return db.Query("DELETE FROM CIDRBans WHERE CIDR = @0", cidr) != 0;
            }
            catch (Exception ex)
            {
                TShock.Log.Error(ex.ToString());
            }
            return false;
        }

        /// <summary>Delete all CIDR ranges matched up with an IP</summary>
        /// <param name="ip">Specified IP</param>
        /// <returns>List of CIDR range strings</returns>
        public List<string> DelCIDRBanByIP(string ip)
        {
            List<CIDRBan> banlist = new List<CIDRBan>();
            List<string> removelist = new List<string>();
            try
            {
                using (var reader = db.QueryReader("SELECT * FROM CIDRBans"))
                {
                    while (reader.Read())
                        banlist.Add(new CIDRBan(reader.Get<string>("CIDR"), reader.Get<string>("Reason"),
                            reader.Get<string>("BanningUser"), reader.Get<string>("Date"), reader.Get<string>("Expiration")));
                    //Check if the IP matches any of range in CIDR Ban List
                    foreach (CIDRBan ban in banlist)
                    {
                        if (CIDRBan.Check(ip, ban.CIDR))
                            removelist.Add(ban.CIDR);
                        //If true, add it to list for removal preparation
                        //Loop until every CIDR range in list is checked
                    }
                }
                //Start removing everything in prepared list
                foreach (string removed in removelist)
                    db.Query("DELETE FROM CIDRBans WHERE CIDR = @0", removed);
                //Inform players
                return removelist;
            }
            catch (Exception ex)
            {
                TShock.Log.Error(ex.ToString());
            }
            return new List<string>();
        }
    }

    /// <summary>CIDR Ban Information</summary>
    public class CIDRBan
    {
        /// <summary>CIDR Range</summary>
        public string CIDR { get; set; }
        /// <summary>Ban Reason</summary>
        public string Reason { get; set; }
        /// <summary>Banning User</summary>
        public string BanningUser { get; set; }
        /// <summary>Date of banning</summary>
        public string Date { get; set; }
        /// <summary>Date of ban expiration</summary>
        public string Expiration { get; set; }

        /// <summary>Constructor for creating CIDRBan class</summary>
        /// <param name="cidr">CIDR Range</param>
        /// <param name="reason">Ban Reason</param>
        /// <param name="user">Banning User</param>
        /// <param name="date">Date of banning</param>
        /// <param name="expire">Expiration Period</param>
        public CIDRBan(string cidr, string reason, string user, string date, string expire)
        {
            this.CIDR = cidr;
            this.Reason = reason;
            this.BanningUser = user;
            this.Date = date;
            this.Expiration = expire;
        }

        /// <summary>Blank constructor for CIDRBan class</summary>
        public CIDRBan()
        {
            this.CIDR = "";
            this.Reason = "";
            this.BanningUser = "";
            this.Date = "";
            this.Expiration = "";
        }

        /// <summary>
        /// Check if IP is included in CIDR Range or not
        /// </summary>
        /// <param name="check">IP to check</param>
        /// <param name="cidr">CIDR Range</param>
        /// <returns>Match Success</returns>
        public static bool Check(string check, string cidr)
        {
            const uint defaultmask = 0xffffffff;
            int maskcount = Convert.ToInt32(cidr.Split('/')[1]);
            //mask = 32-bit mask for using & operator
            uint mask = Convert.ToUInt32(defaultmask << (32 - maskcount));
            //cidrbyteparts = List with 4 byte objects containing each part of CIDR IP
            List<byte> cidrbyteparts = (from num in cidr.Split('/')[0].Split('.')
                                        select Convert.ToByte(num)).ToList();
            //cidrip = Translate CIDR IP to a number ranging from 0 to 2^32
            uint cidrip = (((uint)cidrbyteparts[0] * (uint)Math.Pow(2, 24)) + ((uint)cidrbyteparts[1] * (uint)Math.Pow(2, 16)) +
                ((uint)cidrbyteparts[2] * (uint)Math.Pow(2, 8)) + (uint)cidrbyteparts[3]);
            //Cut the part which is not meant to be the same out, for checking
            cidrip &= mask;
            //checkbyteparts = List with 4 byte objects containing each part of check IP
            List<byte> checkbyteparts = (from num in check.Split('.')
                                         select Convert.ToByte(num)).ToList();
            //checkip = Translate check IP to a number ranging from 0 to 2^32
            uint checkip = (((uint)checkbyteparts[0] * (uint)Math.Pow(2, 24)) + ((uint)checkbyteparts[1] * (uint)Math.Pow(2, 16)) +
                ((uint)checkbyteparts[2] * (uint)Math.Pow(2, 8)) + (uint)checkbyteparts[3]);
            //Cut the part which is not meant to be the same out, for checking
            checkip &= mask;
            //Check the front part which is meant to be the same if it is same or not
            if (cidrip == checkip)
                return true;
            return false;
        }
    }
}
