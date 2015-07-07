using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace CIDRbans
{
    [ApiVersion(1, 18)]
    public class CIDRBansPlugin : TerrariaPlugin
    {
        /// <summary>Plugin Name string</summary>
        public override string Name { get { return "CIDR Bans"; } }
        /// <summary>Plugin Description string</summary>
        public override string Description { get { return "Allows banning CIDR ranges"; } }
        /// <summary>Plugin Author string</summary>
        public override string Author { get { return "AquaBlitz11"; } }
        /// <summary>Plugin Version object</summary>
        public override Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        /// <summary>Plugin Constructor</summary>
        public CIDRBansPlugin(Main game) : base(game) { }

        /// <summary>CIDRBanManager object</summary>
        private CIDRBanManager cidrbans;
        /// <summary>CIDR Range Regular Expression string</summary>
        private const string rangeregex = @"^(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])(\.(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])){3}\/(3[0-2]|[1-2]?[0-9])$";
        /// <summary>IP Regular Expression string</summary>
        private const string ipregex = @"^(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])(\.(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])){3}$";

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        }

        protected override void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
            }
            base.Dispose(Disposing);
        }

        /// <summary>Fired when server initalizes Terraria</summary>
        /// <param name="args">The EventArgs object</param>
        public void OnInitialize(EventArgs args)
        {
            //Initialize database
            cidrbans = new CIDRBanManager();
            //Adds CIDRBan command
            Commands.ChatCommands.Add(new Command("cidrbans.use", CIDRBanCommand, "cidrban")
            {
                HelpDesc = new[]
                {
                    "{0}cidrban add <range> [reason] - Ban a CIDR range permanently.".SFormat(Commands.Specifier),
                    "{0}cidrban addtemp <range> <time> [reason] - Ban a CIDR range temporarily.".SFormat(Commands.Specifier),
                    "{0}cidrban del <ip/range> - Unban CIDR range or ranges that includes specified IP.".SFormat(Commands.Specifier),
                    "{0}cidrban list - List all CIDR ranges banned in the system.".SFormat(Commands.Specifier)
                }
            });
        }

        /// <summary>Fired when a player joins the server</summary>
        /// <param name="args">The JoinEventArgs object</param>
        public void OnJoin(JoinEventArgs args)
        {
            //If player was disconnected by another component, skip
            if (args.Handled)
                return;
            //If IP banning isn't used, skip
            if (!TShock.Config.EnableIPBans)
                return;
            //Creates TSPlayer player object for easy access
            TSPlayer player = TShock.Players[args.Who];
            //Searches a ban by player's IP
            CIDRBan ban = cidrbans.GetCIDRBanByIP(player.IP);
            //If no ban is found or ban has expired, skip
            if (ban == null)
                return;
            //DateTime exp object
            DateTime exp;
            //If player's ban has no expiration date, say ban forever
            if (!DateTime.TryParse(ban.Expiration, out exp))
                player.Disconnect("You are banned forever: " + ban.Reason);
            //If player's ban is temporary, check if it has expired or not
            else
            {
                //If player's ban has expired, remove the ban
                if (DateTime.UtcNow >= exp)
                {
                    cidrbans.DelCIDRBanByRange(ban.CIDR);
                    return;
                }
                //Finds time left from now to expiration date
                TimeSpan ts = exp - DateTime.UtcNow;
                //Makes 30 days count shows as one month
                int months = ts.Days / 30;
                //Converts timespan object into time string to inform player
                if (months > 0)
                {
                    player.Disconnect(String.Format("You are banned for {0} month{1} and {2} day{3}: {4}",
                        months, months == 1 ? "" : "s", ts.Days, ts.Days == 1 ? "" : "s", ban.Reason));
                }
                else if (ts.Days > 0)
                {
                    player.Disconnect(String.Format("You are banned for {0} day{1} and {2} hour{3}: {4}",
                        ts.Days, ts.Days == 1 ? "" : "s", ts.Hours, ts.Hours == 1 ? "" : "s", ban.Reason));
                }
                else if (ts.Hours > 0)
                {
                    player.Disconnect(String.Format("You are banned for {0} hour{1} and {2} minute{3}: {4}",
                        ts.Hours, ts.Hours == 1 ? "" : "s", ts.Minutes, ts.Minutes == 1 ? "" : "s", ban.Reason));
                }
                else if (ts.Minutes > 0)
                {
                    player.Disconnect(String.Format("You are banned for {0} minute{1} and {2} second{3}: {4}",
                        ts.Minutes, ts.Minutes == 1 ? "" : "s", ts.Seconds, ts.Seconds == 1 ? "" : "s", ban.Reason));
                }
                else
                {
                    player.Disconnect(String.Format("You are banned for {0} second{1}: {2}",
                        ts.Seconds, ts.Seconds == 1 ? "" : "s", ban.Reason));
                }
            }
            //Sets player's connection handled
            args.Handled = true;
        }

        /// <summary>Fired when CIDR Ban command is used</summary>
        /// <param name="args">CommandArgs object</param>
        private void CIDRBanCommand(CommandArgs args)
        {
            //Creates TSPlayer player object for easy access
            TSPlayer player = args.Player;
            //Creates subcmd as shortcut for sub-command used (args.Parameters[0])
            string subcmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0].ToLower();
            //Checks which sub-command is used
            switch (subcmd)
            {
                case "help":
                    //Displays CIDR Bans Plugin's information
                    player.SendInfoMessage("CIDR Bans Plugin");
                    player.SendInfoMessage("Description: Allows banning CIDR ranges");
                    player.SendInfoMessage("Syntax: {0}cidrban <add/addtemp/del/list> [arguments]", Commands.Specifier);
                    player.SendInfoMessage("Type {0}help cidrban for more info.", Commands.Specifier);
                    break;
                case "add":
                    //Checks if player has put in CIDR range
                    if (args.Parameters.Count < 2)
                    {
                        player.SendErrorMessage("Invalid syntax! Proper syntax: {0}cidrban add <range> [reason]", Commands.Specifier);
                        return;
                    }
                    //Checks if player's CIDR range input is correct
                    if (!Regex.IsMatch(args.Parameters[1], rangeregex))
                    {
                        player.SendErrorMessage("Invalid CIDR range string! Proper format: 0-255.0-255.0-255.0-255/0-32");
                        return;
                    }
                    //If player hasn't specified reason, set default reason
                    if (args.Parameters.Count < 3)
                        args.Parameters.Add("Manually added IP address ban.");
                    //For reason with spaces without use of quotation marks, put into one string
                    args.Parameters[2] = String.Join(" ", args.Parameters.GetRange(2, args.Parameters.Count - 2));
                    //Add CIDR range into database
                    if (cidrbans.AddCIDRBan(args.Parameters[1], args.Parameters[2], player.Name, DateTime.UtcNow.ToString("s")))
                        player.SendSuccessMessage("Banned range {0} for '{1}'.", args.Parameters[1], args.Parameters[2]);
                    //If it fails, inform player
                    else
                        player.SendErrorMessage("Adding range {0} into database failed.", args.Parameters[1]);
                    break;
                case "addtemp":
                    //Checks if player has put in CIDR range and time
                    if (args.Parameters.Count < 3)
                    {
                        player.SendErrorMessage("Invalid syntax! Proper syntax: {0}cidrban addtemp <range> <time> [reason]", Commands.Specifier);
                        return;
                    }
                    //Checks if player's CIDR range input is correct
                    if (!Regex.IsMatch(args.Parameters[1], rangeregex))
                    {
                        player.SendErrorMessage("Invalid CIDR range string! Proper format: 0-255.0-255.0-255.0-255/0-32");
                        return;
                    }
                    //Creates int exp object to store expiration time in seconds
                    int exp;
                    //Parse input argument into exp object
                    if (!TShock.Utils.TryParseTime(args.Parameters[2], out exp))
                    {
                        //If fails, inform player
                        args.Player.SendErrorMessage("Invalid time string! Proper format: _d_h_m_s, with at least one time specifier.");
                        args.Player.SendErrorMessage("For example, 1d and 10h-30m+2m are both valid time strings, but 2 is not.");
                        return;
                    }
                    //If player hasn't specified reason, set default reason
                    if (args.Parameters.Count < 4)
                        args.Parameters.Add("Manually added IP address ban.");
                    //For reason with spaces without use of quotation marks, put into one string
                    args.Parameters[3] = String.Join(" ", args.Parameters.GetRange(3, args.Parameters.Count - 3));
                    //Add CIDR range into database
                    if (cidrbans.AddCIDRBan(args.Parameters[1], args.Parameters[3], player.Name,
                        DateTime.UtcNow.ToString("s"), DateTime.UtcNow.AddSeconds(exp).ToString("s")))
                        player.SendSuccessMessage("Banned range {0} for '{1}'.", args.Parameters[1], args.Parameters[3]);
                    //If it fails, inform player
                    else
                        player.SendErrorMessage("Adding range {0} into database failed.", args.Parameters[1]);
                    break;
                case "del":
                    //Checks if player has put in either CIDR range or IP
                    if (args.Parameters.Count < 2)
                    {
                        player.SendErrorMessage("Invalid syntax! Proper syntax: {0}cidrban del <ip/range>", Commands.Specifier);
                        return;
                    }
                    //Checks if input is CIDR range
                    if (Regex.IsMatch(args.Parameters[1], rangeregex))
                    {
                        //Removes ban from database
                        if (cidrbans.DelCIDRBanByRange(args.Parameters[1]))
                            player.SendSuccessMessage("Unbanned range {0}.", args.Parameters[1]);
                        //If it fails, inform player
                        else
                            player.SendErrorMessage("Removing range {0} from database failed.", args.Parameters[1]);
                        return;
                    }
                    //Checks if input is IP
                    if (Regex.IsMatch(args.Parameters[1], ipregex))
                    {
                        //List of string for removed CIDR ranges
                        List<string> removed = cidrbans.DelCIDRBanByIP(args.Parameters[1]);
                        //If no CIDR ranges are removed, inform player that it fails
                        if (removed.Count == 0)
                        {
                            player.SendErrorMessage("Removing range {0} from database failed.", args.Parameters[1]);
                            return;
                        }
                        //Inform player amount of ranges removed and which
                        player.SendSuccessMessage("Removed {0} range{1} from the database:", removed.Count, removed.Count == 1 ? "" : "s");
                        player.SendInfoMessage(String.Join(", ", removed));
                        return;
                    }
                    //If player's input doesn't match either range or IP, inform player
                    player.SendErrorMessage("Invalid syntax! Proper syntax: {0}cidrban del <ip/range>", Commands.Specifier);
                    player.SendErrorMessage("IP proper format: 0-255.0-255.0-255.0-255");
                    player.SendErrorMessage("CIDR range proper format : 0-255.0-255.0-255.0-255/0-32");
                    break;
                case "list":
                    //PageNumber integer to use with TShock's built-in PaginationTools
                    int pagenumber;
                    if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pagenumber))
                        return;
                    //List of all CIDR Ban Informations
                    List<CIDRBan> list = cidrbans.GetCIDRBanList();
                    //Creates list of CIDR ranges
                    var namelist = from ban in list
                                            select ban.CIDR;
                    //Inform player, allows selecting page
                    PaginationTools.SendPage(player, pagenumber, PaginationTools.BuildLinesFromTerms(namelist),
                            new PaginationTools.Settings
                            {
                                HeaderFormat = "CIDR Range Bans ({0}/{1}):",
                                FooterFormat = "Type {0}ban list {{0}} for more.".SFormat(Commands.Specifier),
                                NothingToDisplayString = "There are currently no CIDR range bans."
                            });
                    break;
                default:
                    player.SendErrorMessage("Invalid subcommand. Type {0}help cidrban for information.", Commands.Specifier);
                    break;
            }
        }
    }
}
