using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheChestDimension
{
    public class ModEntry : Mod
    {
        // list of custom spawn locations of all players in current save
        List<playerEntry> customLocations;

        string currentPlayerID;
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
            currentPlayerID = helper.Data.ReadGlobalData<string>("TCDplayerID");
            if (currentPlayerID == null)
            {
                currentPlayerID = Guid.NewGuid().ToString();
                helper.Data.WriteGlobalData("TCDplayerID", currentPlayerID);
            }
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
                playerEntry entry = e.ReadAs<playerEntry>();
                if (Game1.IsMasterGame)
                {
                    // master: received position request from slave
                    if (e.Type == "TCDposRequest")
                    {
                        playerEntry pe = null;
                        foreach (playerEntry player in customLocations)
                        {
                            if (player.ID == entry.ID) pe = player;
                        }
                        if (pe != null)
                        {
                            Helper.Multiplayer.SendMessage(pe, "TCDpos", new[] { ModManifest.UniqueID });
                        }
                        else
                        {
                            Helper.Multiplayer.SendMessage(new playerEntry(entry.ID), "TCDpos", new[] { ModManifest.UniqueID });
                        }
                    }

                    // master: received position set request from slave
                    if (e.Type == "TCDposSet")
                    {
                        bool foundPlayer = false;
                        foreach (playerEntry player in customLocations)
                        {
                            if (player.ID == entry.ID)
                            {
                                foundPlayer = true;
                                player.pos = entry.pos;
                            }
                        }
                        if (!foundPlayer) { customLocations.Add(entry); }
                        Helper.Multiplayer.SendMessage(entry, "TCDconfirmPosSet", new[] { ModManifest.UniqueID });
                    }
                }
                else
                {
                    // slave: received position from master
                    if (e.Type == "TCDpos" && entry.ID == currentPlayerID)
                    {
                        if (entry.empty)
                        {
                            customPos = null;
                        }
                        else
                        {
                            customPos = entry.pos;
                        }
                        waitingToWarp = false;
                        locReceived = true;
                        Warp();
                    }

                    // slave: received position set confirmation from master
                    if (e.Type.StartsWith("TCDconfirmPosSet") && entry.ID == currentPlayerID)
                    {
                        string x = entry.pos.X;
                        string y = entry.pos.Y;
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
                    Helper.Data.WriteSaveData("TCDspawnPos_" + currentPlayerID, customPos);
                    Game1.chatBox.addInfoMessage("TCD spawn position set to " + xPos + "," + yPos + ".");
                }
                else
                {
                    Helper.Multiplayer.SendMessage(new playerEntry(currentPlayerID,customPos), "TCDposSet", new[] { ModManifest.UniqueID });
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
                customPos = Helper.Data.ReadSaveData<spawnPos>("TCDspawnPos_" + currentPlayerID);
                return true;
            }
            else
            {
                waitingToWarp = true;
                Helper.Multiplayer.SendMessage(new playerEntry(currentPlayerID), "TCDposRequest", new[] { ModManifest.UniqueID });
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
        public string ID;
        public spawnPos pos;
        public bool empty = false;
        public playerEntry()
        {

        }
        public playerEntry(string id, spawnPos pos)
        {
            ID = id;
            this.pos = pos;
        }
        public playerEntry(string id)
        {
            ID = id;
            empty = true;
        }
    }
}