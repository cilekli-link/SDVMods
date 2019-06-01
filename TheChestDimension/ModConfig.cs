using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheChestDimension
{
    class ModConfig
    {
        public SButton TCDKey { get; set; } = SButton.H;

        public bool ShowEntryMessage { get; set; } = false;
        public string EntryMessage { get; set; } = "Welcome to the Chest Dimension. Press {TCDkey} or go to the top-left corner to exit.";

        public string CanOnlyEnterFrom { get; set; } //if not empty, implies CanEnterFromMines = false
        public bool CanEnterFromCave { get; set; } = true;
        public bool ShowCannotEnterMessage { get; set; } = true;
        public string CannotEnterMessage { get; set; } = "You cannot enter the Chest Dimension from this location.";
    }
}
