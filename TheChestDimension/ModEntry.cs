using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;
using System.Linq;

namespace TheChestDimension
{
    public class ModEntry : Mod
    {
        // list of custom spawn locations of all players in current save
        List<playerEntry> customLocations;

        private ModConfig config;
        // custom spawn position
        private spawnPos customPos;
        // player's location and position before the warp
        GameLocation OldLocation;
        Vector2 OldPosition;
        // true after requesting playerEntry from master, false after receiving spawnPos
        private bool waitingToWarp = false;
        // true after receiving playerEntry, to prevent asking for spawnPos multiple times
        private bool locReceived = false;
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            helper.Events.GameLoop.DayEnding += GameLoop_DayEnding;
            helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageReceived;
            config = Helper.ReadConfig<ModConfig>();
        }

        private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
        {
            if (Game1.IsMasterGame) Helper.Data.WriteSaveData("TCDcustomSpawnLocations", customLocations);
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (Game1.IsMasterGame)
            {
                customLocations = Helper.Data.ReadSaveData<List<playerEntry>>("TCDcustomSpawnLocations");
                if (customLocations == null) customLocations = new List<playerEntry>();
            }
        }

        private void Multiplayer_ModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID == ModManifest.UniqueID)
            {
                if (Game1.IsMasterGame)
                {
                    // master: received spawnPos request from slave
                    if (e.Type == "TCDposRequest")
                    {
                        long playerID = e.FromPlayerID;
                        playerEntry pe = null;
                        foreach (playerEntry player in customLocations)
                        {
                            if (player.ID == playerID) pe = player;
                        }
                        if (pe != null)
                        {
                            Helper.Multiplayer.SendMessage(pe, "TCDpos", new[] { ModManifest.UniqueID });
                        }
                        else
                        {
                            Helper.Multiplayer.SendMessage(new playerEntry(playerID), "TCDpos", new[] { ModManifest.UniqueID });
                        }
                    }

                    // master: received spawnPos set request from slave
                    if (e.Type == "TCDposSet")
                    {
                        long playerID = e.FromPlayerID;
                        spawnPos newPos = e.ReadAs<spawnPos>();
                        playerEntry newEntry = new playerEntry(playerID, newPos);
                        bool foundPlayer = false;
                        foreach (playerEntry player in customLocations)
                        {
                            if (player.ID == playerID)
                            {
                                foundPlayer = true;
                                player.pos = newPos;
                            }
                        }
                        if (!foundPlayer) { customLocations.Add(newEntry); }
                        Helper.Multiplayer.SendMessage(newEntry, "TCDconfirmPosSet", new[] { ModManifest.UniqueID });
                    }
                }
                else
                {
                    // slave: received spawnPos from master
                    if (e.Type == "TCDpos" && e.ReadAs<playerEntry>().ID == Game1.player.UniqueMultiplayerID)
                    {
                        if (e.ReadAs<playerEntry>().empty)
                        {
                            customPos = null;
                        }
                        else
                        {
                            customPos = e.ReadAs<playerEntry>().pos;
                        }
                        waitingToWarp = false;
                        locReceived = true;
                        Warp();
                    }

                    // slave: received spawnPos set confirmation from master
                    if (e.Type.StartsWith("TCDconfirmPosSet") && e.ReadAs<playerEntry>().ID == Game1.player.UniqueMultiplayerID)
                    {
                        playerEntry pe = e.ReadAs<playerEntry>();
                        string x = pe.pos.X;
                        string y = pe.pos.Y;
                        Game1.chatBox.addInfoMessage("TCD spawn position of " + Game1.player.Name + " set to " + x + "," + y + ".");
                    }
                }
            }
        }

        private void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (e.Button == config.TCDKey)
            {
                if (!waitingToWarp && Game1.player.CanMove)
                {
                    Warp();
                }
            }
            else if (e.Button == config.SetSpawnKey)
            {
                SetSpawnPos();
            }
        }

        private void SetSpawnPos()
        {
            if (PlayerInTCD)
            {
                string xPos = Game1.player.getTileX().ToString();
                string yPos = Game1.player.getTileY().ToString();
                customPos = new spawnPos(xPos, yPos);
                if (Game1.IsMasterGame)
                {
                    Helper.Data.WriteSaveData("TCDspawnPos_" + Game1.player.UniqueMultiplayerID.ToString(), customPos);
                    Game1.chatBox.addInfoMessage("TCD spawn position set to " + xPos + "," + yPos + ".");
                }
                else
                {
                    Helper.Multiplayer.SendMessage(customPos, "TCDposSet", new[] { ModManifest.UniqueID });
                    //Game1.chatBox.addInfoMessage("Sent current TCD position to the host, waiting for confirmation.");
                }
            }
            else
            {
                Game1.chatBox.addErrorMessage("You have to be inside TCD to set your spawn position.");
            }
        }

        private bool PlayerInTCD => Game1.player.currentLocation.Name == "ChestDimension";

        bool getSpawnPos()
        {
            if (Game1.IsMasterGame)
            {
                customPos = Helper.Data.ReadSaveData<spawnPos>("TCDspawnPos_" + Game1.player.UniqueMultiplayerID.ToString());
                return true;
            }
            else
            {
                waitingToWarp = true;
                Helper.Multiplayer.SendMessage(new object(), "TCDposRequest", new[] { ModManifest.UniqueID });
                return false;
            }
        }

        void Warp()
        {
            // if already in TCD, go back
            if (PlayerInTCD)
            {
                Game1.warpFarmer(new LocationRequest("oldLoc", true, OldLocation), (int)OldPosition.X, (int)OldPosition.Y, 0);
                return;
            }

            // shorten string names
            string coef = config.CanOnlyEnterFrom;
            string curloc = Game1.player.currentLocation.Name;

            if

                // no specific entry location, or if there is any, it's the same as the current location
                (coef == null || coef == "" || coef == curloc)
            {
                // player is not in a cave, when CanEnterFromCave is false
                if (!(!config.CanEnterFromCave && curloc.StartsWith("UndergroundMine")))
                {

                    OldLocation = Game1.player.currentLocation;
                    OldPosition = Game1.player.getTileLocation();


                    if (!locReceived)
                    {
                        if (!getSpawnPos())
                        {
                            return;
                        }
                    }

                    // if custom spawn location is null, warp to default spawn location, else warp to custom spawn location 
                    if (customPos == null)
                    {
                        Game1.warpFarmer("ChestDimension", 55, 37, false);
                    }
                    else
                    {
                        Game1.warpFarmer("ChestDimension", int.Parse(customPos.X), int.Parse(customPos.Y), false);
                    }

                    // if entry message is enabled, replace variables and show entry message in chat
                    if (config.ShowEntryMessage)
                    {
                        string msg = config.EntryMessage;
                        msg.Replace("{TCDkey}", config.TCDKey.ToString());
                        Game1.chatBox.addInfoMessage(msg);
                    }

                }
            }

            else // cannot enter TCD
            {
                if (config.ShowCannotEnterMessage)
                {
                    Game1.chatBox.addInfoMessage(config.CannotEnterMessage);
                }
            }
        }
    }


    class spawnPos
    {
        public string X;
        public string Y;

        public spawnPos()
        {
        }

        public spawnPos(string x, string y)
        {
            X = x;
            Y = y;
        }
    }

    class playerEntry
    {
        public long ID;
        public spawnPos pos;
        public bool empty = false;
        public playerEntry()
        {

        }
        public playerEntry(long id, spawnPos pos)
        {
            ID = id;
            this.pos = pos;
        }
        public playerEntry(long id)
        {
            ID = id;
            empty = true;
        }
    }
}