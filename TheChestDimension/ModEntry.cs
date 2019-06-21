using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;

namespace TheChestDimension
{
    public class ModEntry : Mod
    {
        // list of entries for all players in current save
        // player entries currently only contain custom spawn locations
        List<playerEntry> entries;

        // entry of the current player
        playerEntry currentEntry = new playerEntry();

        private ModConfig config;

        // custom spawn position
        // player's location and position before the warp
        GameLocation OldLocation;
        Vector2 OldPosition;
        // true after requesting playerEntry from master, false after receiving playerEntry
        private bool waitingToWarp = false;
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            helper.Events.GameLoop.DayEnding += GameLoop_DayEnding;
            helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageReceived;
            config = Helper.ReadConfig<ModConfig>();
            currentEntry.ID = helper.Data.ReadGlobalData<string>("TCDplayerID");
            if (currentEntry.ID == null)
            {
                currentEntry.ID = Guid.NewGuid().ToString();
                helper.Data.WriteGlobalData("TCDplayerID", currentEntry.ID);
            }
        }

        private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
        {
            if (Game1.IsMasterGame) Helper.Data.WriteSaveData("TCDplayerEntries", entries);
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (Game1.IsMasterGame)
            {
                entries = Helper.Data.ReadSaveData<List<playerEntry>>("TCDplayerEntries");
                if (entries == null) entries = new List<playerEntry>();
                currentEntry = getEntryWithID(currentEntry.ID);
            }
        }

        private void Multiplayer_ModMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID == ModManifest.UniqueID)
            {
                playerEntry receivedEntry = e.ReadAs<playerEntry>();
                if (Game1.IsMasterGame)
                {
                    // master: received entry request from slave
                    if (e.Type == "TCDentryRequest")
                    {
                        Helper.Multiplayer.SendMessage(getEntryWithID(receivedEntry.ID), "TCDentry", new[] { ModManifest.UniqueID });
                    }

                    // master: received entry set request from slave
                    if (e.Type == "TCDentrySet")
                    {
                        addOrUpdateEntry(receivedEntry);
                    }
                }
                else
                {
                    // slave: received entry from master
                    if (e.Type == "TCDentry" && receivedEntry.ID == currentEntry.ID)
                    {
                        currentEntry = receivedEntry;
                        if (currentEntry.emptyPos)
                        {
                            currentEntry.customPos = null;
                        }
                        waitingToWarp = false;
                        Warp();
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
                currentEntry.emptyPos = false;
                currentEntry.customPos = new spawnPos(xPos, yPos);
                Game1.chatBox.addInfoMessage("TCD spawn position set to " + xPos + "," + yPos + ".");
                if (!Game1.IsMasterGame)
                {
                    Helper.Multiplayer.SendMessage(currentEntry, "TCDentrySet", new[] { ModManifest.UniqueID });
                }
            }
            else
            {
                Game1.chatBox.addErrorMessage("You have to be inside TCD to set your spawn position.");
            }
        }

        private bool PlayerInTCD => Game1.player.currentLocation.Name == "ChestDimension";



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

                    if (!Game1.IsMasterGame)
                    {
                        waitingToWarp = true;
                        requestEntry();
                        return;
                    }
                    // if custom spawn location is null, warp to default spawn location, else warp to custom spawn location 
                    if (currentEntry.emptyPos)
                    {
                        Game1.warpFarmer("ChestDimension", 55, 37, false);
                    }
                    else
                    {
                        Game1.warpFarmer("ChestDimension", int.Parse(currentEntry.customPos.X), int.Parse(currentEntry.customPos.Y), false);
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

        private void requestEntry()
        {
            Helper.Multiplayer.SendMessage(new playerEntry(currentEntry.ID), "TCDentryRequest", new[] { ModManifest.UniqueID });
        }

        void addOrUpdateEntry(playerEntry entry)
        {
            foreach (playerEntry e in entries)
            {
                if (e.ID == entry.ID)
                {
                    e.update(entry);
                    return;
                }
            }
            entries.Add(entry);
        }

        playerEntry getEntryWithID(string ID)
        {
            foreach (playerEntry e in entries)
            {
                if (e.ID == ID) return e;
            }
            return new playerEntry(ID);
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
        public spawnPos customPos;
        public bool emptyPos = false;
        public playerEntry()
        {
            emptyPos = true;
        }
        public playerEntry(string id, spawnPos customPos)
        {
            ID = id;
            this.customPos = customPos;
        }
        public playerEntry(string id)
        {
            ID = id;
            emptyPos = true;
        }
        public void update(playerEntry updateFrom)
        {
            customPos = updateFrom.customPos;
            emptyPos = updateFrom.emptyPos;
        }
    }


}