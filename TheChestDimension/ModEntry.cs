using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Network;
using xTile;

namespace TheChestDimension
{
    public class ModEntry : Mod
    {
        private ModConfig config;

        // Player's location and position before the warp
        GameLocation OldLocation;
        Vector2 OldPosition;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            config = Helper.ReadConfig<ModConfig>();
        }

        private void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (e.Button == config.TCDKey)
            {
                // if already in TCD, go back
                if (Game1.player.currentLocation.Name == "ChestDimension")
                {
                    Game1.warpFarmer(OldLocation.Name, (int)OldPosition.X, (int)OldPosition.Y, false);
                    return;
                }

                // shorten string names, for the upcoming monstrosity of an if statement
                string coef = config.CanOnlyEnterFrom;
                string curloc = Game1.player.currentLocation.Name;

                if
                (
                    // No specific entry location, or if there is any, it's the same as the current location
                    (coef == null || coef == "" || coef == curloc)
                    &&
                    // Player is not in a cave, when CanEnterFromCave is false
                    !(!config.CanEnterFromCave && curloc.StartsWith("UndergroundMine"))
                )
                {
                    // Remember old locations and warp to TCD
                    OldLocation = Game1.player.currentLocation;
                    OldPosition = Game1.player.getTileLocation();
                    Game1.warpFarmer("ChestDimension", 40, 22, false);

                    // If entry message is enabled, replace variables and show entry message in chat
                    if (config.ShowEntryMessage)
                    {
                        string msg = config.EntryMessage;
                        msg.Replace("{TCDkey}", config.TCDKey.ToString());
                        Game1.chatBox.addInfoMessage(msg);
                    }
                }
                else // Cannot enter TCD
                {
                    if (config.ShowCannotEnterMessage)
                    {
                        Game1.chatBox.addInfoMessage(config.CannotEnterMessage);
                    }
                }
            }
        }
    }
}