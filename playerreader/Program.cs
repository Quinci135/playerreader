using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using Rests;
using System.ComponentModel;
using HttpServer;
using TShockAPI.DB;
using MySql;

namespace PlayerReader
{
    [ApiVersion(2, 1)]
    public class PlayerReader : TerrariaPlugin
    {
        public override string Author => "Quinci";

        public override string Description => "Adds a rest endpoint for more player data.";

        public override string Name => "Player Rest";

        public override Version Version => new Version(1, 0, 0, 0);

        public PlayerReader(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInit);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
            }
            base.Dispose(disposing);
        }

        private void OnInit(EventArgs e)
        {
            TShock.RestApi.RegisterRedirect("/readplayers", "/readplayers");
            TShock.RestApi.Register(new SecureRestCommand("/readplayers", PlayerRead, RestPermissions.restuserinfo));
        }

        private object PlayerFind(IParameterCollection parameters)
        {
            string name = parameters["player"];
            if (string.IsNullOrWhiteSpace(name))
            {
                return new RestObject("400") { Error = "Missing or empty 'player' parameter" };
            }

            var found = TSPlayer.FindByNameOrID(name);
            if (found.Count == 1)
            {
                return found[0];
            }
            else if (found.Count == 0)
            {
                UserAccount account = TShock.UserAccounts.GetUserAccountByName(name);
                if (account != null)
                {
                    try
                    {
                        using (var reader = TShock.DB.QueryReader("SELECT * FROM sscinventory WHERE Account=@0", account.ID))
                        {
                            if (reader.Read())
                            {
                                List<NetItem> inventoryList = reader.Get<string>("Inventory").Split('~').Select(NetItem.Parse).ToList();
                                object items = new
                                {
                                    inventoryList
                                };

                                return new RestObject
                                {
                                    {"online" , "false"},
                                    {"nickname", account.Name},
                                    {"username", account.Name},
                                    {"group", account.Group},
                                    {"position", "Player is offline."},
                                    {"items", items},
                                };
                            }
                            else
                            {
                                return new RestObject("400") { Error = "DB could not be read." };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        TShock.Log.Error(ex.ToString());
                        return new RestObject("400") { Error = "Player " + name + " was not found online or saved offline." };
                    }
                }
                else
                {
                    return new RestObject("400") { Error = "Player " + name + " was not found online or saved offline." };
                }
            }
            else
            {
                return new RestObject("400") { Error = "Player " + name + " matches " + found.Count + " players" };
            }
        }

        [Description("Get information for a user.")]
        [Route("/readplayers")]
        [Permission(RestPermissions.restuserinfo)]
        [Noun("player", true, "The player to lookup", typeof(String))]
        [Token]
        private object PlayerRead (RestRequestArgs args)
        {
            var ret = PlayerFind(args.Parameters);
            if (ret is RestObject)
            {
                return ret;
            }

            TSPlayer player = (TSPlayer)ret;

            var items = new
            {
                inventory = player.TPlayer.inventory.Where(i => i.active).Select(item => (NetItem)item),
                equipment = player.TPlayer.armor.Where(i => i.active).Select(item => (NetItem)item),
                dyes = player.TPlayer.dye.Where(i => i.active).Select(item => (NetItem)item),
                miscEquip = player.TPlayer.miscEquips.Where(i => i.active).Select(item => (NetItem)item),
                miscDye = player.TPlayer.miscDyes.Where(i => i.active).Select(item => (NetItem)item),
                piggy = player.TPlayer.bank.item.Where(i => i.active).Select(item => (NetItem)item),
                safe = player.TPlayer.bank2.item.Where(i => i.active).Select(item => (NetItem)item),
                trash = (NetItem)player.TPlayer.trashItem,
                forge = player.TPlayer.bank3.item.Where(i => i.active).Select(item => (NetItem)item),
                vault = player.TPlayer.bank4.item.Where(i => i.active).Select(item => (NetItem)item),
            };

            return new RestObject
            {
                {"online" , "true"},
                {"nickname", player.Name},
                {"username", player.Account?.Name},
                {"ip", player.IP},
                {"group", player.Group.Name},
                {"registered", player.Account?.Registered},
                {"muted", player.mute },
                {"position", player.TileX + "," + player.TileY},
                {"items", items},
                {"buffs", string.Join(", ", player.TPlayer.buffType)}
            };
        }
    }
}